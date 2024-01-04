using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Effects;

public static class CanonicalStatEffects {
    /*
    Recall the docs @ https://revive101.github.io/Imlight-docs/internals/schemas.html#object-creation
    Effects that are 'canonical' are effects that change numerical player stats, such as health or damage.
    Canonical effects follow this schema. The template manifest for these effects is the CanonicalStatEffects.xml file.

    The template:
    Every template in the CanonicalStatEffects.xml file has a corresponding stat table.
    These tables are located in the GameEffectRuleData directory in the Root.wad file.
    The stat table name property (m_statTableName) in the template is the path of the table in the Root.wad file.

    The information:
    The effect information contains only two bits of information: the effect name and the lookup index.
    The effect name is used to find the effect template in the CanonicalStatEffects.xml file.
    The lookup index is used to find the stat value in the corresponding stat table.

    Some stats are multiplied by 100.
    */

    private const string EffectTablePath = "GameEffectData/CanonicalStatEffects.xml";
    private const string GameRuleDirectoryPrefix = "GameEffectRuleData/";
    private static readonly string[] s_timesHundredEffectNames = {
        "Accuracy",
        "Damage",
        "Piercing",
        "ReduceDamage",
    };

    private static GameEffectTemplateList s_effectTable;
    private static WizardStatTable[] s_wizardStatTables;
    private static bool s_isLoaded;

    /// <summary>
    /// Loads the canonical stat effects and stat tables.
    /// </summary>
    internal static void Load() {
        s_effectTable = ResourceManager.LoadDeserializedFile<GameEffectTemplateList>(ResourceManager.RootWadName, EffectTablePath);
        if (s_effectTable is null) {
            Logger.Error("Could not find effect table {0} in {1}", Logger.Args(EffectTablePath, ResourceManager.RootWadName));
            return;
        }

        // Iterate through each table and find the stat table name.
        // This will be the literal name of the table in the Root.wad file.
        var statTables = new List<WizardStatTable>();
        var seenTables = new HashSet<string>();
        foreach (var template in s_effectTable.m_effectTemplates) {
            var statEffectTemplate = (WizStatisticEffectTemplate) template;
            var statTableName = statEffectTemplate.m_statTableName;

            // Check for duplicates.
            if (!seenTables.Add(statTableName)) {
                continue;
            }

            // Search for this table in the Root.wad file.
            var statTableDirectory = GameRuleDirectoryPrefix + statTableName + ".xml";
            var statTable = ResourceManager.LoadDeserializedFile<WizardStatTable>(ResourceManager.RootWadName, statTableDirectory);
            if (statTable is null) {
                // There seem to be server side tables here. Not all of them are in the client.
                // Logger.Error("Could not find stat table {0} in {1}", Logger.Args(statTableName, ResourceManager.RootWadName));
            }

            statTables.Add(statTable);
            seenTables.Add(statTableName);
        }

        s_wizardStatTables = statTables.ToArray();
        Logger.Information("Loaded {0} canonical stat tables", Logger.Args(s_wizardStatTables.Length));

        s_isLoaded = true;
    }

    /// <summary>
    /// Retrieves the canonical stat value based on the provided <see cref="StatisticEffectInfo"/>.
    /// </summary>
    /// <param name="info">The <see cref="StatisticEffectInfo"/> containing the effect information.</param>
    /// <returns>The calculated canonical stat value.</returns>
    internal static float GetCanonicalStatValue(StatisticEffectInfo info) {
        if (!s_isLoaded) {
            Load();
        }

        if (s_effectTable is null || s_wizardStatTables is null) {
            Logger.Error("Effect table or stat tables are null. Cannot gather stat value.");
            return 0;
        }

        var effectTemplate = GetEffectTemplate(info.m_effectName);
        if (effectTemplate is null) {
            Logger.Error("Could not find effect template {0}", Logger.Args(info.m_effectName));
            return 0;
        }

        var statTable = GetStatTable(effectTemplate);
        if (statTable is null) {
            Logger.Error("Could not find stat table for template {0}", Logger.Args(effectTemplate.m_effectName));
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

    private static WizardStatTable GetStatTable(GameEffectTemplate effectTemplate) {
        var tableName = ((WizStatisticEffectTemplate)effectTemplate).m_statTableName;
        return s_wizardStatTables.FirstOrDefault(x => x is not null && x.m_tableName == tableName);
    }

    private static float GetStatValue(WizardStatTable statTable, int lookupIndex) {
        if (lookupIndex >= statTable.m_statVector.Count) {
            Logger.Error("Invalid lookup index {0} for stat table {1}", Logger.Args(lookupIndex, statTable.m_tableName));
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
}
