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
 * CANONICAL STAT EFFECTS
 * ========================================================================
 * 
 * PURPOSE:
 * Loads and processes game effect templates from XML resources to calculate 
 * character statistic modifications based on effect names and lookup indices
 * as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * float statValue = CanonicalStatEffects.GetCanonicalStatValue(new StatisticEffectInfo() { 
 *     m_effectName = "Accuracy", 
 *     m_lookupIndex = 5 
 * });
 * 
 * NOTE:
 * Some stat effects are multiplied by 100 for proper scaling within the game.

 * Recall the docs @ https://revive101.github.io/Imlight-docs/internals/schemas.html#object-creation
 * Effects that are 'canonical' are effects that change numerical player stats, such as health or damage.
 * Canonical effects follow this schema. The template manifest for these effects is the CanonicalStatEffects.xml file.

 * The template:
 * Every template in the CanonicalStatEffects.xml file has a corresponding stat table.
 * These tables are located in the GameEffectRuleData directory in the Root.wad file.
 * The stat table name property (m_statTableName) in the template is the path of the table in the Root.wad file.

 * The information:
 * The effect information contains only two bits of information: the effect name and the lookup index.
 * The effect name is used to find the effect template in the CanonicalStatEffects.xml file.
 * The lookup index is used to find the stat value in the corresponding stat table.

 * Some stats are multiplied by 100.
 *
 * TODO:
 * 
 * Created by: Joji, Jooty
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Effects;

/// <summary>
/// Manages canonical stat effects that modify numerical player statistics by loading and processing 
/// effect templates.
/// </summary>
/// <remarks>
/// Effect data is loaded from "GameEffectData/CanonicalStatEffects.xml" and corresponding 
/// stat tables from GameEffectRuleData in the Root.wad file.
/// </remarks>
internal class CanonicalStatEffects : RootSingleResourceSingleton<CanonicalStatEffects>, IMemoryStreamDisposable {

    protected override string ResourceName => "GameEffectData/CanonicalStatEffects.xml";
    private static readonly string[] s_timesHundredEffectNames = [
        "Accuracy",
        "Damage",
        "Piercing",
        "ReduceDamage",
    ];

    private static GameEffectTemplateList s_effectTable;
    private static readonly ConcurrentDictionary<string, byte> s_warnedEffects = new();

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var streamAsBytes = Stream.ToArray();
        if (!serializer.Deserialize(streamAsBytes, out s_effectTable)) {
            throw new InvalidDataException($"The item stat effects {ResourceName} could not be read. Re-download the game resources.");
        }

        Logger.Information("Loaded {Count} canonical stat effects.",
            Logger.Args(s_effectTable.m_effectTemplates.Count));
    }

    /// <summary>
    /// Retrieves the canonical stat value based on the provided <see cref="StatisticEffectInfo"/>.
    /// </summary>
    /// <param name="info">The <see cref="StatisticEffectInfo"/> containing the effect information.</param>
    /// <returns>The calculated canonical stat value.</returns>
    internal static float GetCanonicalStatValue(StatisticEffectInfo info) {
        var effectTemplate = GetEffectTemplate(info.m_effectName);
        if (effectTemplate is null) {
            WarnOnce(info.m_effectName, "Stat effect {Effect} is not in the game data; the item bonus counts as zero.");

            return 0;
        }

        // Cast to WizStatisticEffectTemplate to access the stat table name property.
        var castedTemplate = (WizStatisticEffectTemplate) effectTemplate;
        var statTable = GameEffectRuleData.GetWizardStatTable(castedTemplate.m_statTableName);
        if (statTable is null) {
            WarnOnce(effectTemplate.m_effectName, "Stat effect {Effect} has no stat table in the game data; the item bonus counts as zero.");

            return 0;
        }

        var stat = GetStatValue(statTable, info.m_lookupIndex);
        var category = effectTemplate.m_effectCategory;
        var isFlat = IsFlatEffect(info.m_effectName);

        return CalculateStatValue(stat, category, isFlat);
    }

    /// <summary>
    /// Retrieves the effect template based on the provided effect name.
    /// </summary>
    /// <param name="effectName"></param>
    /// <returns></returns>
    internal static GameEffectTemplate GetEffectTemplate(string effectName)
        => s_effectTable.m_effectTemplates.FirstOrDefault(x => x.m_effectName == effectName);

    public void DisposeStream()
        => Stream.Dispose();

    private static float GetStatValue(WizardStatTable statTable, int lookupIndex) {
        if (lookupIndex >= statTable.m_statVector.Count) {
            WarnOnce($"{statTable.m_tableName}#{lookupIndex}", "Stat table entry {Entry} is past the end of its table; the item bonus counts as zero.");

            return 0;
        }

        return statTable.m_statVector[lookupIndex];
    }

    private static bool IsFlatEffect(string effectName)
        => effectName.ToString().Contains("Flat");

    private static float CalculateStatValue(float stat, string category, bool isFlat) {
        if (s_timesHundredEffectNames.Contains(category) && !isFlat) {
            return stat * 100f;
        }
        else {
            return stat;
        }
    }

    private static void WarnOnce(string effect, string message) {
        if (s_warnedEffects.TryAdd(effect, 0)) {
            Logger.Warning(message, Logger.Args(effect));
        }
    }

}
