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
 * - The slot tables, the revision moves and the write-or-undo step are shared with AtticState (BlobSet, Persistence); this
 *   class owns the house document, its item limit and its owner. Use HouseRegistry.TryEnter and HouseRegistry.Release to count viewers.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Action = System.Action;

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

    private const int DECORATOR_LIMIT = 50;

    private readonly Lock _lock = new();
    private readonly House _house;
    private readonly HouseTemplateInfo _template;
    private readonly BlobSet<PlacedItem> _tables = new(item => item.Id, HousingItems.LiveSubTypes);

    private HouseState(House house, HouseTemplateInfo template) {
        _house = house;
        _template = template;
    }

    internal ViewerCount Viewers { get; } = new();

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
    /// The number of items the house may hold: the client's own formula, the template's limit plus the bonus slots. The
    /// texture holder is stored with the items but is not one of them, so it adds a slot.
    /// </summary>
    internal int ItemLimit {
        get {
            lock (_lock) {
                return LimitLocked();
            }
        }
    }

    internal bool IsRetired => Viewers.IsRetired;

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
        var previousNextItemId = house.NextItemId;
        var ordered = previousItems.OrderBy(item => item.Id).Select(item => WithCustom(house.OwnerCharId, item)).ToList();
        AddTextureHolder(house, ordered);
        var revisions = new Dictionary<string, uint>(previousRevisions);
        foreach (var subType in HousingItems.LiveSubTypes) {
            revisions[subType] = revisions.GetValueOrDefault(subType) + 1;
        }

        house.Items = ordered;
        house.Revisions = revisions;
        if (!HouseCollection.Save(house)) {
            house.Items = previousItems;
            house.Revisions = previousRevisions;
            house.NextItemId = previousNextItemId;

            return false;
        }

        state = new HouseState(house, template);
        foreach (var group in ordered.GroupBy(SubTypeOf)) {
            state._tables.For(group.Key).Slots.AddRange(group);
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
            var table = _tables.For(subType);

            return new HouseSnapshot(subType, Persistence.RevisionOf(_house.Revisions, subType), table.GidBase, table.Live, [.. table.Slots]);
        }
    }

    /// <summary>
    /// Copies every item of the house, blob by blob, in slot order.
    /// </summary>
    /// <returns>The placed items, not counting removed slots.</returns>
    internal IReadOnlyList<PlacedItem> Items() {
        lock (_lock) {
            return _tables.AllItems();
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
        lock (_lock) {
            return _tables.TryFindByGid(gid, out item, out subType);
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
    /// Finds the first placed item of a template.
    /// </summary>
    /// <param name="templateId">The item template.</param>
    /// <returns>The item as it is stored; <c>null</c> when none is placed.</returns>
    internal PlacedItem FindByTemplate(uint templateId) {
        lock (_lock) {
            return _house.Items.FirstOrDefault(item => item.TemplateId == templateId);
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
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return new HouseChange(HouseEdit.NotFound);
            }

            if (_house.Items.Count >= LimitLocked()) {
                return new HouseChange(HouseEdit.LimitReached);
            }

            var table = _tables.For(subType);
            if (!table.Fits(item.Custom, placed => placed.Custom)) {
                return new HouseChange(MakeRoom(subType, table, item.Custom));
            }

            var added = item with { Id = _house.NextItemId };
            Action undoRevision = null;
            uint revision = 0;
            var saved = Persist(
                () => {
                    _house.Items.Add(added);
                    _house.NextItemId = added.Id + 1;
                    undoRevision = Persistence.BumpRevision(_house.Revisions, subType, out revision);
                },
                () => {
                    _house.Items.RemoveAt(_house.Items.Count - 1);
                    _house.NextItemId = added.Id;
                    undoRevision();
                },
                save);
            if (!saved) {
                return new HouseChange(HouseEdit.SaveFailed);
            }

            table.Slots.Add(added);
            var slot = table.Slots.Count - 1;

            return new HouseChange(HouseEdit.Done, subType, slot, table.GidIdOf(slot), revision, added);
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
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return new HouseChange(HouseEdit.NotFound);
            }

            if (_tables.Locate(itemId) is not { } found) {
                return new HouseChange(HouseEdit.NotFound);
            }

            var previous = found.Table.Slots[found.Slot];
            var index = _house.Items.FindIndex(candidate => candidate.Id == itemId);
            Action undoRevision = null;
            uint revision = 0;
            var saved = Persist(
                () => {
                    _house.Items.RemoveAt(index);
                    undoRevision = Persistence.BumpRevision(_house.Revisions, found.SubType, out revision);
                },
                () => {
                    _house.Items.Insert(index, previous);
                    undoRevision();
                },
                save);
            if (!saved) {
                return new HouseChange(HouseEdit.SaveFailed);
            }

            found.Table.Slots[found.Slot] = null;

            return new HouseChange(HouseEdit.Done, found.SubType, found.Slot, found.Table.GidIdOf(found.Slot), revision, previous);
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
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return HouseEdit.NotFound;
            }

            if (itemIds.Count == 0 || itemIds.Distinct().Count() != itemIds.Count) {
                return HouseEdit.NotFound;
            }

            var located = itemIds.Select(_tables.Locate).ToList();
            if (located.Any(found => found is null)) {
                return HouseEdit.NotFound;
            }

            var previousItems = _house.Items;
            var previousRevisions = _house.Revisions;
            var revisions = new Dictionary<string, uint>(previousRevisions);
            var changes = new List<HouseChange>(located.Count);
            foreach (var found in located.Select(candidate => candidate.Value)) {
                var revision = Persistence.RevisionOf(revisions, found.SubType) + 1;
                revisions[found.SubType] = revision;
                changes.Add(new HouseChange(HouseEdit.Done, found.SubType, found.Slot, found.Table.GidIdOf(found.Slot), revision, found.Table.Slots[found.Slot]));
            }

            var removedIds = itemIds.ToHashSet();
            var saved = Persist(
                () => {
                    _house.Items = [.. previousItems.Where(candidate => !removedIds.Contains(candidate.Id))];
                    _house.Revisions = revisions;
                },
                () => {
                    _house.Items = previousItems;
                    _house.Revisions = previousRevisions;
                },
                save);
            if (!saved) {
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
    /// <param name="save">Writes the house; <c>null</c> writes the house alone.</param>
    /// <returns>
    /// <see cref="HouseEdit.NotFound"/> when the house has no such item or <paramref name="update"/> returns <c>null</c>
    /// or an item of another id or template, <see cref="HouseEdit.SaveFailed"/> when the write failed.
    /// </returns>
    internal HouseChange TryUpdate(uint itemId, Func<PlacedItem, PlacedItem> update, Func<House, bool> save = null) {
        lock (_lock) {
            // A retired state is gone from the registry, and a write to it would be lost.
            if (IsRetired) {
                return new HouseChange(HouseEdit.NotFound);
            }

            if (_tables.Locate(itemId) is not { } found) {
                return new HouseChange(HouseEdit.NotFound);
            }

            var previous = found.Table.Slots[found.Slot];
            var replacement = update(previous);
            if (replacement is null || ReferenceEquals(replacement, previous)
                || replacement.Id != previous.Id || replacement.TemplateId != previous.TemplateId) {
                return new HouseChange(HouseEdit.NotFound);
            }

            var index = _house.Items.FindIndex(candidate => candidate.Id == itemId);
            Action undoRevision = null;
            uint revision = 0;
            var saved = Persist(
                () => {
                    _house.Items[index] = replacement;
                    undoRevision = Persistence.BumpRevision(_house.Revisions, found.SubType, out revision);
                },
                () => {
                    _house.Items[index] = previous;
                    undoRevision();
                },
                save);
            if (!saved) {
                return new HouseChange(HouseEdit.SaveFailed);
            }

            found.Table.Slots[found.Slot] = replacement;

            return new HouseChange(HouseEdit.Done, found.SubType, found.Slot, found.Table.GidIdOf(found.Slot), revision, replacement);
        }
    }

    /// <summary>
    /// Checks whether a character is one of the house's decorators.
    /// </summary>
    /// <param name="charId">The character to look for.</param>
    /// <returns><c>true</c> when the owner made the character a decorator.</returns>
    internal bool IsDecorator(ulong charId) {
        lock (_lock) {
            return _house.Decorators.Contains(charId);
        }
    }

    /// <summary>
    /// Copies the decorators of the house.
    /// </summary>
    /// <returns>The character ids of the decorators.</returns>
    internal IReadOnlyList<ulong> Decorators() {
        lock (_lock) {
            return [.. _house.Decorators];
        }
    }

    /// <summary>
    /// Makes characters decorators of the house with one write. The owner and those who already are one are left out.
    /// </summary>
    /// <param name="charIds">The characters to add.</param>
    /// <param name="added">The characters that were added when this returns <see cref="HouseEdit.Done"/>.</param>
    /// <returns>
    /// <see cref="HouseEdit.NotFound"/> when there is nobody to add, <see cref="HouseEdit.LimitReached"/> when the house
    /// would have more decorators than it may, <see cref="HouseEdit.SaveFailed"/> when the write failed.
    /// </returns>
    internal HouseEdit TryAddDecorators(IEnumerable<ulong> charIds, out IReadOnlyList<ulong> added) {
        added = [];
        lock (_lock) {
            if (IsRetired) {
                return HouseEdit.NotFound;
            }

            var fresh = charIds.Where(charId => charId != OwnerCharId && !_house.Decorators.Contains(charId)).Distinct().ToList();
            if (fresh.Count == 0) {
                return HouseEdit.NotFound;
            }

            if (_house.Decorators.Count + fresh.Count > DECORATOR_LIMIT) {
                return HouseEdit.LimitReached;
            }

            if (!ChangeDecorators([.. _house.Decorators, .. fresh])) {
                return HouseEdit.SaveFailed;
            }

            added = fresh;

            return HouseEdit.Done;
        }
    }

    /// <summary>
    /// Takes characters off the decorators of the house with one write.
    /// </summary>
    /// <param name="charIds">The characters to remove.</param>
    /// <param name="removed">The characters that were removed when this returns <see cref="HouseEdit.Done"/>.</param>
    /// <returns><see cref="HouseEdit.NotFound"/> when none of them is a decorator, <see cref="HouseEdit.SaveFailed"/> when the write failed.</returns>
    internal HouseEdit TryRemoveDecorators(IEnumerable<ulong> charIds, out IReadOnlyList<ulong> removed) {
        removed = [];
        lock (_lock) {
            if (IsRetired) {
                return HouseEdit.NotFound;
            }

            var leaving = charIds.Where(_house.Decorators.Contains).Distinct().ToList();
            if (leaving.Count == 0) {
                return HouseEdit.NotFound;
            }

            if (!ChangeDecorators([.. _house.Decorators.Where(charId => !leaving.Contains(charId))])) {
                return HouseEdit.SaveFailed;
            }

            removed = leaving;

            return HouseEdit.Done;
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

    private static void AddTextureHolder(House house, List<PlacedItem> ordered) {
        var holderTemplateId = HouseTextures.ProxyTemplateId;
        if (holderTemplateId == 0 || ordered.Any(item => item.TemplateId == holderTemplateId)) {
            return;
        }

        var holder = HouseTextures.NewProxy();
        if (holder is null) {
            return;
        }

        ordered.Add(holder with { Id = house.NextItemId });
        house.NextItemId++;
    }

    private int LimitLocked() {
        var holderTemplateId = HouseTextures.ProxyTemplateId;
        var holders = holderTemplateId == 0 ? 0 : _house.Items.Count(item => item.TemplateId == holderTemplateId);

        return _template.ItemLimit + _house.BonusSlots + holders;
    }

    private HouseEdit MakeRoom(string subType, BlobTable<PlacedItem> table, byte[] custom) {
        if (table.Live >= table.Slots.Count || !table.Fits(custom, placed => placed.Custom, compacted: true)) {
            return HouseEdit.LimitReached;
        }

        Action undo = null;
        var saved = Persist(() => undo = _tables.Renumber(subType, table, _house.Revisions), () => undo(), null);

        return saved ? HouseEdit.NeedsResync : HouseEdit.SaveFailed;
    }

    private bool ChangeDecorators(List<ulong> decorators) {
        var previous = _house.Decorators;

        return Persist(() => _house.Decorators = decorators, () => _house.Decorators = previous, null);
    }

    private bool Persist(Action apply, Action undo, Func<House, bool> save)
        => Persistence.Persist(apply, undo, () => (save ?? HouseCollection.Save)(_house), $"house {_house.LotGid}");

}
