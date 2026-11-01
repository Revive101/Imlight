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
 * The Elo update of a rated match: the expected score from two ratings and the rating change for a result.
 * 
 * USAGE EXAMPLE:
 * var delta = PvPEloCalculator.GetDelta(rating, opponentRating, PvPEloCalculator.WIN, kFactor, scale);
 * 
 * NOTE:
 * Standard Elo with a configurable scale. A team is rated against the average of the other team, and each player is updated with their own K-factor.
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

namespace Imlight.CoreLib.Game.PvP.Ranking;

internal static class PvPEloCalculator {

    public const double WIN = 1;
    public const double DRAW = 0.5;
    public const double LOSS = 0;
    public const double ELO_SCALE = 400;

    public static double GetExpectedScore(double rating, double opponentRating, double scale)
        => 1 / (1 + Math.Pow(10, (opponentRating - rating) / scale));

    public static int GetDelta(int rating, double opponentRating, double score, int kFactor, double scale) {
        if (kFactor <= 0 || scale <= 0) {
            return 0;
        }

        return (int) Math.Round(kFactor * (score - GetExpectedScore(rating, opponentRating, scale)), MidpointRounding.AwayFromZero);
    }

    public static int GetNewRating(int rating, int delta, int floor)
        => Math.Max(floor, rating + delta);

    public static double GetTeamAverage(IEnumerable<int> ratings) {
        var list = ratings.ToList();

        return list.Count == 0 ? 0 : list.Average();
    }

}
