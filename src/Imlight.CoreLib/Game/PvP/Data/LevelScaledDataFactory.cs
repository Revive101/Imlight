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
 * Loads the level scaling data from the game data: the stat values of a wizard of each school at each level, and the floor share a weaker wizard keeps when scaled up.
 * 
 * USAGE EXAMPLE:
 * if (LevelScaledDataFactory.TryGetTable(out var table)) { }
 * 
 * NOTE:
 * The data is the one file of its class in the root of the archive. The rows are turned into plain rows once, because a row has over a hundred stat properties and the scaler reads them by name.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Rules;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed class LevelScaledDataFactory : RootDirectoryResourceSingleton<LevelScaledDataFactory> {

    private const string STAT_PROPERTY_PREFIX = "m_canonical";

    protected override string DirectoryName => nameof(LevelScaledData);

    private static readonly PropertyInfo[] s_statProperties = [.. typeof(LevelScaledInfo)
        .GetProperties()
        .Where(property => property.PropertyType == typeof(float) && property.Name.StartsWith(STAT_PROPERTY_PREFIX, System.StringComparison.Ordinal))];

    private static LevelScaledTable s_table;

    protected override void AfterLoad() {
        foreach (var file in base.Files) {
            if (!PvPResourceReader.TryRead<LevelScaledData>(file.Key, file.Value, out var data)) {
                continue;
            }

            var floorRow = data.m_statsFloorPercent is null ? null : CreateRow(data.m_statsFloorPercent);
            s_table = new LevelScaledTable((data.m_levelScaledInfoList ?? []).Select(CreateRow), floorRow);

            Logger.Information("Loaded {Count} level scaling rows.", Logger.Args(s_table.RowCount));

            return;
        }

        Logger.Error("The level scaling data is missing from the game data.");
    }

    internal static bool TryGetTable(out LevelScaledTable table) {
        table = s_table;

        return table is not null;
    }

    private static LevelScaledRow CreateRow(LevelScaledInfo info) {
        var values = new Dictionary<string, float>(s_statProperties.Length);
        foreach (var property in s_statProperties) {
            values[property.Name[STAT_PROPERTY_PREFIX.Length..]] = (float) property.GetValue(info);
        }

        string school = info.m_school;

        return new LevelScaledRow(school, info.m_level, values);
    }

}
