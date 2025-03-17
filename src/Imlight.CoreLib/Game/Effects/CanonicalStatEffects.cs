/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Effects;

public class CanonicalStatEffects : RootSingleResourceSingleton<CanonicalStatEffects>, IMemoryStreamDisposable {

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

    protected override string ResourceName => "GameEffectData/CanonicalStatEffects.xml";
    private static readonly string[] s_timesHundredEffectNames = {
        "Accuracy",
        "Damage",
        "Piercing",
        "ReduceDamage",
    };

    private static GameEffectTemplateList s_effectTable;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var streamAsBytes = Stream.ToArray();
        if (!serializer.Deserialize(streamAsBytes, out s_effectTable)) {
            Logger.Error("Failed to deserialize canonical stat effects");

            return;
        }

        Logger.Information("Loaded {0} canonical stat effects", Logger.Args(s_effectTable.m_effectTemplates.Count));
    }

    /// <summary>
    /// Retrieves the canonical stat value based on the provided <see cref="StatisticEffectInfo"/>.
    /// </summary>
    /// <param name="info">The <see cref="StatisticEffectInfo"/> containing the effect information.</param>
    /// <returns>The calculated canonical stat value.</returns>
    internal static float GetCanonicalStatValue(StatisticEffectInfo info) {
        if (s_effectTable is null) {
            Logger.Error("Effect table was null. Cannot gather stat value.");
            return 0;
        }

        var effectTemplate = GetEffectTemplate(info.m_effectName);
        if (effectTemplate is null) {
            Logger.Error("Could not find effect template {0}", Logger.Args(info.m_effectName));
            return 0;
        }

        // Cast to WizStatisticEffectTemplate to access the stat table name property.
        var castedTemplate = (WizStatisticEffectTemplate) effectTemplate;
        var statTable = GameEffectRuleData.GetWizardStatTable(castedTemplate.m_statTableName);
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

    public void DisposeStream()
        => Stream.Dispose();

}
