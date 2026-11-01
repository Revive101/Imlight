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
 * One player of a started match, as the arena zone needs them to seat the player on a circle.
 * 
 * USAGE EXAMPLE:
 * new ZONE_102_PROTOCOL.MSG_STARTPVPMATCH { MatchId = id, Roster = [new PvPRosterEntry(charId, session, player, 0)] };
 * 
 * NOTE:
 * TeamIndex 0 is the blue side (circles 4-7 of the sigil), 1 the red side.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal sealed record PvPRosterEntry(ulong CharId, IActorRef Session, CoreObject PlayerObject, int TeamIndex);
