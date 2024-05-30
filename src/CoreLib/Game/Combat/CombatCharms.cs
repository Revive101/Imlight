/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Provides utility methods for handling combat charms in the game.
/// </summary>
internal static class CombatCharms {
    /// <summary>
    /// Finds the applied charms from the given array of spell effects based on the caster.
    /// </summary>
    /// <param name="caster">The combat duel actor sub-circle representing the caster.</param>
    /// <param name="effects">The array of spell effects to search for applied charms.</param>
    /// <returns>A list of spell effects representing the applied charms.</returns>
    internal static List<SpellEffect> FindAppliedCharms(CombatDuelActorSubCircle caster, SpellEffect[] effects) {
        var appliedCharms = new List<SpellEffect>();

        foreach (var effect in effects) {
            var isDamageEffect = effect.m_effectType is SpellEffect.kSpellEffects.kDamage
                                                     or SpellEffect.kSpellEffects.kDamageOverTime
                                                     or SpellEffect.kSpellEffects.kDamageNoCrit
                                                     or SpellEffect.kSpellEffects.kDamagePerTotalPipPower
                                                     or SpellEffect.kSpellEffects.kDivideDamage
                                                     or SpellEffect.kSpellEffects.kStealHealth;
            var isHealEffect = effect.m_effectType is SpellEffect.kSpellEffects.kHeal
                                                   or SpellEffect.kSpellEffects.kHealOverTime;

            if (isDamageEffect) {
                appliedCharms = caster._hangingEffects
                    .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyOutgoingDamage)
                    .Where(x => x.m_sDamageType == effect.m_sDamageType || x.m_sDamageType == "All")
                    .Reverse()
                    .ToList();
            }
            else if (isHealEffect) {
                appliedCharms = caster._hangingEffects
                    .Where(x => x.m_effectType is SpellEffect.kSpellEffects.kModifyIncomingHeal
                                               or SpellEffect.kSpellEffects.kModifyIncomingHealFlat)
                    .Reverse()
                    .ToList();
            }
        }

        return appliedCharms;
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
}
