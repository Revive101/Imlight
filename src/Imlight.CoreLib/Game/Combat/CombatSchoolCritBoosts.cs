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
 * COMBAT SCHOOL CRIT BOOSTS
 * ========================================================================
 *
 * PURPOSE:
 * Works out the critical chance that school-specific crit boosts add to a
 * cast: a charm on the caster is spent, a battlefield effect stays.
 *
 * USAGE EXAMPLE:
 * chance += CombatSchoolCritBoosts.TakeBoostPercent(caster, school);
 *
 * NOTE:
 * The client only applies the boost when the spell's school matches the
 * effect's school. The plain crit boost stays in CombatCriticals.
 *
 * TODO:
 * - That a battlefield school crit boost adds to the roll like the charm does is unconfirmed.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Reads the school-specific crit boosts that apply to a cast.
/// </summary>
internal static class CombatSchoolCritBoosts {

    /// <summary>
    /// Gets the crit chance school-specific boosts add to a cast of the given school, spending the caster's matching charm.
    /// </summary>
    /// <param name="caster">The caster of the spell.</param>
    /// <param name="school">The school name of the spell.</param>
    /// <returns>The added chance, as a fraction.</returns>
    internal static float TakeBoostPercent(CombatDuelSubCircle caster, string school) {
        var boost = 0f;
        var charm = caster._hangingEffects?.FirstOrDefault(x => IsBoostFor(x, school));
        if (charm is not null) {
            caster._hangingEffects.Remove(charm);
            boost += charm.m_effectParam / 100f;
        }

        var battlefield = CombatEffectApplicator.GetCurrentGlobalEffects(caster._duelActor.Duel)
            .FirstOrDefault(x => IsBoostFor(x, school));
        if (battlefield is not null) {
            boost += battlefield.m_effectParam / 100f;
        }

        return boost;
    }

    private static bool IsBoostFor(SpellEffect effect, string school)
        => effect.m_effectType == kSpellEffects.kCritBoostSchoolSpecific
        && effect.m_sDamageType == school;

}
