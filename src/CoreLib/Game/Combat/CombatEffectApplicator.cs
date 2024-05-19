/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Shared.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Applies the effects of a spell to the targets.
/// </summary>
internal static class CombatEffectApplicator {
    private const float DAMAGE_PERCENT_MAX = 2.0f;
    private const float HANGING_EFFECT_CONSUME_TIME = 1.0f;

    internal static float ApplyEffect(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        var cinematicTime = 0.0f;

        switch (effect.m_effectType) {
            case SpellEffect.kSpellEffects.kDamage:
                cinematicTime += ApplyEffectDamage(effect, caster, targets);
                break;
            case SpellEffect.kSpellEffects.kHeal:
                cinematicTime += ApplyEffectHeal(effect, caster, targets);
                break;
            case SpellEffect.kSpellEffects.kStealHealth:
                cinematicTime += ApplyEffectStealHealth(effect, caster, targets);
                break;
            case SpellEffect.kSpellEffects.kModifyOutgoingDamage:
            case SpellEffect.kSpellEffects.kModifyIncomingDamage:
            case SpellEffect.kSpellEffects.kDispel:
            case SpellEffect.kSpellEffects.kModifyAccuracy:
            case SpellEffect.kSpellEffects.kModifyOutgoingHeal:
            case SpellEffect.kSpellEffects.kModifyOutgoingHealFlat:
            case SpellEffect.kSpellEffects.kModifyIncomingHeal:
            case SpellEffect.kSpellEffects.kModifyIncomingHealFlat:
            case SpellEffect.kSpellEffects.kModifyIncomingDamageType:
                ApplyHangingEffect(effect, targets);
                break;
            case SpellEffect.kSpellEffects.kStun:
                cinematicTime += ApplyStunEffect(effect, targets);
                break;
            default:
                break;
        }

        return cinematicTime;
    }

    private static float ApplyEffectDamage(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        int damageFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        if (!Enum.TryParse(typeof(MagicSchool), effect.m_sDamageType, out var damageTypeObj)) {
            throw new ArgumentException("Invalid damage type");
        }
        var damageType = (MagicSchool) damageTypeObj;

        // Calculate damage increase from caster stats.
        var damageFlatIncrease = GetFlatDamageIncrease(caster, damageType);
        var damagePercentIncrease = GetPercentDamageIncrease(caster, damageType);
        damagePercentIncrease = Math.Min(damagePercentIncrease, DAMAGE_PERCENT_MAX);
        damageFromCaster = (int) Math.Floor(damageFromCaster * (1 + damagePercentIncrease) + damageFlatIncrease);

        // Calculate damage changes from hanging effects.
        cinematicTime += CalculateBladeCinematicTime(effect.m_sDamageType, caster);
        damageFromCaster = ApplyBlades(effect.m_sDamageType, damageFromCaster, caster);

        // Apply damage to each target
        foreach (var target in targets) {
            var damage = damageFromCaster;

            // Calculate damage changes from target hanging effects.
            cinematicTime += CalculateWardCinematicTime(effect.m_sDamageType, target);
            damage = ApplyWards(effect.m_sDamageType, damage, target, out var finalDmgSchool);

            // Calculate global damage modifier
            var bubbleDamageIncrease = GetGlobalEffectDamageModifier(target, damageType);
            damage = (int) Math.Floor(damage * (1 + bubbleDamageIncrease));

            var finalDmgSchoolEnum = (MagicSchool) Enum.Parse(typeof(MagicSchool), finalDmgSchool);

            DoDamageToTarget(target, damage, finalDmgSchoolEnum);
        }

        return cinematicTime;
    }

    private static float ApplyEffectHeal(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        int healFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        // Calculate heal increase
        var percentOutgoingHealIncrease = GetPercentOutgoingHealIncrease(caster);
        healFromCaster = (int) Math.Ceiling(healFromCaster * (1 + percentOutgoingHealIncrease));

        // Apply heal to each target
        foreach (var target in targets) {
            DoHealToTarget(target, healFromCaster);
        }

        return cinematicTime;
    }

    private static float ApplyEffectStealHealth(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        int damageFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        if (!Enum.TryParse(typeof(MagicSchool), effect.m_sDamageType, out var damageTypeObj)) {
            throw new ArgumentException("Invalid damage type");
        }
        var damageType = (MagicSchool) damageTypeObj;

        // Calculate damage increase from caster stats.
        var damageFlatIncrease = GetFlatDamageIncrease(caster, damageType);
        var damagePercentIncrease = GetPercentDamageIncrease(caster, damageType);
        damagePercentIncrease = Math.Min(damagePercentIncrease, DAMAGE_PERCENT_MAX);

        // Calculate damage changes from hanging effects.
        cinematicTime += CalculateBladeCinematicTime(effect.m_sDamageType, caster);
        damageFromCaster = ApplyBlades(effect.m_sDamageType, damageFromCaster, caster);

        damageFromCaster = (int) Math.Floor(damageFromCaster * (1 + damagePercentIncrease) + damageFlatIncrease);

        // Apply damage to each target
        var damageDealt = 0;
        foreach (var target in targets) {
            var damage = damageFromCaster;

            // Calculate damage changes from target hanging effects.
            cinematicTime += CalculateWardCinematicTime(effect.m_sDamageType, target);
            damage = ApplyWards(effect.m_sDamageType, damageFromCaster, target, out var finalDmgSchool);

            // Calculate global damage modifier
            damage += (int) Math.Floor(damage * GetGlobalEffectDamageModifier(target, damageType));

            var finalDmgSchoolEnum = (MagicSchool) Enum.Parse(typeof(MagicSchool), finalDmgSchool);

            damageDealt += DoDamageToTarget(target, damage, finalDmgSchoolEnum);
        }

        var casterHealTotal = (int) Math.Floor(damageDealt * effect.m_healModifier);
        DoHealToTarget(caster, casterHealTotal);

        return cinematicTime;
    }

    private static void ApplyHangingEffect(SpellEffect effect, CombatDuelActorSubCircle[] targets) {
        foreach (var target in targets) {
            target.HangingEffects.Add(effect);
        }
    }

    private static float ApplyStunEffect(SpellEffect effect, CombatDuelActorSubCircle[] targets) {
        var cinematicTime = 0.0f;

        foreach (var target in targets) {
            // If this target is dead, do nothing.
            if (!target.IsAlive) {
                continue;
            }

            // Check to see if this target has a stun block.
            var spellBlock = target.HangingEffects.FirstOrDefault(x => x.m_effectType == SpellEffect.kSpellEffects.kStunBlock);
            if (spellBlock is not null) {
                // Remove the stun block and do nothing else.
                target.HangingEffects.Remove(spellBlock);
                cinematicTime += HANGING_EFFECT_CONSUME_TIME;
                continue;
            }

            if (target.TryStun()) {
                // Creature was stunned. Add a stun block hanging effect.
                var stunBlockEffect = new SpellEffect {
                    m_effectType = SpellEffect.kSpellEffects.kStunBlock,
                    m_spellTemplateID = effect.m_spellTemplateID,
                };

                target.HangingEffects.Add(stunBlockEffect);
            }
        }

        return cinematicTime;
    }

    private static void ApplyGlobalEffect(SpellEffect spellEffect, Duel duel) {
        // todo: not good. some bubbles give two effects.
        duel.m_duelModifier.m_battlefieldEffects.Clear();
        duel.m_duelModifier.m_battlefieldEffects.Add(spellEffect);
    }

    private static int ApplyBlades(string school, int damage, CombatDuelActorSubCircle caster) {
        var blades = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyOutgoingDamage)
            .Reverse()
            .ToList();

        var seen = new HashSet<uint>();
        foreach (var blade in blades.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            if (!seen.Add(blade.m_spellTemplateID)) {
                continue;
            }

            var damageChange = blade.m_effectParam / 100.0f;
            damage = (int) Math.Floor(damage * (1 + damageChange));

            caster.HangingEffects.Remove(blade);
        }

        return damage;
    }

    private static int ApplyWards(string school, int damage, CombatDuelActorSubCircle target, out string currentDmgSchool) {
        var wards = target.HangingEffects
            .Where(x => x.m_effectType is SpellEffect.kSpellEffects.kModifyIncomingDamage
                                       or SpellEffect.kSpellEffects.kModifyIncomingDamageType)
            .Reverse()
            .ToList();

        var seen = new HashSet<SpellEffect>();
        currentDmgSchool = school;
        foreach (var ward in wards) {
            // Check if this ward has already been applied.
            if (!seen.Add(ward)) {
                continue;
            }
            if (ward.m_sDamageType != currentDmgSchool && ward.m_sDamageType != "All") {
                continue;
            }

            // If this is a prism, we need to change the damage type.
            if (ward.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamageType) {
                currentDmgSchool = ((MagicSchool) ward.m_effectParam).ToString();
                target.HangingEffects.Remove(ward);
                continue;
            }

            var damageChange = ward.m_effectParam / 100.0f;
            damage = (int) Math.Floor(damage * (1 + damageChange));

            target.HangingEffects.Remove(ward);
        }

        return damage;
    }

    private static float CalculateBladeCinematicTime(string school, CombatDuelActorSubCircle caster) {
        var wards = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamage)
            .ToList();
        var cinematicTime = 0.0f;

        var seen = new HashSet<uint>();
        foreach (var ward in wards.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            if (!seen.Add(ward.m_spellTemplateID)) {
                continue;
            }

            cinematicTime += HANGING_EFFECT_CONSUME_TIME;
        }

        return cinematicTime;
    }

    private static float CalculateWardCinematicTime(string school, CombatDuelActorSubCircle caster) {
        var wards = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamage)
            .ToList();
        var cinematicTime = 0.0f;

        var seen = new HashSet<uint>();
        foreach (var ward in wards.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            if (!seen.Add(ward.m_spellTemplateID)) {
                continue;
            }

            cinematicTime += HANGING_EFFECT_CONSUME_TIME;
        }

        return cinematicTime;
    }

    private static int DoDamageToTarget(CombatDuelActorSubCircle target, int damage, MagicSchool damageType) {
        // Calculate damage reduction from target stats
        var damageReductionFlat = GetFlatDamageReduction(target, damageType);
        var damageReductionPercent = GetPercentDamageReduction(target, damageType);
        var reducedDamage = (int) Math.Floor(damage * (1 - damageReductionPercent) - damageReductionFlat);

        // Ensure that damage isn't negative.
        reducedDamage = Math.Max(reducedDamage, 0);

        target.DamageParticipant(reducedDamage);
        return reducedDamage;
    }

    private static int DoHealToTarget(CombatDuelActorSubCircle target, int heal) {
        var percentIncomingHealIncrease = GetPercentIncomingHealIncrease(target);
        heal = (int) Math.Ceiling(heal * (1 + percentIncomingHealIncrease));

        target.HealParticipant(heal);
        return heal;
    }

    private static float GetFlatDamageIncrease(CombatDuelActorSubCircle caster, MagicSchool damageType) {
        var damageFlatIncrease = caster.GetStatBySchool(caster.ParticipantGameStats.m_dmgBonusFlat, damageType);
        return damageFlatIncrease + caster.ParticipantGameStats.m_dmgBonusFlatAll;
    }

    private static float GetPercentDamageIncrease(CombatDuelActorSubCircle caster, MagicSchool damageType) {
        var damagePercentIncrease = caster.GetStatBySchool(caster.ParticipantGameStats.m_dmgBonusPercent, damageType);
        return damagePercentIncrease + caster.ParticipantGameStats.m_dmgBonusPercentAll;
    }

    private static float GetFlatDamageReduction(CombatDuelActorSubCircle target, MagicSchool damageType) {
        var damageReductionFlat = target.GetStatBySchool(target.ParticipantGameStats.m_dmgReduceFlat, damageType);
        return damageReductionFlat + target.ParticipantGameStats.m_dmgReduceFlatAll;
    }

    private static float GetPercentDamageReduction(CombatDuelActorSubCircle target, MagicSchool damageType) {
        var damageReductionPercent = target.GetStatBySchool(target.ParticipantGameStats.m_dmgReducePercent, damageType);
        return damageReductionPercent + target.ParticipantGameStats.m_dmgReducePercentAll;
    }

    private static float GetPercentOutgoingHealIncrease(CombatDuelActorSubCircle caster) {
        return caster.ParticipantGameStats.m_healBonusPercentAll;;
    }

    private static float GetPercentIncomingHealIncrease(CombatDuelActorSubCircle target) {
        return target.ParticipantGameStats.m_healIncBonusPercentAll;
    }

    private static float GetGlobalEffectDamageModifier(CombatDuelActorSubCircle target, MagicSchool damageType) {
        var sDamageType = damageType.ToString();
        var schoolBubble = target.DuelActor.Duel.m_duelModifier.m_battlefieldEffects
            .FirstOrDefault(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyOutgoingDamage
                              && x.m_sDamageType == sDamageType);

        if (schoolBubble is not null) {
            return schoolBubble.m_effectParam / 100.0f;
        }

        return 0.0f;
    }

    private static float GetGlobalEffectHealingModifier(CombatDuelActorSubCircle target) {
        var healBubble = target.DuelActor.Duel.m_duelModifier.m_battlefieldEffects
            .FirstOrDefault(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyOutgoingHeal);

        if (healBubble is not null) {
            return healBubble.m_effectParam / 100.0f;
        }

        return 0.0f;
    }
}
