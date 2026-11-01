/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * The slot tables of one house or one attic, one per blob subtype, with the numbering that keeps their bases apart.
 *
 * USAGE EXAMPLE:
 * var blobs = new BlobSet<PlacedItem>(item => item.Id, HousingItems.LiveSubTypes);
 * var table = blobs.For(subType);
 * if (blobs.TryFindByGid(gid, out var item, out var subType)) { ... }
 *
 * NOTE:
 * - The client numbers the entries of a blob base, base + 1, and so on. The bases of one set are far enough apart that
 *   no two blobs share an id, so a GID names one blob.
 * - A table is created on first use, with a new base; Renumber closes the removed slots and draws a new base.
 * - The set is not thread-safe; its owner changes it under its own lock.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Housing.Items;

internal sealed class BlobSet<TItem> where TItem : class {

    private readonly Dictionary<string, BlobTable<TItem>> _tables = new(StringComparer.Ordinal);
    private readonly Func<TItem, uint> _idOf;

    /// <summary>
    /// Creates a set.
    /// </summary>
    /// <param name="idOf">Gets the id of an item.</param>
    /// <param name="subTypes">The subtypes whose tables exist from the start.</param>
    internal BlobSet(Func<TItem, uint> idOf, IEnumerable<string> subTypes = null) {
        _idOf = idOf;
        foreach (var subType in subTypes ?? []) {
            For(subType);
        }
    }

    /// <summary>
    /// Where an item is in the set.
    /// </summary>
    /// <param name="SubType">The subtype of the blob.</param>
    /// <param name="Table">The blob's table.</param>
    /// <param name="Slot">The slot in the table.</param>
    internal readonly record struct Location(string SubType, BlobTable<TItem> Table, int Slot);

    /// <summary>
    /// Gets the table of a subtype, creating it with a new base when there is none.
    /// </summary>
    /// <param name="subType">The blob's subtype.</param>
    /// <returns>The table.</returns>
    internal BlobTable<TItem> For(string subType) {
        if (_tables.TryGetValue(subType, out var table)) {
            return table;
        }

        table = new BlobTable<TItem> { GidBase = HousingGids.NewBaseApart(BasesExcept(null)) };
        _tables[subType] = table;

        return table;
    }

    /// <summary>
    /// Lists the items of every table, table by table, in slot order.
    /// </summary>
    /// <returns>The items, not counting removed slots.</returns>
    internal IReadOnlyList<TItem> AllItems()
        => [.. _tables.Values.SelectMany(table => table.Slots).Where(item => item is not null)];

    /// <summary>
    /// Lists the subtypes of the tables that hold an item.
    /// </summary>
    /// <returns>The subtype names.</returns>
    internal IEnumerable<string> SubTypesWithItems()
        => _tables.Where(pair => pair.Value.Live > 0).Select(pair => pair.Key);

    /// <summary>
    /// Finds the slot a GID names.
    /// </summary>
    /// <param name="gid">The GID the client sent.</param>
    /// <param name="item">The item in the slot; <c>null</c> when the slot was removed.</param>
    /// <param name="subType">The subtype of the blob the GID is in.</param>
    /// <returns><c>true</c> when the GID is inside the numbering of one of the tables.</returns>
    internal bool TryFindByGid(ulong gid, out TItem item, out string subType) {
        item = null;
        subType = null;
        foreach (var (name, table) in _tables) {
            if (!HousingGids.TryGetSlot(gid, table.GidBase, table.Slots.Count, out var slot)) {
                continue;
            }

            item = table.Slots[slot];
            subType = name;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds an item by its id.
    /// </summary>
    /// <param name="itemId">The id of the item.</param>
    /// <returns>Where the item is; <c>null</c> when no table holds it.</returns>
    internal Location? Locate(uint itemId) {
        foreach (var (subType, table) in _tables) {
            var slot = table.Slots.FindIndex(candidate => candidate is not null && _idOf(candidate) == itemId);
            if (slot >= 0) {
                return new Location(subType, table, slot);
            }
        }

        return null;
    }

    /// <summary>
    /// Closes the removed slots of a table, draws it a new base and moves its revision, in memory only.
    /// </summary>
    /// <param name="subType">The table's subtype.</param>
    /// <param name="table">The table.</param>
    /// <param name="revisions">The revisions of the document, by subtype.</param>
    /// <returns>An action that puts the table and the revision back as they were.</returns>
    internal Action Renumber(string subType, BlobTable<TItem> table, IDictionary<string, uint> revisions) {
        var previousSlots = table.Slots;
        var previousBase = table.GidBase;
        var undoRevision = Persistence.BumpRevision(revisions, subType, out _);
        table.Slots = [.. previousSlots.Where(item => item is not null)];
        table.GidBase = HousingGids.NewBaseApart(BasesExcept(table));

        return () => {
            table.Slots = previousSlots;
            table.GidBase = previousBase;
            undoRevision();
        };
    }

    private uint[] BasesExcept(BlobTable<TItem> table)
        => [.. _tables.Values.Where(other => other != table).Select(other => other.GidBase)];

}
