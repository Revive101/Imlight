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
 * Finds where a player goes when they leave a house zone: through a trigger that names no destination, with Go Home, or
 * when the owner sends them away (their client answers with Go Home).
 *
 * USAGE EXAMPLE:
 * if (HouseExits.TryResolve(zonePath, out var zone, out var location)) {
 *     // transfer the player to zone, at location
 * }
 *
 * NOTE:
 * - Every house zone's exit trigger has an empty ResTeleport; the game data keeps the destination elsewhere. A
 *   destination stored for the trigger (ZoneTransfer in SpiralDB) replaces the result before it runs, so this only
 *   answers for triggers without one.
 * - The destination is the hub of the house's world, from WorldHubZones. A world without a hub sends the player to the
 *   starting zone.
 *
 * TODO:
 * - Does the live server send a player out of a house to the hub of the world, or back to where they came from?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Houses;
using Imlight.CoreLib.Game.World;

namespace Imlight.CoreLib.Game.Housing.Access;

internal static class HouseExits {

    /// <summary>
    /// Finds the zone and location a player leaves a house zone to.
    /// </summary>
    /// <param name="houseZone">The house zone the player leaves.</param>
    /// <param name="zone">The destination zone when this returns <c>true</c>.</param>
    /// <param name="location">The destination location when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when <paramref name="houseZone"/> is not a house zone.</returns>
    internal static bool TryResolve(string houseZone, out string zone, out string location) {
        zone = null;
        location = null;
        if (!HouseZoneIndex.IsHouseZone(houseZone)) {
            return false;
        }

        _ = WorldHubZones.Instance;
        var hub = WorldHubZones.GetHubForZone(houseZone);
        if (hub is not null && !string.IsNullOrEmpty(hub.m_hubZone.ToString())) {
            zone = hub.m_hubZone.ToString();
            location = string.IsNullOrEmpty(hub.m_location.ToString()) ? HouseDeeds.ENTRY_LOCATION : hub.m_location.ToString();

            return true;
        }

        Logger.Warning("House zone {Zone} has no world hub, so players leave it to the starting zone.", Logger.Args(houseZone));
        zone = ConfigurationManager.Settings["Character.StartingZone"];
        location = HouseDeeds.ENTRY_LOCATION;

        return !string.IsNullOrEmpty(zone);
    }

}
