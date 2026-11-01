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
 * The level scaling data of all schools, looked up by school and level.
 * 
 * USAGE EXAMPLE:
 * if (table.TryGetRow("Fire", 55, out var row)) { }
 * 
 * NOTE:
 * The rows of a school sit on a level grid. A level is rounded down to the closest row at or below it, so a level above the last row uses the last row and a level below the first row has none.
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

namespace Imlight.CoreLib.Game.PvP.Rules;

internal sealed class LevelScaledTable {

    private readonly Dictionary<string, List<LevelScaledRow>> _rowsBySchool = new(StringComparer.OrdinalIgnoreCase);

    public LevelScaledRow FloorRow { get; }
    public int RowCount { get; }

    public LevelScaledTable(IEnumerable<LevelScaledRow> rows, LevelScaledRow floorRow) {
        foreach (var group in rows.GroupBy(row => row.School, StringComparer.OrdinalIgnoreCase)) {
            _rowsBySchool[group.Key] = [.. group.OrderBy(row => row.Level)];
            RowCount += _rowsBySchool[group.Key].Count;
        }

        FloorRow = floorRow;
    }

    public bool TryGetRow(string school, int level, out LevelScaledRow row) {
        row = null;
        if (school is null || !_rowsBySchool.TryGetValue(school, out var rows)) {
            return false;
        }

        row = rows.FindLast(candidate => candidate.Level <= level);

        return row is not null;
    }

}
