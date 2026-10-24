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
 * Finds the badges that reaching a rank awards. The rank table of the game data names one badge per rank; the badge id is the string hash of that name, like every badge id.
 * 
 * USAGE EXAMPLE:
 * var badgeIds = PvPRankBadges.GetAwardIds(table, record.RankIndex);
 * 
 * NOTE:
 * A badge name the badge catalog does not hold is skipped. LogResolution tells at boot how many of the names resolve.
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
using Imcodec.Cryptography;
using Imlight.Common;
using Imlight.CoreLib.Game.Badges;
using Imlight.CoreLib.Game.PvP.Data;

namespace Imlight.CoreLib.Game.PvP.Ranking;

internal static class PvPRankBadges {

    public static IReadOnlyList<uint> GetAwardIds(PvPRankTable table, int rankIndex) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            return [];
        }

        var last = Math.Min(rankIndex, table.RankCount - 1);

        return [.. Enumerable.Range(0, last + 1)
            .Select(index => table.GetBadgeName(index))
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => StringHash.Compute(name))
            .Where(id => catalog.TryGet(id, out _))
            .Distinct()];
    }

    public static void LogResolution() {
        var catalog = BadgeCatalog.Current;
        var tables = PvPLeagueFactory.GetRankTables().Append(PvPLeagueFactory.GetDefaultRankTable()).Where(table => table is not null);
        var names = tables
            .SelectMany(table => table.Ranks.Select(rank => rank.BadgeName))
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();
        var missing = names.Where(name => !catalog.TryGet(StringHash.Compute(name), out _)).ToList();

        Logger.Information("{Resolved} of {Total} PvP rank badge names resolve in the badge catalog.{Missing}",
            Logger.Args(names.Count - missing.Count, names.Count, missing.Count == 0 ? string.Empty : $" Missing: {string.Join(", ", missing)}."));
    }

}
