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
 * COMBAT DAMAGE MODIFIER SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Applies the flat, armor piercing, school change and damage cap charms and wards to a
 * damage hit, the way the client's hanging effect handlers change the hit's effect.
 *
 * USAGE EXAMPLE:
 * var school = CombatDamageModifiers.GetOutgoingSchool(charms, effect.m_sDamageType);
 * var pierce = CombatDamageModifiers.PierceWards(wards, CombatDamageModifiers.GetOutgoingArmorPiercing(charms, effect));
 *
 * NOTE:
 * Percent charms and wards stay in CombatCharms and CombatWards; this type only adds the
 * other damage modifier types to the same passes.
 *
 * TODO:
 * - The client applies charms and wards in hanging order; here flat and caps follow the percents.
 * - A cap with rounds left is a multi-round ward on the client; here it is spent like any ward.
 * - The client adds an armor piercing ward in hanging order, so it may not reach a shield hung before it;
 *   here every armor piercing ward is added before any shield is pierced.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Flat, armor piercing, school change and cap modifiers for damage hits.
/// </summary>
internal static class CombatDamageModifiers {

    private const int EFFECT_PARAM_LIMIT = 1000000;
    private const string ALL_SCHOOLS = "All";

    /// <summary>
    /// Whether the effect type is a damage modifier this type hangs on its targets.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <returns>True for a damage modifier.</returns>
    internal static bool IsHungModifier(kSpellEffects effectType) => IsOutgoingModifier(effectType)
                                                                   || IsIncomingModifier(effectType)
                                                                   || effectType == kSpellEffects.kModifyIncomingDamageOverTime;

    /// <summary>
    /// Whether the effect type is a non-percent charm on outgoing damage.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <returns>True for a flat, armor piercing or school change charm.</returns>
    internal static bool IsOutgoingModifier(kSpellEffects effectType) => effectType is kSpellEffects.kModifyOutgoingDamageFlat
                                                                                    or kSpellEffects.kModifyOutgoingArmorPiercing
                                                                                    or kSpellEffects.kModifyOutgoingDamageType;

    /// <summary>
    /// Whether the effect type is a non-percent ward on incoming damage.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <returns>True for a flat, armor piercing or cap ward.</returns>
    internal static bool IsIncomingModifier(kSpellEffects effectType) => effectType is kSpellEffects.kModifyIncomingDamageFlat
                                                                                    or kSpellEffects.kModifyIncomingArmorPiercing
                                                                                    or kSpellEffects.kMaximumIncomingDamage;

    /// <summary>
    /// Hangs a copy of a damage modifier on every target.
    /// </summary>
    /// <param name="effect">The modifier effect.</param>
    /// <param name="targets">The targets of the effect.</param>
    /// <param name="context">The per-action values of the cast.</param>
    /// <returns>The cinematic time of the added effects.</returns>
    internal static float Hang(SpellEffect effect, CombatDuelSubCircle[] targets, CombatEffectContext context) {
        foreach (var target in targets) {
            target._hangingEffects.Add(CombatHangingEffects.CopyForTarget(effect, context.SpellTemplateId));
        }

        return targets.Count(x => x.IsAlive) * CombatHangingEffects.HANGING_EFFECT_ADD_TIME;
    }

    /// <summary>
    /// Finds the caster's flat, armor piercing and school change charms that a spell's damage effects use.
    /// </summary>
    /// <param name="caster">The caster of the spell.</param>
    /// <param name="effects">The spell's effects so far.</param>
    /// <returns>The charms, newest first.</returns>
    internal static IEnumerable<SpellEffect> FindOutgoingModifiers(CombatDuelSubCircle caster, IEnumerable<SpellEffect> effects) {
        var schools = effects.Where(IsDamageHit).Select(x => (string) x.m_sDamageType).ToHashSet(StringComparer.Ordinal);
        if (schools.Count == 0) {
            return [];
        }

        return caster._hangingEffects
            .Where(x => IsOutgoingModifier(x.m_effectType) && (x.m_sDamageType == ALL_SCHOOLS || schools.Contains(x.m_sDamageType)))
            .Reverse()
            .DistinctBy(x => (x.m_spellTemplateID, x.m_effectType))
            .ToList();
    }

    /// <summary>
    /// Adds the flat charms of a hit's school to its damage.
    /// </summary>
    /// <param name="charms">The charms applied to the cast.</param>
    /// <param name="school">The school of the hit.</param>
    /// <param name="damage">The damage so far.</param>
    /// <returns>The damage with the flat charms added.</returns>
    internal static int ApplyOutgoingFlat(SpellEffect[] charms, string school, int damage) {
        foreach (var charm in Matching(charms, kSpellEffects.kModifyOutgoingDamageFlat, school)) {
            damage = AddFlat(damage, charm.m_effectParam);
        }

        return damage;
    }

    /// <summary>
    /// Gets a hit's armor piercing: the effect's own plus the armor piercing charms of its school.
    /// </summary>
    /// <param name="charms">The charms applied to the cast.</param>
    /// <param name="effect">The damage effect.</param>
    /// <returns>The armor piercing in percent points.</returns>
    internal static int GetOutgoingArmorPiercing(SpellEffect[] charms, SpellEffect effect)
        => effect.m_armorPiercingParam + Matching(charms, kSpellEffects.kModifyOutgoingArmorPiercing, effect.m_sDamageType)
            .Sum(x => x.m_effectParam);

    /// <summary>
    /// Gets the school a hit lands as after the school change charms of its school.
    /// </summary>
    /// <param name="charms">The charms applied to the cast.</param>
    /// <param name="school">The school of the hit.</param>
    /// <returns>The school the hit lands as.</returns>
    internal static string GetOutgoingSchool(SpellEffect[] charms, string school) {
        var landingSchool = school;

        foreach (var charm in Matching(charms, kSpellEffects.kModifyOutgoingDamageType, school)) {
            landingSchool = MagicSchools.GetMagicSchool((uint) charm.m_effectParam)?.m_schoolName ?? landingSchool;
        }

        return landingSchool;
    }

    /// <summary>
    /// Leaves out the over-time-only wards when the hit is not an over-time tick.
    /// </summary>
    /// <param name="wards">The wards found for the hit.</param>
    /// <param name="hit">The damage effect of the hit.</param>
    /// <returns>The wards that apply to the hit.</returns>
    internal static List<SpellEffect> WithoutOverTimeWards(List<SpellEffect> wards, SpellEffect hit) {
        if (CombatHangingEffects.IsOverTime(hit)) {
            return wards;
        }

        return [.. wards.Where(x => x.m_effectType != kSpellEffects.kModifyIncomingDamageOverTime)];
    }

    /// <summary>
    /// Runs a hit's armor piercing through its wards: the target's armor piercing wards add to it, then it lifts
    /// the negative percent of each damage-reducing ward in order until it runs out.
    /// </summary>
    /// <param name="wards">The wards the hit uses, in application order. Pierced wards are changed in place.</param>
    /// <param name="armorPiercing">The hit's armor piercing.</param>
    /// <returns>The armor piercing left for the target's resist.</returns>
    internal static int PierceWards(List<SpellEffect> wards, int armorPiercing) {
        armorPiercing += wards.Where(x => x.m_effectType == kSpellEffects.kModifyIncomingArmorPiercing).Sum(x => x.m_effectParam);

        foreach (var ward in wards) {
            var isPercentWard = ward.m_effectType is kSpellEffects.kModifyIncomingDamage or kSpellEffects.kModifyIncomingDamageOverTime;
            if (!isPercentWard || ward.m_effectParam >= 0 || armorPiercing <= 0) {
                continue;
            }

            var pierced = armorPiercing + ward.m_effectParam;
            armorPiercing = Math.Max(pierced, 0);
            ward.m_effectParam = Math.Min(pierced, 0);
        }

        return armorPiercing;
    }

    /// <summary>
    /// Applies a flat ward to a hit, or skips an armor piercing or cap ward, which have their own passes.
    /// </summary>
    /// <param name="ward">The ward.</param>
    /// <param name="damage">The hit's damage, changed by a flat ward.</param>
    /// <returns>True when the ward is one of this type's and the percent pass must skip it.</returns>
    internal static bool TryApplyIncomingModifier(SpellEffect ward, ref int damage) {
        if (!IsIncomingModifier(ward.m_effectType)) {
            return false;
        }

        if (ward.m_effectType == kSpellEffects.kModifyIncomingDamageFlat) {
            damage = Math.Max(AddFlat(damage, ward.m_effectParam), 0);
        }

        return true;
    }

    /// <summary>
    /// Caps a hit at its damage cap wards. A cap the hit stays under is not used, so it leaves the used list
    /// and stays on the target.
    /// </summary>
    /// <param name="wards">The wards the hit uses.</param>
    /// <param name="damage">The hit's damage after the other wards.</param>
    /// <returns>The capped damage.</returns>
    internal static int ApplyIncomingCaps(List<SpellEffect> wards, int damage) {
        var caps = wards.Where(x => x.m_effectType == kSpellEffects.kMaximumIncomingDamage).ToList();

        foreach (var cap in caps) {
            if (cap.m_effectParam < 0 || cap.m_effectParam >= damage) {
                wards.Remove(cap);

                continue;
            }

            damage = cap.m_effectParam;
        }

        return damage;
    }

    /// <summary>
    /// Lowers a target's resist by the armor piercing a hit has left after the wards.
    /// </summary>
    /// <param name="reducePercent">The target's resist as a fraction.</param>
    /// <param name="armorPiercing">The hit's armor piercing in percent points.</param>
    /// <returns>The resist the hit meets.</returns>
    internal static float PierceResist(float reducePercent, int armorPiercing) {
        if (reducePercent <= 0 || armorPiercing <= 0) {
            return reducePercent;
        }

        return Math.Max(reducePercent - armorPiercing / 100.0f, 0);
    }

    private static IEnumerable<SpellEffect> Matching(SpellEffect[] charms, kSpellEffects effectType, string school)
        => charms.Where(x => x.m_effectType == effectType && (x.m_sDamageType == ALL_SCHOOLS || x.m_sDamageType == school));

    private static int AddFlat(int param, int flat) => Math.Clamp(param + flat, -EFFECT_PARAM_LIMIT, EFFECT_PARAM_LIMIT);

    private static bool IsDamageHit(SpellEffect effect) => effect.m_effectType is kSpellEffects.kDamage
                                                                               or kSpellEffects.kDamageNoCrit
                                                                               or kSpellEffects.kDamageOverTime
                                                                               or kSpellEffects.kDamagePerTotalPipPower
                                                                               or kSpellEffects.kDivideDamage
                                                                               or kSpellEffects.kStealHealth;

}
