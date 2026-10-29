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
 * A player who was on the way to an arena when the match was cancelled. If they still arrive, the director
 * sends them back.
 *
 * USAGE EXAMPLE:
 * _strandedPlayers[charId] = new PvPStrandedPlayer(arenaZone, lobbySession, returnTo, expiresAtUtc);
 *
 * NOTE:
 * The entry only applies to a session other than the lobby session, in the arena zone, before it expires.
 *
 * TODO:
 *
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using Akka.Actor;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal sealed record PvPStrandedPlayer(string ArenaZone, IActorRef LobbySession, PvPArenaReturn ReturnTo, DateTime ExpiresAtUtc);
