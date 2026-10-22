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
 * Moves the objects of a house into its owner's attic: the client's "Pick Up All Items". The objects leave the house and
 * enter the attic in one write.
 *
 * USAGE EXAMPLE:
 * var result = HousePickUpAll.Run(house, attic, keptGids);
 * if (result.Edit == HouseEdit.Done) {
 *     // one delete patch per result.Removed, one add patch per result.Added
 * }
 *
 * NOTE:
 * - The objects the client names stay in the house. So does an object whose family has no attic blob, or that the server does
 *   not support in an attic, and so does every object past the room the attic has left; the result counts the objects that
 *   could have moved but did not.
 * - An object keeps the global id and the item document it was placed from, so it comes back as the same item, whether
 *   it is a pet or a chair.
 * - The house and the attic are locked in that order, and nothing takes them the other way round.
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
using Imlight.CoreLib.Game.Housing.Attics;
using Imlight.CoreLib.Game.Housing.Families;
using Imlight.CoreLib.Game.Housing.Items;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// How a pick up of every object ended.
/// </summary>
/// <param name="Edit">
/// <see cref="HouseEdit.Done"/> when the objects moved (or there were none to move), <see cref="HouseEdit.LimitReached"/> when the attic
/// could not take them, <see cref="HouseEdit.NeedsResync"/> when attic blobs were numbered again and the run needs repeating after the
/// client has them, <see cref="HouseEdit.NotFound"/> when an object left the house meanwhile, <see cref="HouseEdit.SaveFailed"/> when the write failed.
/// </param>
/// <param name="Removed">One change per object that left the house, in slot order: its slot, GID id, the version of its patch and the item.</param>
/// <param name="Added">One change per object that entered the attic, in the order of <paramref name="Removed"/>.</param>
/// <param name="Renumbered">The subtypes of the attic blobs that were numbered again.</param>
/// <param name="Left">The number of objects that could have moved but stayed because the attic is full.</param>
internal readonly record struct PickUpAllResult(
    HouseEdit Edit,
    IReadOnlyList<HouseChange> Removed,
    IReadOnlyList<AtticChange> Added,
    IReadOnlyList<string> Renumbered,
    int Left);

internal static class HousePickUpAll {

    /// <summary>
    /// Moves the objects of a house into the attic of its owner.
    /// </summary>
    /// <param name="house">The house.</param>
    /// <param name="attic">The attic of the house's owner.</param>
    /// <param name="keptGids">The object GIDs the client wants to leave in the house; a GID that is not an object of the house is ignored.</param>
    /// <returns>What moved and what the client needs to be told.</returns>
    internal static PickUpAllResult Run(HouseState house, AtticState attic, IEnumerable<ulong> keptGids) {
        var kept = new HashSet<uint>();
        foreach (var gid in keptGids) {
            if (house.TryFindByGid(gid, out var keptItem, out _) && keptItem is not null) {
                kept.Add(keptItem.Id);
            }
        }

        var movable = new List<(PlacedItem Item, HousingFamily Family)>();
        foreach (var item in house.Snapshot().Slots) {
            if (item is not null && !kept.Contains(item.Id)
                && HousingItems.Check(item.TemplateId, out _, out var family) == HousingPlacement.Allowed
                && family.AtticSubType is not null) {
                movable.Add((item, family));
            }
        }

        var chosen = movable.Take(Math.Max(0, attic.Limit - attic.LiveCount)).ToList();
        var left = movable.Count - chosen.Count;
        if (chosen.Count == 0) {
            return new PickUpAllResult(left > 0 ? HouseEdit.LimitReached : HouseEdit.Done, [], [], [], left);
        }

        var additions = chosen
            .Select(entry => new AtticAddition(
                new AtticItem {
                    ItemGid = entry.Item.ItemGid,
                    TemplateId = entry.Item.TemplateId,
                    Tint = entry.Item.Tint,
                    Custom = entry.Item.Custom,
                },
                entry.Family.AtticSubType))
            .ToList();

        var batchRan = false;
        var batch = new AtticBatch(HouseEdit.SaveFailed, [], []);
        var edit = house.TryRemoveMany([.. chosen.Select(entry => entry.Item.Id)], out var removed, houseDocument => {
            batchRan = true;
            batch = attic.TryAddMany(additions, atticDocument => HouseCollection.SaveWithAttic(houseDocument, atticDocument));

            return batch.Done;
        });
        if (edit == HouseEdit.Done) {
            return new PickUpAllResult(HouseEdit.Done, removed, batch.Changes, [], left);
        }

        if (edit == HouseEdit.SaveFailed && batchRan && batch.Result is HouseEdit.NeedsResync or HouseEdit.LimitReached) {
            return new PickUpAllResult(batch.Result, [], [], batch.Renumbered, 0);
        }

        return new PickUpAllResult(edit, [], [], [], 0);
    }

}
