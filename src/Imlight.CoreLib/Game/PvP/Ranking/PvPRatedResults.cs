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
 * Applies the result of a ranked match to the rating records of a character, for a character who is online or not.
 * 
 * USAGE EXAMPLE:
 * var change = PvPRatedResults.Apply(wizard.PvPEloBehavior, leagueId, seasonId, opponentElo, score, out var table);
 * 
 * NOTE:
 * ApplyOffline is for a player who left before the match was decided: it changes the stored character and awards the rank badges without telling a client, which gets the full badge list at the next login.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using Imlight.Common;
using Imlight.CoreLib.Game.Badges;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP.Ranking;

internal static class PvPRatedResults {

    public static PvPEloRecords.Change Apply(ServerPvPEloBehavior behavior, uint leagueId, uint seasonId, double opponentElo, double score, out PvPRankTable table) {
        table = null;
        if (!PvPLeagueFactory.TryGetLeague(leagueId, out var league)
            || !PvPLeagueFactory.TryGetSeason(seasonId, out var season)
            || !PvPLeagueFactory.TryGetRankTable(league.m_leagueType, out table)) {
            return null;
        }

        var startTime = PvPSeasonClock.GetStartUnix(season, DateTime.UtcNow);
        var record = PvPEloRecords.GetOrCreate(behavior, leagueId, seasonId, startTime, table, out _);

        return PvPEloRecords.Apply(record, table, score, opponentElo);
    }

    public static void ApplyOffline(ulong charId, PVP_111_PROTOCOL.MSG_RATEDRESULT result) {
        var saved = WizardCollection.UpdateCharacterWithSession(charId, (_, stored) => {
            var change = Apply(stored.PvPEloBehavior, result.LeagueId, result.SeasonId, result.OpponentElo, result.Score, out var table);
            if (change is not null) {
                AwardBadges(stored, table, change.NewRankIndex);
            }

            return change is not null;
        });

        if (!saved) {
            Logger.Warning("The rated result of match {MatchId} could not be applied to character {CharId}.", Logger.Args(result.MatchId, charId));
        }
    }

    private static void AwardBadges(Wizard stored, PvPRankTable table, int rankIndex) {
        var catalog = BadgeCatalog.Current;
        var ids = PvPRankBadges.GetAwardIds(table, rankIndex);
        if (ids.Count > 0) {
            new BadgeEngine(stored, catalog).Execute(engine => {
                foreach (var id in ids) {
                    engine.Award(id);
                }
            });
        }
    }

}
