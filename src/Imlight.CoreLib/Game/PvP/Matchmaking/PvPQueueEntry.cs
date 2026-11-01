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
 * One queued player: what they asked to play and since when.
 * 
 * USAGE EXAMPLE:
 * new PvPQueueEntry(charId, realm, tournamentId, matchId, leagueId, seasonId, false, DateTime.UtcNow, rating, zone, zoneName, location);
 * 
 * NOTE:
 * IsHosted marks a player of a hosted match: they were never queued, so a cancelled match sends them idle instead
 * of back into the public queue.
 * The queue key is the realm, the tournament and the match: only players with equal keys can be paired.
 * The rating is the one of the match level table at the level the player had when they joined, or for a ranked entry the Elo of the player in the league. The origin is
 * the zone, its display name and the location the player queued from.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using Imlight.CoreLib.Game.PvP.Ranking;

namespace Imlight.CoreLib.Game.PvP.Matchmaking;

internal sealed record PvPQueueEntry(
    ulong CharId,
    string RealmName,
    uint TournamentNameId,
    uint MatchNameId,
    uint LeagueId,
    uint SeasonId,
    bool ExpandSearch,
    DateTime EnteredAtUtc,
    int Rating,
    string OriginZone,
    string OriginZoneDisplayName,
    string OriginLocation,
    bool IsHosted = false,
    PvPRatingSnapshot Ranked = null);
