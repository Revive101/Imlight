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
 * COMBAT OFFENSIVE MODIFIER SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages damage and healing boosts (charms) that modify outgoing spell effects
 * based on magic school and effect type.
 * 
 * USAGE EXAMPLE:
 * var charms = CombatCharms.FindAppliedCharms(caster, effects);
 * int modifiedDamage = CombatCharms.GetOutgoingDamageFromCharms(charms, initialDamage);
 * 
 * NOTE:
 * Distinguishes between beneficial and harmful charms, ensuring proper
 * application order and stacking behavior for damage and healing modifiers.
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Imcodec.ObjectProperty.TypeCache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Provides utility methods for handling offensive combat modifiers (charms) in combat.
/// </summary>
/// <remarks>
/// Manages the selection and application of outgoing damage and healing modifiers, determining
/// which charms affect specific spells based on the caster, target, and spell school. Handles
/// both beneficial (damage/healing boosting) and harmful (damage reducing) charms.
/// </remarks>
internal static class CombatCharms {

    /// <summary>
    /// Finds the applied charms from the given array of spell effects based on the caster.
    /// </summary>
    /// <param name="caster">The combat duel actor sub-circle representing the caster.</param>
    /// <param name="effects">The array of spell effects to search for applied charms.</param>
    /// <returns>A list of spell effects representing the applied charms.</returns>
    internal static List<SpellEffect> FindAppliedCharms(CombatDuelSubCircle caster,
                                                        SpellEffect[] effects,
                                                        kHangingDisposition disposition = kHangingDisposition.kBoth) {
        var appliedCharms = new List<SpellEffect>();

        foreach (var effect in effects) {
            var isDamageEffect = effect.m_effectType is kSpellEffects.kDamage
                                                     or kSpellEffects.kDamageOverTime
                                                     or kSpellEffects.kDamageNoCrit
                                                     or kSpellEffects.kDamagePerTotalPipPower
                                                     or kSpellEffects.kDivideDamage
                                                     or kSpellEffects.kStealHealth;
            var isHealEffect = effect.m_effectType is (kSpellEffects) 3 or (kSpellEffects) 76;

            // Choose the beneficial or harmful charms based on the disposition.
            var beneficialCharms = GetBeneficialCharms(caster);
            var harmfulCharms = GetHarmfulCharms(caster);
            var charms = disposition switch {
                kHangingDisposition.kBeneficial => beneficialCharms,
                kHangingDisposition.kHarmful => harmfulCharms,
                _ => [.. beneficialCharms.Concat(harmfulCharms).Reverse()]
            };

            if (isDamageEffect) {
                appliedCharms = [.. beneficialCharms.Where(x => x.m_sDamageType == effect.m_sDamageType || x.m_sDamageType == "All").Reverse()];
            }
            else if (isHealEffect) {
                appliedCharms = [.. beneficialCharms.Where(x => x.m_effectType is kSpellEffects.kModifyOutgoingHeal
                                                                           or kSpellEffects.kModifyOutgoingHealFlat)
                                                .Reverse()];
            }
        }

        return [.. appliedCharms.DistinctBy(x => x.m_spellTemplateID)];
    }

    /// <summary>
    /// Calculates the outgoing damage from the given array of charms and initial damage.
    /// </summary>
    /// <param name="charms">The array of spell effects representing the charms.</param>
    /// <param name="initialDamage">The initial damage value.</param>
    /// <returns>The calculated outgoing damage.</returns>
    internal static int GetOutgoingDamageFromCharms(SpellEffect[] charms, int initialDamage = 0) {
        foreach (var charm in charms) {
            var damageChange = charm.m_effectParam / 100.0f;
            initialDamage = (int) Math.Floor(initialDamage * (1 + damageChange));
        }

        return initialDamage;
    }

    private static IEnumerable<SpellEffect> GetBeneficialCharms(CombatDuelSubCircle target) => target._hangingEffects
            .Where(x => x.m_effectType is kSpellEffects.kModifyOutgoingDamage && x.m_effectParam > 0
                                       || x.m_effectType == kSpellEffects.kModifyOutgoingHeal
                                       || x.m_effectType == kSpellEffects.kModifyOutgoingHealFlat);

    private static IEnumerable<SpellEffect> GetHarmfulCharms(CombatDuelSubCircle target) => target._hangingEffects
            .Where(x => x.m_effectType is kSpellEffects.kModifyOutgoingDamage && x.m_effectParam < 0);
            
}
