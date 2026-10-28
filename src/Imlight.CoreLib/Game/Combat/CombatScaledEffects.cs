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
 * COMBAT SCALED EFFECTS
 * ========================================================================
 *
 * PURPOSE:
 * Applies the damage and heal effects whose amount is worked out per target
 * from combat state: no-crit damage, damage per pip, divided damage, percent
 * heals and heals per ward.
 *
 * USAGE EXAMPLE:
 * cinematicTime += CombatScaledEffects.Apply(effect, charms, caster, targets, context);
 *
 * NOTE:
 * The client turns the first four into a plain damage or heal before its
 * passes run; this class does the same and hands the result back to
 * CombatEffectApplicator.
 *
 * TODO:
 * - Divided damage only reaches targets once multi-target selection exists.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Applies damage and heal effects whose amount depends on the target or the combat state.
/// </summary>
internal static class CombatScaledEffects {

    private const int MINIMUM_HEALTH_FOR_PERCENT_HEAL = 100;

    /// <summary>
    /// Applies a scaled damage or heal effect to its targets.
    /// </summary>
    /// <param name="effect">The spell effect to apply.</param>
    /// <param name="charms">The charms currently applied to the caster.</param>
    /// <param name="caster">The caster of the spell.</param>
    /// <param name="targets">The targets of the spell effect.</param>
    /// <param name="context">The per-action values.</param>
    /// <returns>The cinematic time the effect adds.</returns>
    internal static float Apply(SpellEffect effect,
                                SpellEffect[] charms,
                                CombatDuelSubCircle caster,
                                CombatDuelSubCircle[] targets,
                                CombatEffectContext context) {
        var noCritContext = context with { CritMultiplier = 1f };

        return effect.m_effectType switch {
            kSpellEffects.kDamageNoCrit => ApplyAs(effect, kSpellEffects.kDamage, effect.m_effectParam, charms, caster, targets, noCritContext),
            kSpellEffects.kDamagePerTotalPipPower => ApplyDamagePerPip(effect, charms, caster, targets, context),
            kSpellEffects.kDivideDamage => ApplyDividedDamage(effect, charms, caster, targets, noCritContext),
            kSpellEffects.kHealPercent => ApplyPercentHeal(effect, charms, caster, targets, noCritContext),
            kSpellEffects.kHealByWard => ApplyHealByWard(effect, caster, targets),
            _ => 0.0f,
        };
    }

    private static float ApplyAs(SpellEffect effect,
                                 kSpellEffects type,
                                 int param,
                                 SpellEffect[] charms,
                                 CombatDuelSubCircle caster,
                                 CombatDuelSubCircle[] targets,
                                 CombatEffectContext context) {
        var converted = CombatHangingEffects.CopyForTarget(effect, context.SpellTemplateId);
        converted.m_effectType = type;
        converted.m_effectParam = param;

        return CombatEffectApplicator.ApplyEffect(converted, charms, caster, targets, context);
    }

    private static float ApplyDamagePerPip(SpellEffect effect,
                                           SpellEffect[] charms,
                                           CombatDuelSubCircle caster,
                                           CombatDuelSubCircle[] targets,
                                           CombatEffectContext context) {
        // The target's own pips count: a power pip counts twice, and m_numRounds is the damage per pip.
        var cinematicTime = 0.0f;

        foreach (var target in targets) {
            var pips = GetEffectivePipCount(target);
            if (pips == 0) {
                continue;
            }

            cinematicTime += ApplyAs(effect, kSpellEffects.kDamage, pips * effect.m_numRounds, charms, caster, [target], context);
        }

        return cinematicTime;
    }

    private static int GetEffectivePipCount(CombatDuelSubCircle target) {
        var pipCount = target.CombatParticipant?.m_pipCount;
        if (pipCount is null) {
            return 0;
        }

        // todo: the client also counts school pips twice; they are not added here.
        return pipCount.m_genericPips + pipCount.m_powerPips * 2;
    }

    private static float ApplyDividedDamage(SpellEffect effect,
                                            SpellEffect[] charms,
                                            CombatDuelSubCircle caster,
                                            CombatDuelSubCircle[] targets,
                                            CombatEffectContext context) {
        if (effect.m_effectParam <= 0 || targets.Length == 0) {
            return 0.0f;
        }

        // todo: the client divides by a count it is handed with the target list; the target count is assumed.
        return ApplyAs(effect, kSpellEffects.kDamage, effect.m_effectParam / targets.Length, charms, caster, targets, context);
    }

    private static float ApplyPercentHeal(SpellEffect effect,
                                          SpellEffect[] charms,
                                          CombatDuelSubCircle caster,
                                          CombatDuelSubCircle[] targets,
                                          CombatEffectContext context) {
        var cinematicTime = 0.0f;

        foreach (var target in targets) {
            if (!target.IsAlive) {
                continue;
            }

            var maxHealth = Math.Max(target.ParticipantGameStats.m_baseHitpoints, MINIMUM_HEALTH_FOR_PERCENT_HEAL);
            var amount = (int) (maxHealth * effect.m_effectParam / 100.0f);

            // A negative percent takes health straight off, as the client's health change does.
            if (amount < 0) {
                target.DamageParticipant(-amount);

                continue;
            }

            cinematicTime += ApplyAs(effect, kSpellEffects.kHeal, amount, charms, caster, [target], context);
        }

        return cinematicTime;
    }

    private static float ApplyHealByWard(SpellEffect effect, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        // Spends up to m_numRounds of the caster's beneficial wards; each one heals m_effectParam, plus m_paramPerRound once.
        // todo: whether the client's ward count includes wards that cannot be removed is unconfirmed; they are left out.
        var spent = CombatHangingEffects.Take(caster, CombatHangingEffects.HangingKind.Ward, kHangingDisposition.kBeneficial,
            Math.Max(effect.m_numRounds, 0), effect.m_bypassProtection);
        if (spent.Count == 0) {
            return 0.0f;
        }

        var heal = spent.Count * effect.m_effectParam + effect.m_paramPerRound;
        foreach (var target in targets) {
            if (target.IsAlive) {
                target.HealParticipant(heal);
            }
        }

        return spent.Count * CombatHangingEffects.HANGING_EFFECT_CONSUME_TIME;
    }

}
