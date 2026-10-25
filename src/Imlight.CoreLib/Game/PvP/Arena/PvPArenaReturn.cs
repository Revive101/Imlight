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
 * PVP
 * ========================================================================
 *
 * PURPOSE:
 * Where a player goes back to when a match ends: the zone and spot they queued from.
 *
 * USAGE EXAMPLE:
 * var returnTo = PvPArenaReturn.For(queueEntry);
 *
 * NOTE:
 * The zone and spot come from the queue entry, which takes them from the player's presence when they queue.
 *
 * TODO:
 *
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imlight.CoreLib.Game.PvP.Matchmaking;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal sealed record PvPArenaReturn(string Zone, string ZoneDisplayName, string Location) {

    public static PvPArenaReturn For(PvPQueueEntry entry)
        => new(entry.OriginZone, entry.OriginZoneDisplayName, entry.OriginLocation);

}
