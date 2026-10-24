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
 * The rating table of one league: start and floor rating and the ranks with their rating bounds, K-factor, season carry-over rating and badge name. Built from the Elo config of the game data.
 * 
 * USAGE EXAMPLE:
 * var rank = table.GetRankIndex(rating, currentRankIndex);
 * 
 * NOTE:
 * Down and Up are the lowest and highest rating of a rank. A rating between the Up of one rank and the Down of the next is a gap: the player keeps the rank they hold if it is one of the two, otherwise the nearest one below or above.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed class PvPRankTable {

    internal sealed record Rank(int Index, int Down, int Up, int KFactor, int Carryover, string NameKey, string BadgeName);

    private readonly Rank[] _ranks;

    public PvPRankTable(string leagueType, int startingElo, int minElo, IEnumerable<Rank> ranks) {
        LeagueType = leagueType ?? string.Empty;
        StartingElo = startingElo;
        MinElo = minElo;
        _ranks = [.. ranks.OrderBy(rank => rank.Index)];
    }

    public string LeagueType { get; }
    public int StartingElo { get; }
    public int MinElo { get; }
    public IReadOnlyList<Rank> Ranks => _ranks;
    public int RankCount => _ranks.Length;
    public bool IsRated => _ranks.Any(rank => rank.KFactor > 0);

    public Rank GetRank(int index)
        => _ranks.Length == 0 ? null : _ranks[Math.Clamp(index, 0, _ranks.Length - 1)];

    public int GetKFactor(int index)
        => GetRank(index)?.KFactor ?? 0;

    public int GetCarryover(int index)
        => GetRank(index)?.Carryover ?? StartingElo;

    public string GetBadgeName(int index)
        => GetRank(index)?.BadgeName ?? string.Empty;

    public int GetRankIndex(int rating, int currentIndex) {
        if (_ranks.Length <= 1) {
            return 0;
        }

        var current = Math.Clamp(currentIndex, 0, _ranks.Length - 1);
        for (var index = _ranks.Length - 1; index >= 0; index--) {
            if (rating < _ranks[index].Down) {
                continue;
            }

            var isGap = index < _ranks.Length - 1 && rating > _ranks[index].Up;

            return isGap ? ResolveGap(index, current) : index;
        }

        return 0;
    }

    private static int ResolveGap(int lowerIndex, int current)
        => current == lowerIndex || current == lowerIndex + 1 ? current : current < lowerIndex ? lowerIndex : lowerIndex + 1;

}
