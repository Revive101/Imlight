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
 * Scales the stats of a duel copy of a wizard to a level, the way the client does for a level scaling effect: stats above the wizard's baseline at that level are lowered to it, and a weaker wizard keeps the same share of the baseline.
 * 
 * USAGE EXAMPLE:
 * if (PvPStatScaler.TryScale(duelCopy, table, scaleToLevel, scaleUp, schoolIndices)) { }
 * 
 * NOTE:
 * The client scales once per stats object, so this does too: a stats object that was scaled is left alone. The lists of the stats are replaced by new ones, never changed, because a copy of a wizard's stats shares its lists with the wizard.
 * A wizard below the level scales up only when the effect says so, otherwise the stats are only capped. A wizard at or above the level scales down. The scaled health and mana start full.
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
using System.Globalization;
using System.Linq;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Behaviors;

namespace Imlight.CoreLib.Game.PvP.Rules;

internal static class PvPStatScaler {

    private const float HEALTH_ROUNDING_THRESHOLD = 0.498f;

    private enum ScaleMode {

        None,
        CapOnly,
        ScaleUp,
        ScaleDown

    }

    private readonly record struct ScaleContext(ScaleMode Mode, LevelScaledRow Previous, LevelScaledRow Next, LevelScaledRow Floor);

    private sealed record SchoolStat(
        string RowKey,
        Func<ServerWizGameStats, List<float>> GetList,
        Action<ServerWizGameStats, List<float>> SetList,
        Func<ServerWizGameStats, float> GetAll);

    private sealed record SingleStat(
        string RowKey,
        Func<ServerWizGameStats, float> GetTotal,
        Action<ServerWizGameStats, float> AddDelta);

    private static readonly SchoolStat[] s_schoolStats = [
        new("Accuracy", s => s.m_accBonusPercent, (s, l) => s.m_accBonusPercent = l, s => s.m_accBonusPercentAll),
        new("ArmorPiercing", s => s.m_apBonusPercent, (s, l) => s.m_apBonusPercent = l, s => s.m_apBonusPercentAll),
        new("Damage", s => s.m_dmgBonusPercent, (s, l) => s.m_dmgBonusPercent = l, s => s.m_dmgBonusPercentAll),
        new("FlatDamage", s => s.m_dmgBonusFlat, (s, l) => s.m_dmgBonusFlat = l, s => s.m_dmgBonusFlatAll),
        new("ReduceDamage", s => s.m_dmgReducePercent, (s, l) => s.m_dmgReducePercent = l, s => s.m_dmgReducePercentAll),
        new("FlatReduceDamage", s => s.m_dmgReduceFlat, (s, l) => s.m_dmgReduceFlat = l, s => s.m_dmgReduceFlatAll),
        new("CriticalHit", s => s.m_criticalHitRatingBySchool, (s, l) => s.m_criticalHitRatingBySchool = l, s => s.m_criticalHitRatingAll),
        new("Block", s => s.m_blockRatingBySchool, (s, l) => s.m_blockRatingBySchool = l, s => s.m_blockRatingAll),
        new("PipConversion", s => s.m_pipConversionRatingPerSchool, (s, l) => s.m_pipConversionRatingPerSchool = l, s => s.m_pipConversionRatingAll),
    ];

    private static readonly SingleStat[] s_singleStats = [
        new("PowerPip", s => s.m_powerPipBase + s.m_powerPipBonusPercentAll, (s, d) => s.m_powerPipBonusPercentAll += d),
        new("ShadowPipRating", s => s.m_shadowPipRating + s.m_bonusShadowPipRating, (s, d) => s.m_bonusShadowPipRating += d),
        new("IncHealing", s => s.m_healIncBonusPercentAll, (s, d) => s.m_healIncBonusPercentAll += d),
        new("LifeHealing", s => s.m_healBonusPercentAll, (s, d) => s.m_healBonusPercentAll += d),
        new("AllArchmastery", s => s.m_archmasteryBase + s.m_archmasteryBonus, (s, d) => s.m_archmasteryBonus += d),
    ];

    /// <summary>
    /// Scales the stats of a duel copy of a wizard, replacing its lists with scaled ones.
    /// </summary>
    /// <returns><c>false</c> when nothing was scaled.</returns>
    internal static bool TryScale(ServerWizGameStats stats,
                                  LevelScaledTable table,
                                  int scaleToLevel,
                                  bool scaleUp,
                                  IReadOnlyDictionary<string, int> schoolIndices) {
        var mode = GetMode(stats.Level, scaleToLevel, scaleUp);
        if (mode == ScaleMode.None || stats.m_levelScaled != 0) {
            return false;
        }

        var school = stats.MagicSchool.ToString();
        if (!table.TryGetRow(school, scaleToLevel, out var next) || !table.TryGetRow(school, stats.Level, out var previous)) {
            return false;
        }

        var context = new ScaleContext(mode, previous, next, mode == ScaleMode.ScaleUp ? table.FloorRow : null);
        ScaleHealthAndMana(stats, context);
        ScaleSchoolStats(stats, context, schoolIndices);
        ScaleSingleStats(stats, context);
        if (mode != ScaleMode.CapOnly) {
            stats.m_levelScaled = scaleToLevel;
        }

        return true;
    }

    /// <summary>
    /// Lists the stats of one school that matter in a duel, for a log line.
    /// </summary>
    internal static string Describe(ServerWizGameStats stats, int schoolIndex) {
        return string.Create(CultureInfo.InvariantCulture,
            $"health {stats.m_currentHitpoints}/{stats.m_baseHitpoints}, damage {Total(s => s.m_dmgBonusPercent, s => s.m_dmgBonusPercentAll)}, "
            + $"resist {Total(s => s.m_dmgReducePercent, s => s.m_dmgReducePercentAll)}, accuracy {Total(s => s.m_accBonusPercent, s => s.m_accBonusPercentAll)}, "
            + $"crit {Total(s => s.m_criticalHitRatingBySchool, s => s.m_criticalHitRatingAll)}, block {Total(s => s.m_blockRatingBySchool, s => s.m_blockRatingAll)}");

        float Total(Func<ServerWizGameStats, List<float>> getList, Func<ServerWizGameStats, float> getAll) {
            var list = getList(stats);
            var perSchool = list is not null && schoolIndex < list.Count ? list[schoolIndex] : 0f;

            return perSchool + getAll(stats);
        }
    }

    private static ScaleMode GetMode(int level, int scaleToLevel, bool scaleUp) {
        if (scaleToLevel <= 0) {
            return ScaleMode.None;
        }

        if (level < scaleToLevel) {
            return scaleUp ? ScaleMode.ScaleUp : ScaleMode.CapOnly;
        }

        return scaleUp ? ScaleMode.None : ScaleMode.ScaleDown;
    }

    private static void ScaleHealthAndMana(ServerWizGameStats stats, ScaleContext context) {
        stats.m_baseHitpoints = RescaleMaximum(stats.m_baseHitpoints, "MaxHealth", context);
        stats.m_currentHitpoints = stats.m_baseHitpoints;
        stats.m_baseMana = RescaleMaximum(stats.m_baseMana, "MaxMana", context);
        stats.m_currentMana = stats.m_baseMana;
    }

    private static void ScaleSchoolStats(ServerWizGameStats stats, ScaleContext context, IReadOnlyDictionary<string, int> schoolIndices) {
        var size = Math.Max(schoolIndices.Values.Max(), (int) MagicSchools.GetMaxMagicSchoolIndex()) + 1;
        foreach (var stat in s_schoolStats) {
            var list = CopyList(stat.GetList(stats), size);
            var all = stat.GetAll(stats);
            foreach (var (school, index) in schoolIndices) {
                var total = list[index] + all;
                list[index] += Rescale(total, school + stat.RowKey, context) - total;
            }

            stat.SetList(stats, list);
        }
    }

    private static void ScaleSingleStats(ServerWizGameStats stats, ScaleContext context) {
        foreach (var stat in s_singleStats) {
            var total = stat.GetTotal(stats);
            stat.AddDelta(stats, Rescale(total, stat.RowKey, context) - total);
        }
    }

    private static List<float> CopyList(List<float> source, int size) {
        var copy = source is null ? [] : new List<float>(source);
        while (copy.Count < size) {
            copy.Add(0f);
        }

        return copy;
    }

    private static int RescaleMaximum(int maximum, string key, ScaleContext context) {
        var scaled = Rescale(maximum, key, context);

        return (int) MathF.Floor(scaled + (1f - HEALTH_ROUNDING_THRESHOLD));
    }

    private static float Rescale(float total, string key, ScaleContext context) {
        var next = context.Next.Get(key);
        if (context.Mode == ScaleMode.CapOnly) {
            return MathF.Min(total, next);
        }

        var previous = context.Previous.Get(key);
        var ratio = previous > 0f ? MathF.Min(total / previous, 1f) : 0f;
        if (context.Floor is not null) {
            ratio = MathF.Max(ratio, context.Floor.Get(key));
        }

        return ratio * next;
    }

}
