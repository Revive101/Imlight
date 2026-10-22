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
 * The live state of one house: its placed items under one lock, persisted on every change, shared
 * by every player who is in the house.
 * 
 * USAGE EXAMPLE:
 * var change = state.TryAdd(item, subType);
 * if (change.Done) {
 *     // patch GID change.GidId of blob change.SubType, version change.Revision
 * }
 * 
 * NOTE:
 * - Every change is written to the database before it returns. A failed write undoes the change
 *   in memory, so the stored house and the live house never differ.
 * - A change that also moves an item to or from a backpack passes its own save, which writes both in one transaction.
 * - Stored items are never changed in place; an update replaces the item. A change names the item by its id,
 *   which survives a renumbering; the result carries the blob, slot and patch GID of the moment.
 * - The house has one live blob per kind (HousingItems.LiveSubTypes): plain objects, pets and mounts. The kind is not stored: it
 *   is derived from the item's template whenever the house is loaded. Each blob has its own base, revision and slot table:
 *   GID id = base + slot, and a removed item leaves a null slot, so the numbers in later patches stay valid. The tables are
 *   built, with new bases and revisions no client has seen, when the state is created, and a table again when it is full.
 *   Clients that still use the old base ask for the blob when a patch does not fit.
 * - The bases of one house are far enough apart that no two blobs share an id.
 * - Use HouseRegistry.TryEnter and HouseRegistry.Release to count viewers.
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
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// How a change to a house ended.
/// </summary>
internal enum HouseEdit {
    Done,
    LimitReached,
    NotFound,
    SaveFailed,
    NeedsResync,
}

/// <summary>
/// The result of one change, with what its patch needs.
/// </summary>
/// <param name="Result">How the change ended; the other values are only set for <see cref="HouseEdit.Done"/>.</param>
/// <param name="SubType">The subtype of the blob the change is in.</param>
/// <param name="Slot">The slot of the changed item in that blob.</param>
/// <param name="GidId">The patch's <c>GIDID</c>: the blob's base plus the slot.</param>
/// <param name="Revision">The revision the change produced: the patch's version.</param>
/// <param name="Item">The item as it is stored after the change; for a removal, as it was before.</param>
internal readonly record struct HouseChange(HouseEdit Result, string SubType = null, int Slot = -1, uint GidId = 0, uint Revision = 0, PlacedItem Item = null) {

    internal bool Done => Result == HouseEdit.Done;

}

/// <summary>
/// A consistent copy of one blob of a house for building it.
/// </summary>
/// <param name="SubType">The blob's subtype.</param>
/// <param name="Revision">The blob version.</param>
/// <param name="GidBase">The base the blob entries are numbered from.</param>
/// <param name="LiveCount">The number of placed items in the blob, not counting removed slots.</param>
/// <param name="Slots">The items in slot order; a removed slot is <c>null</c>.</param>
internal readonly record struct HouseSnapshot(string SubType, uint Revision, uint GidBase, int LiveCount, IReadOnlyList<PlacedItem> Slots);

/// <summary>
/// The live, persisted state of one house.
/// </summary>
internal sealed class HouseState {

    /// <summary>
    /// The most blob entries one blob holds: a string field is at most 65,535 bytes, the blob header takes
    /// 47 and every entry 21.
    /// </summary>
    internal const int MAX_SLOTS = HousingGids.MAX_SLOTS;

    private readonly Lock _lock = new();
    private readonly House _house;
    private readonly HouseTemplateInfo _template;
    private readonly Dictionary<string, Table> _tables = new(StringComparer.Ordinal);
    private int _viewers;
    private bool _retired;

    private HouseState(House house, HouseTemplateInfo template) {
        _house = house;
        _template = template;
        foreach (var subType in HousingItems.LiveSubTypes) {
            _tables[subType] = new Table { GidBase = HousingGids.NewBaseApart([.. _tables.Values.Select(other => other.GidBase)]) };
        }
    }

    internal ulong LotGid => _house.LotGid;

    internal ulong OwnerCharId => _house.OwnerCharId;

    internal string ZoneName => _template.LotZone;

    internal int LiveCount {
        get {
            lock (_lock) {
                return _house.Items.Count;
            }
        }
    }

    /// <summary>
    /// The number of items the house may hold: the client's own formula, the template's limit plus the bonus slots.
    /// </summary>
    internal int ItemLimit {
        get {
            lock (_lock) {
                return _template.ItemLimit + _house.BonusSlots;
            }
        }
    }

    internal int CastleBlockLimit => _template.CastleBlockLimit;

    internal bool IsRetired {
        get {
            lock (_lock) {
                return _retired;
            }
        }
    }

    /// <summary>
    /// Creates the live state of a stored house. The items are numbered by their ids in the blob of their kind, every blob
    /// gets a new base, and the revisions move once and are stored, so a client with an older cache fetches the blobs
    /// again. A pet or a mount that was stored without its entry data gets it from its item document.
    /// </summary>
    /// <param name="house">The stored house; the state owns it from now on.</param>
    /// <param name="template">The house's template.</param>
    /// <param name="state">The live state when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the new revisions could not be stored.</returns>
    internal static bool TryCreate(House house, HouseTemplateInfo template, out HouseState state) {
        state = null;
        var previousItems = house.Items;
        var previousRevisions = house.Revisions;
        var ordered = previousItems.OrderBy(item => item.Id).Select(item => WithCustom(house.OwnerCharId, item)).ToList();
        var revisions = new Dictionary<string, uint>(previousRevisions);
        foreach (var subType in HousingItems.LiveSubTypes) {
            revisions[subType] = revisions.GetValueOrDefault(subType) + 1;
        }

        house.Items = ordered;
        house.Revisions = revisions;
        if (!HouseCollection.Save(house)) {
            house.Items = previousItems;
            house.Revisions = previousRevisions;

            return false;
        }

        state = new HouseState(house, template);
        foreach (var group in ordered.GroupBy(SubTypeOf)) {
            state._tables[group.Key].Slots.AddRange(group);
        }

        return true;
    }

    /// <summary>
    /// Copies one blob of the house for building it.
    /// </summary>
    /// <param name="subType">The blob's subtype, one of <see cref="HousingItems.LiveSubTypes"/>.</param>
    /// <returns>The revision, base, live count and slots, taken together.</returns>
    internal HouseSnapshot Snapshot(string subType) {
        lock (_lock) {
            var table = _tables[subType];

            return new HouseSnapshot(subType, RevisionOf(subType), table.GidBase, table.Live, [.. table.Slots]);
        }
    }

    /// <summary>
    /// Copies every item of the house, blob by blob, in slot order.
    /// </summary>
    /// <returns>The placed items, not counting removed slots.</returns>
    internal IReadOnlyList<PlacedItem> Items() {
        lock (_lock) {
            return [.. _tables.Values.SelectMany(table => table.Slots).Where(item => item is not null)];
        }
    }

    /// <summary>
    /// Finds the item a housing object GID names. The client builds the GID as
    /// <c>(u16 &lt;&lt; 48) | (9 &lt;&lt; 40) | GIDID</c>.
    /// </summary>
    /// <param name="gid">The GID the client sent.</param>
    /// <param name="item">The item in the slot; <c>null</c> when the slot was removed.</param>
    /// <param name="subType">The subtype of the blob the GID is in.</param>
    /// <returns><c>true</c> when the GID is a housing object GID inside the numbering of one of this house's blobs.</returns>
    internal bool TryFindByGid(ulong gid, out PlacedItem item, out string subType) {
        item = null;
        subType = null;
        lock (_lock) {
            foreach (var (name, table) in _tables) {
                if (HousingGids.TryGetSlot(gid, table.GidBase, table.Slots.Count, out var slot)) {
                    item = table.Slots[slot];
                    subType = name;

                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Counts the placed items of a template.
    /// </summary>
    /// <param name="templateId">The item template.</param>
    /// <returns>The number of placed copies.</returns>
    internal int CountTemplate(uint templateId) {
        lock (_lock) {
            return _house.Items.Count(item => item.TemplateId == templateId);
        }
    }

    /// <summary>
    /// Places an item in a new slot at the end of the table of its blob. When the table is full of removed slots it is
    /// numbered again instead and nothing is added.
    /// </summary>
    /// <param name="item">The item to place; its id is replaced by the next free one.</param>
    /// <param name="subType">The subtype of the blob the item goes in, one of <see cref="HousingItems.LiveSubTypes"/>.</param>
    /// <param name="save">Writes the house; <c>null</c> writes the house alone.</param>
    /// <returns>
    /// <see cref="HouseEdit.LimitReached"/> at the item limit, <see cref="HouseEdit.NeedsResync"/> when the table was
    /// numbered again (every viewer needs the blob), <see cref="HouseEdit.SaveFailed"/> when the write failed.
    /// </returns>
    internal HouseChange TryAdd(PlacedItem item, string subType, Func<House, bool> save = null) {
        lock (_lock) {
            if (_house.Items.Count >= _template.ItemLimit + _house.BonusSlots) {
                return new HouseChange(HouseEdit.LimitReached);
            }

            var table = _tables[subType];
            if (table.Slots.Count >= MAX_SLOTS) {
                if (table.Live >= table.Slots.Count) {
                    return new HouseChange(HouseEdit.LimitReached);
                }

                return new HouseChange(Renumber(subType, table) ? HouseEdit.NeedsResync : HouseEdit.SaveFailed);
            }

            var revision = RevisionOf(subType);
            var hadRevision = _house.Revisions.TryGetValue(subType, out var storedRevision);
            var added = item with { Id = _house.NextItemId };
            _house.Items.Add(added);
            _house.NextItemId = added.Id + 1;
            _house.Revisions[subType] = revision + 1;
            if (!Persist(save)) {
                _house.Items.RemoveAt(_house.Items.Count - 1);
                _house.NextItemId = added.Id;
                RestoreRevision(subType, hadRevision, storedRevision);

                return new HouseChange(HouseEdit.SaveFailed);
            }

            table.Slots.Add(added);
            var slot = table.Slots.Count - 1;

            return new HouseChange(HouseEdit.Done, subType, slot, GidIdOf(table, slot), revision + 1, added);
        }
    }

    /// <summary>
    /// Removes a placed item. Its slot stays in its blob's table as a null slot.
    /// </summary>
    /// <param name="itemId">The id of the item to remove.</param>
    /// <param name="save">Writes the house; <c>null</c> writes the house alone.</param>
    /// <returns><see cref="HouseEdit.NotFound"/> when the house has no such item, <see cref="HouseEdit.SaveFailed"/> when the write failed.</returns>
    internal HouseChange TryRemove(uint itemId, Func<House, bool> save = null) {
        lock (_lock) {
            if (Locate(itemId) is not { } found) {
                return new HouseChange(HouseEdit.NotFound);
            }

            var previous = found.Table.Slots[found.Slot];
            var index = _house.Items.FindIndex(candidate => candidate.Id == itemId);
            var revision = RevisionOf(found.SubType);
            var hadRevision = _house.Revisions.TryGetValue(found.SubType, out var storedRevision);
            _house.Items.RemoveAt(index);
            _house.Revisions[found.SubType] = revision + 1;
            if (!Persist(save)) {
                _house.Items.Insert(index, previous);
                RestoreRevision(found.SubType, hadRevision, storedRevision);

                return new HouseChange(HouseEdit.SaveFailed);
            }

            found.Table.Slots[found.Slot] = null;

            return new HouseChange(HouseEdit.Done, found.SubType, found.Slot, GidIdOf(found.Table, found.Slot), revision + 1, previous);
        }
    }

    /// <summary>
    /// Removes several placed items in one write. Each blob's revision moves by the number of items removed from it, so a
    /// client can be sent one delete patch per item with consecutive versions.
    /// </summary>
    /// <param name="itemIds">The ids of the items to remove; none may repeat.</param>
    /// <param name="removed">One change per item in the order of <paramref name="itemIds"/>: its blob, slot, GID id, the version of its patch and the item.</param>
    /// <param name="save">Writes the house; <c>null</c> writes the house alone.</param>
    /// <returns><see cref="HouseEdit.NotFound"/> when an item is not in the house, <see cref="HouseEdit.SaveFailed"/> when the write failed; nothing changes then.</returns>
    internal HouseEdit TryRemoveMany(IReadOnlyCollection<uint> itemIds, out IReadOnlyList<HouseChange> removed, Func<House, bool> save = null) {
        removed = [];
        lock (_lock) {
            if (itemIds.Count == 0 || itemIds.Distinct().Count() != itemIds.Count) {
                return HouseEdit.NotFound;
            }

            var located = itemIds.Select(Locate).ToList();
            if (located.Any(found => found is null)) {
                return HouseEdit.NotFound;
            }

            var previousItems = _house.Items;
            var previousRevisions = _house.Revisions;
            var revisions = new Dictionary<string, uint>(previousRevisions);
            var changes = new List<HouseChange>(located.Count);
            foreach (var found in located.Select(candidate => candidate.Value)) {
                var revision = (revisions.TryGetValue(found.SubType, out var current) ? current : 1) + 1;
                revisions[found.SubType] = revision;
                changes.Add(new HouseChange(HouseEdit.Done, found.SubType, found.Slot, GidIdOf(found.Table, found.Slot), revision, found.Table.Slots[found.Slot]));
            }

            var removedIds = itemIds.ToHashSet();
            _house.Items = [.. previousItems.Where(candidate => !removedIds.Contains(candidate.Id))];
            _house.Revisions = revisions;
            if (!Persist(save)) {
                _house.Items = previousItems;
                _house.Revisions = previousRevisions;

                return HouseEdit.SaveFailed;
            }

            foreach (var found in located.Select(candidate => candidate.Value)) {
                found.Table.Slots[found.Slot] = null;
            }

            removed = changes;

            return HouseEdit.Done;
        }
    }

    /// <summary>
    /// Replaces a placed item with an updated copy of itself.
    /// </summary>
    /// <param name="itemId">The id of the item to update.</param>
    /// <param name="update">Returns the replacement for the stored item; it must keep the id and the template.</param>
    /// <returns>
    /// <see cref="HouseEdit.NotFound"/> when the house has no such item or <paramref name="update"/> returns <c>null</c>
    /// or an item of another id or template, <see cref="HouseEdit.SaveFailed"/> when the write failed.
    /// </returns>
    internal HouseChange TryUpdate(uint itemId, Func<PlacedItem, PlacedItem> update) {
        lock (_lock) {
            if (Locate(itemId) is not { } found) {
                return new HouseChange(HouseEdit.NotFound);
            }

            var previous = found.Table.Slots[found.Slot];
            var replacement = update(previous);
            if (replacement is null || ReferenceEquals(replacement, previous)
                || replacement.Id != previous.Id || replacement.TemplateId != previous.TemplateId) {
                return new HouseChange(HouseEdit.NotFound);
            }

            var index = _house.Items.FindIndex(candidate => candidate.Id == itemId);
            var revision = RevisionOf(found.SubType);
            var hadRevision = _house.Revisions.TryGetValue(found.SubType, out var storedRevision);
            _house.Items[index] = replacement;
            _house.Revisions[found.SubType] = revision + 1;
            if (!Persist(null)) {
                _house.Items[index] = previous;
                RestoreRevision(found.SubType, hadRevision, storedRevision);

                return new HouseChange(HouseEdit.SaveFailed);
            }

            found.Table.Slots[found.Slot] = replacement;

            return new HouseChange(HouseEdit.Done, found.SubType, found.Slot, GidIdOf(found.Table, found.Slot), revision + 1, replacement);
        }
    }

    /// <summary>
    /// Counts a player into the house.
    /// </summary>
    /// <returns><c>false</c> when the state was retired by its last viewer leaving; get the house from the registry again.</returns>
    internal bool Enter() {
        lock (_lock) {
            if (_retired) {
                return false;
            }

            _viewers++;

            return true;
        }
    }

    /// <summary>
    /// Counts a player out of the house. The state is retired when the last viewer leaves.
    /// </summary>
    /// <returns><c>true</c> when this was the last viewer and the state is now retired.</returns>
    internal bool Leave() {
        lock (_lock) {
            if (_viewers > 0) {
                _viewers--;
            }

            if (_viewers > 0 || _retired) {
                return false;
            }

            _retired = true;

            return true;
        }
    }

    private static string SubTypeOf(PlacedItem item)
        => CoreObjectFactory.GetCoreTemplate(item.TemplateId) is WizItemTemplate template ? HousingItems.SubTypeOf(template) : HousingItems.ITEM_SUB_TYPE;

    private static PlacedItem WithCustom(ulong ownerCharId, PlacedItem item) {
        var subType = SubTypeOf(item);
        if (item.Custom is not null || subType == HousingItems.ITEM_SUB_TYPE) {
            return item;
        }

        return item with { Custom = HousingItems.LoadCustomData(ownerCharId, subType, item.ItemGid) };
    }

    private static uint GidIdOf(Table table, int slot)
        => unchecked(table.GidBase + (uint) slot);

    private Located? Locate(uint itemId) {
        foreach (var (subType, table) in _tables) {
            var slot = table.Slots.FindIndex(candidate => candidate?.Id == itemId);
            if (slot >= 0) {
                return new Located(subType, table, slot);
            }
        }

        return null;
    }

    private uint RevisionOf(string subType)
        => _house.Revisions.TryGetValue(subType, out var revision) ? revision : 1;

    private void RestoreRevision(string subType, bool hadRevision, uint storedRevision) {
        if (hadRevision) {
            _house.Revisions[subType] = storedRevision;
        } else {
            _house.Revisions.Remove(subType);
        }
    }

    private bool Renumber(string subType, Table table) {
        var previousSlots = table.Slots;
        var previousBase = table.GidBase;
        var hadRevision = _house.Revisions.TryGetValue(subType, out var storedRevision);
        table.Slots = [.. previousSlots.Where(item => item is not null)];
        table.GidBase = HousingGids.NewBaseApart([.. _tables.Values.Where(other => other != table).Select(other => other.GidBase)]);
        _house.Revisions[subType] = RevisionOf(subType) + 1;
        if (Persist(null)) {
            return true;
        }

        table.Slots = previousSlots;
        table.GidBase = previousBase;
        RestoreRevision(subType, hadRevision, storedRevision);

        return false;
    }

    private bool Persist(Func<House, bool> save) {
        try {
            return (save ?? HouseCollection.Save)(_house);
        } catch (Exception ex) {
            Logger.Error("Failed to write house {LotGid}: {Reason}", Logger.Args(_house.LotGid, ex.Message));

            return false;
        }
    }

    private readonly record struct Located(string SubType, Table Table, int Slot);

    private sealed class Table {

        internal List<PlacedItem> Slots = [];
        internal uint GidBase;

        internal int Live => Slots.Count(item => item is not null);

    }

}
