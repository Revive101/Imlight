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
 * What a PvP window lists: a tournament in a league and season. Practice tournaments of one league and season share
 * one list, so a hosted practice match shows in every such window whatever practice tournament it opened with.
 * 
 * USAGE EXAMPLE:
 * var hosted = PvPRegistry.GetHostedMatches(new PvPListScope(tournamentNameId, leagueId, seasonId));
 * 
 * NOTE:
 * Ranked matches cannot be hosted, so a ranked scope only ever matches its own tournament.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/20/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.PvP.Data;

namespace Imlight.CoreLib.Game.PvP;

internal readonly record struct PvPListScope(uint TournamentNameId, uint LeagueId, uint SeasonId) {

    public bool IsPractice
        => PvPTournamentFactory.TryGetTournament(TournamentNameId, out var tournament) && PvPTournamentFactory.IsPractice(tournament);

    public string Kind => IsPractice ? "practice" : "ranked";

    public bool SameList(PvPListScope other) {
        if (LeagueId != other.LeagueId || SeasonId != other.SeasonId) {
            return false;
        }

        return TournamentNameId == other.TournamentNameId || (IsPractice && other.IsPractice);
    }

    public bool Includes(PvPMatchInfo info)
        => SameList(new PvPListScope(info.m_tournamentNameID, info.m_leagueID, info.m_seasonID));

}
