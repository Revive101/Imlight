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
 * Reads and changes the rating records of a character: creates the record of a league and season on first use, applies the result of a rated match and the admin changes.
 * 
 * USAGE EXAMPLE:
 * var record = PvPEloRecords.GetOrCreate(wizard.PvPEloBehavior, leagueId, seasonId, startTime, table, out var created);
 * 
 * NOTE:
 * A new record starts at the start rating, or at the carry-over rating of the rank the previous record of the league ended on. A record of the same league and another season is ended when a record of a new season is created. The rank index is always derived from the rating through the rank table.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using System.Linq;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP.Ranking;

internal static class PvPEloRecords {

    internal sealed record Change(int OldElo, int NewElo, int OldRankIndex, int NewRankIndex);

    public static PvPEloRecord FindActive(ServerPvPEloBehavior behavior, uint leagueId, uint seasonId)
        => behavior.Records.FirstOrDefault(record => record.LeagueId == leagueId && record.SeasonId == seasonId && !record.Ended);

    public static PvPEloRecord GetOrCreate(ServerPvPEloBehavior behavior, uint leagueId, uint seasonId, uint startTime, PvPRankTable table, out bool created) {
        var active = FindActive(behavior, leagueId, seasonId);
        created = active is null;
        if (active is not null) {
            return active;
        }

        foreach (var other in behavior.Records.Where(record => record.LeagueId == leagueId && !record.Ended)) {
            other.Ended = true;
        }

        return Create(behavior, leagueId, seasonId, startTime, table);
    }

    public static PvPEloRecord StartNewSeason(ServerPvPEloBehavior behavior, uint leagueId, uint seasonId, uint startTime, PvPRankTable table) {
        foreach (var record in behavior.Records.Where(record => record.LeagueId == leagueId && !record.Ended)) {
            record.Ended = true;
        }

        return Create(behavior, leagueId, seasonId, startTime, table);
    }

    public static void Clear(ServerPvPEloBehavior behavior, uint leagueId)
        => behavior.Records.RemoveAll(record => record.LeagueId == leagueId);

    public static Change Apply(PvPEloRecord record, PvPRankTable table, double score, double opponentElo) {
        var kFactor = table.GetKFactor(record.RankIndex);
        var delta = PvPEloCalculator.GetDelta(record.Elo, opponentElo, score, kFactor, PvPEloCalculator.ELO_SCALE);
        var change = Move(record, table, PvPEloCalculator.GetNewRating(record.Elo, delta, table.MinElo));

        if (score >= PvPEloCalculator.WIN) {
            record.Wins++;
        } else if (score <= PvPEloCalculator.LOSS) {
            record.Losses++;
        } else {
            record.Draws++;
        }

        return change;
    }

    public static Change SetRating(PvPEloRecord record, PvPRankTable table, int rating)
        => Move(record, table, Math.Max(table.MinElo, rating));

    public static Change SetRank(PvPEloRecord record, PvPRankTable table, int rankIndex) {
        var index = Math.Clamp(rankIndex, 0, Math.Max(0, table.RankCount - 1));
        var rating = index == 0 ? table.StartingElo : table.GetRank(index).Down;

        return Move(record, table, Math.Max(table.MinElo, rating));
    }

    private static PvPEloRecord Create(ServerPvPEloBehavior behavior, uint leagueId, uint seasonId, uint startTime, PvPRankTable table) {
        var previous = behavior.Records
            .Where(record => record.LeagueId == leagueId && record.Ended)
            .OrderByDescending(record => record.RecordId)
            .FirstOrDefault();
        var rating = previous is null ? table.StartingElo : table.GetCarryover(previous.RankIndex);
        rating = Math.Max(table.MinElo, rating);

        var record = new PvPEloRecord {
            RecordId = behavior.NextRecordId++,
            LeagueId = leagueId,
            SeasonId = seasonId,
            StartTime = startTime,
            Elo = rating,
            RankIndex = table.GetRankIndex(rating, previous?.RankIndex ?? 0)
        };
        behavior.Records.Add(record);

        return record;
    }

    private static Change Move(PvPEloRecord record, PvPRankTable table, int rating) {
        var change = new Change(record.Elo, rating, record.RankIndex, table.GetRankIndex(rating, record.RankIndex));
        record.Elo = change.NewElo;
        record.RankIndex = change.NewRankIndex;

        return change;
    }

}
