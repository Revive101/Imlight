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
 * Finds the level scaling of a league and applies it to the duel copy of a wizard's stats.
 * 
 * USAGE EXAMPLE:
 * if (PvPLeagueScaling.TryScaleDuelStats(leagueId, duelStats)) { }
 * 
 * NOTE:
 * The scaling comes from data: the league lists duel modifiers, a modifier lists game effects, and one of those effects is a level scaling effect with the level to scale to. A league without such an effect, and league id 0, scale nothing.
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
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Rules;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Behaviors;

namespace Imlight.CoreLib.Game.PvP.Data;

internal static class PvPLeagueScaling {

    private static Dictionary<string, int> s_schoolIndices;

    internal static bool TryGetScale(uint leagueId, out PvPLevelScale scale) {
        scale = default;
        if (leagueId == 0 || !PvPLeagueFactory.TryGetLeague(leagueId, out var league)) {
            return false;
        }

        foreach (var effectName in GetEffectNames(league)) {
            if (LevelScaledEffectFactory.TryGetTemplate(effectName, out var template) && template.m_scaleToLevel > 0) {
                scale = new PvPLevelScale(effectName, template.m_scaleToLevel, template.m_scaleUp);

                return true;
            }
        }

        return false;
    }

    internal static List<GameEffectInfo> CreateClientEffects(uint leagueId) {
        if (!TryGetScale(leagueId, out var scale)) {
            return [];
        }

        return [new GameEffectInfo { m_effectName = scale.EffectName }];
    }

    internal static bool TryScaleDuelStats(uint leagueId, ServerWizGameStats duelStats) {
        if (!TryGetScale(leagueId, out var scale)) {
            return false;
        }

        if (!LevelScaledDataFactory.TryGetTable(out var table) || !TryGetSchoolIndices(out var schoolIndices)) {
            Logger.Error("League {LeagueId} scales stats, but the level scaling data is not loaded.", Logger.Args(leagueId));

            return false;
        }

        var before = PvPStatScaler.Describe(duelStats, schoolIndices.GetValueOrDefault(duelStats.MagicSchool.ToString()));
        if (!PvPStatScaler.TryScale(duelStats, table, scale.ScaleToLevel, scale.ScaleUp, schoolIndices)) {
            Logger.Debug("Stat scaling to level {Level} left a level {WizardLevel} {School} wizard as is.",
                Logger.Args(scale.ScaleToLevel, duelStats.Level, duelStats.MagicSchool));

            return false;
        }

        Logger.Debug("Stat scaling to level {Level} for a level {WizardLevel} {School} wizard. Before: {Before}. After: {After}.",
            Logger.Args(scale.ScaleToLevel, duelStats.Level, duelStats.MagicSchool, before,
                PvPStatScaler.Describe(duelStats, schoolIndices.GetValueOrDefault(duelStats.MagicSchool.ToString()))));

        return true;
    }

    private static IEnumerable<string> GetEffectNames(PvPLeagueTemplate league) {
        foreach (var modifierName in league.m_duelModifiers ?? []) {
            if (!DuelModifierFactory.TryGetModifier(modifierName.ToString(), out var modifier)) {
                continue;
            }

            foreach (var effect in modifier.m_gameEffects ?? []) {
                yield return effect.m_effectName.ToString();
            }
        }
    }

    private static bool TryGetSchoolIndices(out Dictionary<string, int> schoolIndices) {
        schoolIndices = s_schoolIndices;
        if (schoolIndices is not null) {
            return true;
        }

        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var school in Enum.GetValues<MagicSchool>().Where(school => school != MagicSchool.None)) {
            var template = MagicSchools.GetMagicSchool(school.ToString());
            if (template is not null) {
                indices[school.ToString()] = (int) template.m_schoolIndex;
            }
        }

        if (indices.Count == 0) {
            return false;
        }

        s_schoolIndices = indices;
        schoolIndices = indices;

        return true;
    }

}
