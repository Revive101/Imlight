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
 * The live state of one character's attic: the items under one lock, persisted on every change, numbered the way the
 * client numbers the entries of the attic blobs.
 *
 * USAGE EXAMPLE:
 * var change = state.TryAdd(item, subType);
 * if (change.Done) {
 *     // patch GID change.GidId of blob change.SubType, version change.Revision
 * }
 *
 * NOTE:
 * - Every item is in the one Attic blob (HousingItems.ATTIC_SUB_TYPE): the client loads every attic subtype the same way, so the
 *   kind of an item does not matter. A blob has its own base, revision and slot table: GID id = base + slot, and a removed
 *   item leaves a null slot so the numbers in later patches stay valid. An attic stored before the single blob has revisions
 *   for the old kind blobs; they stay, and the client is sent them empty, so it drops what it cached there.
 * - A pet or a mount carries its entry data, the serialized item behavior, so the client shows its look. An item stored
 *   without it gets it from its item document when the attic is loaded.
 * - Every change is written before it returns. A failed write undoes the change in memory.
 * - A change that also moves an item to or from a backpack passes its own save, which writes both in one transaction.
 * - The bases of one attic are far enough apart that no two blobs share an id.
 * - The slot tables, the revision moves and the write-or-undo step are shared with HouseState (BlobSet, Persistence); this
 *   class owns the attic document and its limit.
 * - Creating the state numbers every blob again with a new base and a revision no client has seen, and drops the items
 *   whose template is gone. Use AtticRegistry to get a state and count viewers.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Action = System.Action;

namespace Imlight.CoreLib.Game.Housing.Attics;

/// <summary>
/// The result of one change to an attic, with what its patch needs.
/// </summary>
/// <param name="Result">How the change ended; the other values are only set for <see cref="HouseEdit.Done"/>.</param>
/// <param name="SubType">The subtype of the blob the change is in.</param>
/// <param name="Slot">The slot of the changed item in that blob.</param>
/// <param name="GidId">The patch's <c>GIDID</c>: the blob's base plus the slot.</param>
/// <param name="Revision">The revision the change produced: the patch's version.</param>
/// <param name="Item">The item as it is stored after the change; for a removal, as it was before.</param>
internal readonly record struct AtticChange(HouseEdit Result, string SubType = null, int Slot = -1, uint GidId = 0, uint Revision = 0, AtticItem Item = null) {

    internal bool Done => Result == HouseEdit.Done;

}

/// <summary>
/// An item to put in an attic, with the subtype of the blob it goes in.
/// </summary>
/// <param name="Item">The item to store; its id is replaced by the next free one.</param>
/// <param name="SubType">The subtype of the blob the item goes in.</param>
internal readonly record struct AtticAddition(AtticItem Item, string SubType);

/// <summary>
/// The result of putting several items in an attic at once.
/// </summary>
/// <param name="Result">How the change ended; the other values are only set for <see cref="HouseEdit.Done"/> and <see cref="HouseEdit.NeedsResync"/>.</param>
/// <param name="Changes">One change per item in the order of the additions: its blob, slot, GID id, the version of its patch and the item.</param>
/// <param name="Renumbered">The subtypes of the blobs that were numbered again; the client needs them before any patch.</param>
internal readonly record struct AtticBatch(HouseEdit Result, IReadOnlyList<AtticChange> Changes, IReadOnlyList<string> Renumbered) {

    internal bool Done => Result == HouseEdit.Done;

}

/// <summary>
/// A consistent copy of one blob of an attic.
/// </summary>
/// <param name="SubType">The blob's subtype.</param>
/// <param name="Revision">The blob version.</param>
/// <param name="GidBase">The base the blob entries are numbered from.</param>
/// <param name="LiveCount">The number of items in the blob, not counting removed slots.</param>
/// <param name="Slots">The items in slot order; a removed slot is <c>null</c>.</param>
internal readonly record struct AtticSnapshot(string SubType, uint Revision, uint GidBase, int LiveCount, IReadOnlyList<AtticItem> Slots);

/// <summary>
/// What the audit at the creation of a state found.
/// </summary>
/// <param name="Audited">The number of items checked.</param>
/// <param name="Removed">The number of items dropped because their template is gone.</param>
internal readonly record struct AtticAudit(int Audited, int Removed);

/// <summary>
/// The live, persisted state of one character's attic.
/// </summary>
internal sealed class AtticState {

    private readonly Lock _lock = new();
    private readonly Attic _attic;
    private readonly BlobSet<AtticItem> _tables = new(item => item.Id);

    private AtticState(Attic attic) => _attic = attic;

    internal ViewerCount Viewers { get; } = new();

    internal ulong CharId => _attic.CharId;

    internal ulong ContainerGid => _attic.ContainerGid;

    internal int LiveCount {
        get {
            lock (_lock) {
                return _attic.Items.Count;
            }
        }
    }

    internal int Limit => HousingSettings.AtticLimit;

    internal bool IsRetired => Viewers.IsRetired;

    /// <summary>
    /// Creates the live state of a stored attic. Items whose template is gone are dropped, the rest are numbered by their ids, every blob gets a new base,
    /// and the revisions move once and are stored, so a client with an older cache fetches the blobs again.
    /// </summary>
    /// <param name="attic">The stored attic; the state owns it from now on.</param>
    /// <param name="state">The live state when this returns <c>true</c>.</param>
    /// <param name="audit">What was checked and dropped.</param>
    /// <returns><c>false</c> when the new revisions could not be stored.</returns>
    internal static bool TryCreate(Attic attic, out AtticState state, out AtticAudit audit) {
        state = null;
        var previousItems = attic.Items;
        var previousRevisions = attic.Revisions;
        var kept = previousItems.Where(item => SubTypeOf(item) is not null).OrderBy(item => item.Id).Select(item => WithCustom(attic.CharId, item)).ToList();
        audit = new AtticAudit(previousItems.Count, previousItems.Count - kept.Count);

        var revisions = new Dictionary<string, uint>(previousRevisions);
        foreach (var subType in revisions.Keys.Concat(kept.Select(SubTypeOf)).Distinct().ToList()) {
            revisions[subType] = revisions.GetValueOrDefault(subType) + 1;
        }

        attic.Items = kept;
        attic.Revisions = revisions;
        if (!AtticCollection.Save(attic)) {
            attic.Items = previousItems;
            attic.Revisions = previousRevisions;

            return false;
        }

        state = new AtticState(attic);
        foreach (var group in kept.GroupBy(SubTypeOf)) {
            state._tables.For(group.Key).Slots.AddRange(group);
        }

        return true;
    }

    /// <summary>
    /// Copies one blob of the attic for building it.
    /// </summary>
    /// <param name="subType">The blob's subtype.</param>
    /// <returns>The revision, base, live count and slots, taken together.</returns>
    internal AtticSnapshot Snapshot(string subType) {
        lock (_lock) {
            var table = _tables.For(subType);

            return new AtticSnapshot(subType, Persistence.RevisionOf(_attic.Revisions, subType), table.GidBase, table.Live, [.. table.Slots]);
        }
    }

    /// <summary>
    /// Lists the subtypes of the blobs the client can be sent: every subtype that holds an item or has been sent before.
    /// </summary>
    /// <returns>The subtype names.</returns>
    internal IReadOnlyList<string> SubTypes() {
        lock (_lock) {
            return [.. _tables.SubTypesWithItems().Concat(_attic.Revisions.Keys).Distinct()];
        }
    }

    /// <summary>
    /// Finds the item an attic object GID names. The client builds the GID as
    /// <c>(u16 &lt;&lt; 48) | (9 &lt;&lt; 40) | GIDID</c>.
    /// </summary>
    /// <param name="gid">The GID the client sent.</param>
    /// <param name="item">The item in the slot; <c>null</c> when the slot was removed.</param>
    /// <param name="subType">The subtype of the blob the GID is in.</param>
    /// <returns><c>true</c> when the GID is inside the numbering of one of the attic's blobs.</returns>
    internal bool TryFindByGid(ulong gid, out AtticItem item, out string subType) {
        lock (_lock) {
            return _tables.TryFindByGid(gid, out item, out subType);
        }
    }

    /// <summary>
    /// Puts an item in the blob of its subtype, in a new slot at the end. When the blob's table is full of removed slots it
    /// is numbered again instead and nothing is added.
    /// </summary>
    /// <param name="item">The item to store; its id is replaced by the next free one.</param>
    /// <param name="subType">The subtype of the blob the item goes in.</param>
    /// <param name="save">Writes the attic; <c>null</c> writes the attic alone.</param>
    /// <returns>
    /// <see cref="HouseEdit.LimitReached"/> at the limit, <see cref="HouseEdit.NeedsResync"/> when the table was numbered again
    /// (the client needs the blob), <see cref="HouseEdit.SaveFailed"/> when the write failed.
    /// </returns>
    internal AtticChange TryAdd(AtticItem item, string subType, Func<Attic, bool> save = null) {
        lock (_lock) {
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return new AtticChange(HouseEdit.NotFound);
            }

            if (_attic.Items.Count >= Limit) {
                return new AtticChange(HouseEdit.LimitReached);
            }

            var table = _tables.For(subType);
            if (!table.Fits(item.Custom, stored => stored.Custom)) {
                return new AtticChange(MakeRoom(subType, table, item.Custom));
            }

            var added = item with { Id = _attic.NextItemId };
            Action undoRevision = null;
            uint revision = 0;
            var saved = Persist(
                () => {
                    _attic.Items.Add(added);
                    _attic.NextItemId = added.Id + 1;
                    undoRevision = Persistence.BumpRevision(_attic.Revisions, subType, out revision);
                },
                () => {
                    _attic.Items.RemoveAt(_attic.Items.Count - 1);
                    _attic.NextItemId = added.Id;
                    undoRevision();
                },
                save);
            if (!saved) {
                return new AtticChange(HouseEdit.SaveFailed);
            }

            table.Slots.Add(added);
            var slot = table.Slots.Count - 1;

            return new AtticChange(HouseEdit.Done, subType, slot, table.GidIdOf(slot), revision, added);
        }
    }

    /// <summary>
    /// Puts several items in their blobs in one write. Each blob's revision moves by the number of items put in it, so a
    /// client can be sent one patch per item with consecutive versions. A blob whose table has no room for its items but
    /// has removed slots is numbered again instead, and nothing is added.
    /// </summary>
    /// <param name="additions">The items to store and their blobs' subtypes.</param>
    /// <param name="save">Writes the attic; <c>null</c> writes the attic alone.</param>
    /// <returns>
    /// <see cref="HouseEdit.NotFound"/> when there is nothing to add, <see cref="HouseEdit.LimitReached"/> when the attic or a blob
    /// cannot hold the items, <see cref="HouseEdit.NeedsResync"/> when blobs were numbered again, <see cref="HouseEdit.SaveFailed"/>
    /// when the write failed; nothing is added then.
    /// </returns>
    internal AtticBatch TryAddMany(IReadOnlyList<AtticAddition> additions, Func<Attic, bool> save = null) {
        lock (_lock) {
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return new AtticBatch(HouseEdit.NotFound, [], []);
            }

            if (additions.Count == 0) {
                return new AtticBatch(HouseEdit.NotFound, [], []);
            }

            if (_attic.Items.Count + additions.Count > Limit) {
                return new AtticBatch(HouseEdit.LimitReached, [], []);
            }

            var crowded = additions.GroupBy(addition => addition.SubType)
                .Select(group => (Table: _tables.For(group.Key), group.Key, Count: group.Count()))
                .Where(blob => blob.Table.Slots.Count + blob.Count > HousingGids.MAX_SLOTS)
                .ToList();
            if (crowded.Any(blob => blob.Table.Live + blob.Count > HousingGids.MAX_SLOTS)) {
                return new AtticBatch(HouseEdit.LimitReached, [], []);
            }

            if (crowded.Count > 0) {
                return RenumberCrowded(crowded);
            }

            return AddAll(additions, save);
        }
    }

    /// <summary>
    /// Removes an item. Its slot stays in its blob's table as a null slot.
    /// </summary>
    /// <param name="itemId">The id of the item to remove.</param>
    /// <param name="save">Writes the attic; <c>null</c> writes the attic alone.</param>
    /// <returns><see cref="HouseEdit.NotFound"/> when the attic has no such item, <see cref="HouseEdit.SaveFailed"/> when the write failed.</returns>
    internal AtticChange TryRemove(uint itemId, Func<Attic, bool> save = null) {
        lock (_lock) {
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return new AtticChange(HouseEdit.NotFound);
            }

            if (_tables.Locate(itemId) is not { } found) {
                return new AtticChange(HouseEdit.NotFound);
            }

            var previous = found.Table.Slots[found.Slot];
            var index = _attic.Items.FindIndex(candidate => candidate.Id == itemId);
            Action undoRevision = null;
            uint revision = 0;
            var saved = Persist(
                () => {
                    _attic.Items.RemoveAt(index);
                    undoRevision = Persistence.BumpRevision(_attic.Revisions, found.SubType, out revision);
                },
                () => {
                    _attic.Items.Insert(index, previous);
                    undoRevision();
                },
                save);
            if (!saved) {
                return new AtticChange(HouseEdit.SaveFailed);
            }

            found.Table.Slots[found.Slot] = null;

            return new AtticChange(HouseEdit.Done, found.SubType, found.Slot, found.Table.GidIdOf(found.Slot), revision, previous);
        }
    }

    private static string SubTypeOf(AtticItem item)
        => CoreObjectFactory.GetCoreTemplate(item.TemplateId) is WizItemTemplate ? HousingItems.ATTIC_SUB_TYPE : null;

    private static AtticItem WithCustom(ulong charId, AtticItem item) {
        var kind = HousingItems.SubTypeOf(CoreObjectFactory.GetCoreTemplate(item.TemplateId) as WizItemTemplate);
        if (item.Custom is not null || kind == HousingItems.ITEM_SUB_TYPE) {
            return item;
        }

        return item with { Custom = HousingItems.LoadCustomData(charId, kind, item.ItemGid) };
    }

    private AtticBatch AddAll(IReadOnlyList<AtticAddition> additions, Func<Attic, bool> save) {
        var previousItems = _attic.Items;
        var previousRevisions = _attic.Revisions;
        var previousNextItemId = _attic.NextItemId;
        var revisions = new Dictionary<string, uint>(previousRevisions);
        var items = new List<AtticItem>(previousItems);
        var slotCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var changes = new List<AtticChange>(additions.Count);
        var nextItemId = previousNextItemId;
        foreach (var (item, subType) in additions) {
            var table = _tables.For(subType);
            var slot = slotCounts.GetValueOrDefault(subType, table.Slots.Count);
            slotCounts[subType] = slot + 1;
            var revision = Persistence.RevisionOf(revisions, subType) + 1;
            revisions[subType] = revision;
            var added = item with { Id = nextItemId++ };
            items.Add(added);
            changes.Add(new AtticChange(HouseEdit.Done, subType, slot, table.GidIdOf(slot), revision, added));
        }

        var saved = Persist(
            () => {
                _attic.Items = items;
                _attic.Revisions = revisions;
                _attic.NextItemId = nextItemId;
            },
            () => {
                _attic.Items = previousItems;
                _attic.Revisions = previousRevisions;
                _attic.NextItemId = previousNextItemId;
            },
            save);
        if (!saved) {
            return new AtticBatch(HouseEdit.SaveFailed, [], []);
        }

        foreach (var change in changes) {
            _tables.For(change.SubType).Slots.Add(change.Item);
        }

        return new AtticBatch(HouseEdit.Done, changes, []);
    }

    private AtticBatch RenumberCrowded(IEnumerable<(BlobTable<AtticItem> Table, string Key, int Count)> crowded) {
        var renumbered = new List<string>();
        foreach (var blob in crowded) {
            if (!Renumber(blob.Key, blob.Table)) {
                break;
            }

            renumbered.Add(blob.Key);
        }

        // A blob that was numbered again before a write failed still needs its client to fetch it.
        return new AtticBatch(renumbered.Count == 0 ? HouseEdit.SaveFailed : HouseEdit.NeedsResync, [], renumbered);
    }

    private HouseEdit MakeRoom(string subType, BlobTable<AtticItem> table, byte[] custom) {
        if (table.Live >= table.Slots.Count || !table.Fits(custom, stored => stored.Custom, compacted: true)) {
            return HouseEdit.LimitReached;
        }

        return Renumber(subType, table) ? HouseEdit.NeedsResync : HouseEdit.SaveFailed;
    }

    private bool Renumber(string subType, BlobTable<AtticItem> table) {
        Action undo = null;

        return Persist(() => undo = _tables.Renumber(subType, table, _attic.Revisions), () => undo(), null);
    }

    private bool Persist(Action apply, Action undo, Func<Attic, bool> save)
        => Persistence.Persist(apply, undo, () => (save ?? AtticCollection.Save)(_attic), $"attic of character {_attic.CharId}");

}
