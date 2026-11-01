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
 * The rating of a player at the moment they queued for a ranked match. The director carries it with the queue entry so it never reads a wizard.
 * 
 * USAGE EXAMPLE:
 * var ranked = new PvPRatingSnapshot(record.Elo, record.RankIndex, record.Wins, record.Losses, kFactor);
 * 
 * NOTE:
 * 
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP.Ranking;

internal sealed record PvPRatingSnapshot(int Elo, int RankIndex, int Wins, int Losses, int KFactor) {

    public static bool TryCreate(Wizard wizard, uint leagueId, uint seasonId, DateTime utcNow, out PvPRatingSnapshot snapshot) {
        snapshot = null;
        if (!PvPLeagueFactory.TryGetLeague(leagueId, out var league)
            || !PvPLeagueFactory.TryGetSeason(seasonId, out var season)
            || !PvPLeagueFactory.TryGetRankTable(league.m_leagueType, out var table)
            || !table.IsRated) {
            return false;
        }

        var startTime = PvPSeasonClock.GetStartUnix(season, utcNow);
        var record = PvPEloRecords.GetOrCreate(wizard.PvPEloBehavior, leagueId, seasonId, startTime, table, out var created);
        if (created) {
            WizardCollection.UpdateCharacterPvPEloBehavior(wizard);
        }

        var kFactor = table.GetKFactor(record.RankIndex);
        snapshot = new PvPRatingSnapshot(record.Elo, record.RankIndex, record.Wins, record.Losses, kFactor);

        return true;
    }

}
