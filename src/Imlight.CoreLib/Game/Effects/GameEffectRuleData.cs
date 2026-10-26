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
 * GAME EFFECT RULE DATA SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Loads and manages wizard stat tables from XML files in the game resources,
 * providing access to numerical data used for calculating game effect values
 * as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * WizardStatTable statTable = GameEffectRuleData.GetWizardStatTable("DamageBonus");
 * 
 * NOTE:
 * Each table contains numerical values referenced by canonical effect lookups.
 *
 * TODO:
 * 
 * Created by: Joji, Jooty
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Effects;

internal class GameEffectRuleData : RootDirectoryResourceSingleton<GameEffectRuleData>, IMemoryStreamDisposable {

    protected override string DirectoryName => "GameEffectRuleData";
    private static readonly Dictionary<string, WizardStatTable> s_statTables = [];

    protected override void AfterLoad() {
        foreach (var (file, table) in ReadTemplates<WizardStatTable>()) {
            s_statTables.Add(SanitizeTableName(file.FileName), table);
        }

        Logger.Information("Loaded {Count} stat tables.", Logger.Args(s_statTables.Count));
    }

    /// <summary>
    /// Retrieves the WizardStatTable with the specified table name.
    /// </summary>
    /// <param name="tableName">The name of the table to retrieve.</param>
    /// <returns>The WizardStatTable with the specified table name, or null if the table is not found.</returns>
    internal static WizardStatTable GetWizardStatTable(string tableName)
        => s_statTables.GetValueOrDefault(tableName);

    public void DisposeStream()
        => s_statTables.Clear();

    private static string SanitizeTableName(string tableName) {
        var pathSplits = tableName.Split('/')[^1];
        var sanitized = pathSplits.Replace(".xml", "");

        return sanitized;
    }

}
