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
 * Prepares a character's visit to the house another character stands in, for the friend and group "go to" requests.
 *
 * USAGE EXAMPLE:
 * if (HouseVisits.TryPrepare(visitor, host, out var entry, out var failure)) {
 *     Teleport(entry.Zone, houseEntry: entry);
 * }
 *
 * NOTE:
 * - The host's lot is the house they are in, which is not necessarily their own.
 * - The visitor gets an entry that asks the client to load the zone from the house template, because a visitor holds no
 *   deed item for it. The access check here only spares the player a long load; attaching checks it again.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Access;

/// <summary>
/// Why a character cannot visit the house another character is in.
/// </summary>
internal enum HouseVisitFailure {
    None,
    NotInHouse,
    Unavailable,
    Denied,
}

internal static class HouseVisits {

    /// <summary>
    /// Prepares the visit to the house the host is in.
    /// </summary>
    /// <param name="visitor">The character who goes to the host.</param>
    /// <param name="host">The character to go to.</param>
    /// <param name="entry">What the zone transfer needs when this returns <c>true</c>.</param>
    /// <param name="failure">Why the visitor cannot go when this returns <c>false</c>.</param>
    /// <returns><c>false</c> when the host is not in a house, or the house is gone or closed to the visitor.</returns>
    internal static bool TryPrepare(Wizard visitor, Wizard host, out HouseEntry entry, out HouseVisitFailure failure) {
        entry = null;
        failure = HouseVisitFailure.None;
        if (host.AttachedLotGid == 0) {
            failure = HouseVisitFailure.NotInHouse;

            return false;
        }

        if (!HouseLots.TryLoadAttached(host, out var house, out var template)) {
            failure = HouseVisitFailure.Unavailable;

            return false;
        }

        if (HouseAccess.Evaluate(visitor, house) == HouseAccessLevel.Denied) {
            failure = HouseVisitFailure.Denied;

            return false;
        }

        entry = HouseEntry.ForTeleport(house.LotGid, house.OwnerCharId, template);

        return true;
    }

}
