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
 * Works out which house a player walks into from a house: the door between a deed's exterior and its structure's
 * interior, and a trigger that teleports into another zone of the same residence.
 *
 * USAGE EXAMPLE:
 * if (HouseTravel.TryEnterByDoor(wizard, doorGid, out var entry, out var failure)) {
 *     // transfer the player to entry.Zone, lot entry.LotGid
 * }
 *
 * NOTE:
 * - A deed with a structure is two houses, the exterior (the deed) and the interior (the structure). The partner of one
 *   is the other, and the partner is always the same owner's: a visitor walks into the owner's other lot.
 * - A house without a structure that no deed names (a one-zone house) has no partner.
 * - Whoever owns the houses gets the partner's items and lot issued first, so the client treats them as the owner in
 *   the zone they walk into. The partner house is stored on the first walk through.
 * - A visitor must still be allowed in by the owner.
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
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Access;

/// <summary>
/// Why a player cannot walk into a house.
/// </summary>
internal enum HouseTravelFailure {
    None,
    NotInHouse,
    NoPartner,
    Unavailable,
    Denied,
    BackpackFull,
}

internal static class HouseTravel {

    /// <summary>
    /// Finds the house on the other side of a house's door.
    /// </summary>
    /// <param name="template">The template of the house the door is in.</param>
    /// <param name="ownerLots">The owner's lots by template.</param>
    /// <param name="partner">The partner house when this returns <c>true</c>.</param>
    /// <returns>
    /// <c>true</c> for a deed's exterior, whose partner is its structure, and for a structure the owner holds the deed
    /// of, whose partner is the deed. <c>false</c> for a house without a partner.
    /// </returns>
    internal static bool TryGetPartner(HouseTemplateInfo template, IReadOnlyDictionary<uint, ulong> ownerLots, out HouseTemplateInfo partner) {
        partner = default;
        if (template.HasStructure) {
            return HouseTemplates.TryGet(template.StructureTemplateId, out partner);
        }

        foreach (var templateId in ownerLots.Keys.Order()) {
            if (HouseTemplates.TryGet(templateId, out var candidate) && candidate.HasStructure && candidate.StructureTemplateId == template.TemplateId) {
                partner = candidate;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Prepares the walk through a door to the house on its other side.
    /// </summary>
    /// <param name="player">The player who walked up to the door; the house they are in decides the partner.</param>
    /// <param name="doorGid">The door object, which the client is asked to open the zone for.</param>
    /// <param name="entry">What the zone transfer needs when this returns <c>true</c>.</param>
    /// <param name="failure">Why the player cannot walk through when this returns <c>false</c>.</param>
    /// <returns><c>false</c> when the player is not in a house, the house has no partner, or the player may not enter it.</returns>
    internal static bool TryEnterByDoor(Wizard player, ulong doorGid, out HouseEntry entry, out HouseTravelFailure failure) {
        entry = null;
        if (!TryLoadCurrentHouse(player, out var owner, out var template, out failure)) {
            return false;
        }

        if (!TryGetPartner(template, owner.HouseLots, out var partner)) {
            failure = HouseTravelFailure.NoPartner;

            return false;
        }

        return TryOpen(player, owner, partner, (lotGid, newItems) => HouseEntry.ForDoor(lotGid, owner.CharId, partner, doorGid, newItems), out entry, out failure);
    }

    /// <summary>
    /// Prepares the teleport into a zone that is a house of the same owner.
    /// </summary>
    /// <param name="player">The player who is teleported. The house they are in names the owner; outside a house they are the owner.</param>
    /// <param name="zone">The house zone the teleport leads to.</param>
    /// <param name="entry">What the zone transfer needs when this returns <c>true</c>.</param>
    /// <param name="failure">Why the player cannot be teleported when this returns <c>false</c>.</param>
    /// <returns><c>false</c> when the owner has no house in the zone, or the player may not enter it.</returns>
    internal static bool TryEnterZone(Wizard player, string zone, out HouseEntry entry, out HouseTravelFailure failure) {
        entry = null;
        var owner = player;
        if (player.AttachedLotGid != 0 && !TryLoadCurrentHouse(player, out owner, out _, out failure)) {
            return false;
        }

        if (!HouseLots.TryFindInZone(owner, zone, out var target, out _)) {
            failure = HouseTravelFailure.NoPartner;

            return false;
        }

        return TryOpen(player, owner, target, (lotGid, newItems) => HouseEntry.ForTeleport(lotGid, owner.CharId, target, newItems), out entry, out failure);
    }

    private static bool TryLoadCurrentHouse(Wizard player, out Wizard owner, out HouseTemplateInfo template, out HouseTravelFailure failure) {
        owner = null;
        template = default;
        failure = HouseTravelFailure.NotInHouse;
        if (player.AttachedLotGid == 0) {
            return false;
        }

        failure = HouseTravelFailure.Unavailable;
        if (!HouseLots.TryLoadAttached(player, out var current, out template)) {
            return false;
        }

        owner = current.OwnerCharId == player.CharId ? player : WizardCollection.GetCharacterUnloaded(current.OwnerCharId);
        if (owner is null) {
            Logger.Warning("The owner {OwnerId} of lot {LotGid} cannot be loaded.", Logger.Args(current.OwnerCharId, current.LotGid));

            return false;
        }

        failure = HouseTravelFailure.None;

        return true;
    }

    private static bool TryOpen(Wizard player, Wizard owner, HouseTemplateInfo target, Func<ulong, IReadOnlyList<ulong>, HouseEntry> createEntry,
                                out HouseEntry entry, out HouseTravelFailure failure) {
        entry = null;
        IReadOnlyList<ulong> newItems = [];
        if (owner.CharId == player.CharId) {
            // The owner is treated as the owner by the client when they hold the item of the lot they walk into.
            var grant = HouseDeeds.Issue(player, target.TemplateId);
            if (!grant.Succeeded) {
                failure = grant.Failure == HouseGrantFailure.BackpackFull ? HouseTravelFailure.BackpackFull : HouseTravelFailure.Unavailable;

                return false;
            }

            newItems = [.. grant.Created.Select(item => (ulong) item.m_globalID)];
        } else if (HouseAccess.Evaluate(player, owner.CharId) == HouseAccessLevel.Denied) {
            failure = HouseTravelFailure.Denied;

            return false;
        }

        var house = HouseRegistry.GetOrCreate(owner, target.TemplateId, out _);
        if (house is null) {
            failure = HouseTravelFailure.Unavailable;

            return false;
        }

        entry = createEntry(house.LotGid, newItems);
        failure = HouseTravelFailure.None;

        return true;
    }

}
