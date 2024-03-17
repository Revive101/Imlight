/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal static class CombatEffectApplicator {
    private const float DAMAGE_PERCENT_MAX = 2.0f;

    internal static CombatAction ApplyCombatAction(QueuedCombatAction action) {
        var effectStack = new CombatEffectStack();

        if (action.Spell is not null) {
            foreach (var spellEffect in action.Spell.m_spellEffects) {
                var effect = spellEffect;

                // If this is a random spell effect, we need to determine which effect to use.
                if (spellEffect is RandomSpellEffect randomSpellEffect) {
                    var count = randomSpellEffect.m_effectList.Count;
                    var randomEffectIndex = new Random().Next(0, count);
                    effect = randomSpellEffect.m_effectList[randomEffectIndex];

                    // Push the random effect choice onto the stack.
                    effectStack.PushRandomEffectChoice(randomEffectIndex);
                }

                ApplyEffect(effect, action.SpellCaster, action.TargetSubcircle);
            }
        }

        return new CombatAction {
            m_effectChosen = effectStack.GetStackAsUint(),
            m_spellCaster = action.SpellCaster.SlotIndex,
            m_targetSubcircleList = new List<int> { action.TargetSubcircle.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 1, // Determines spell fizzel. 0 = fizzel, >=1 = hit
            m_spell = action.Spell,
        };
    }

    private static void ApplyEffect(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target) {
        var effectTarget = effect.m_effectTarget;

        var targets = new CombatDuelActorSubCircle[] { target };
        if (effectTarget == SpellEffect.kEffectTarget.kEnemySingle
         || effectTarget == SpellEffect.kEffectTarget.kFriendlySingle) {
            targets = new[] { target };
        }

        var effectType = effect.m_effectType;

        switch (effectType) {
            case SpellEffect.kSpellEffects.kDamage:
                ApplyEffectDamage(effect, caster, targets);
                break;
            default:
                break;
        }
    }

    private static void ApplyEffectDamage(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        int damage = effect.m_effectParam;

        // Try to parse the string to an enum
        if (!Enum.TryParse(typeof(MagicSchool), effect.m_sDamageType, out var damageTypeObj)) {
            throw new ArgumentException("Invalid damage type");
        }
        var damageType = (MagicSchool) damageTypeObj;

        // Calculate damage increase
        double damageFlatIncrease = GetFlatDamageIncrease(caster, damageType);
        double damagePercentIncrease = GetPercentDamageIncrease(caster, damageType);

        // Clamp percent increase
        damagePercentIncrease = Math.Min(damagePercentIncrease, DAMAGE_PERCENT_MAX);

        damage = (int) Math.Ceiling(damage * (1 + damagePercentIncrease) + damageFlatIncrease);

        // Apply damage to each target
        foreach (var target in targets) {
            // Calculate damage reduction
            double damageReductionFlat = GetFlatDamageReduction(target, damageType);
            double damageReductionPercent = GetPercentDamageReduction(target, damageType);
            damage = (int) Math.Ceiling(damage * (1 - damageReductionPercent) - damageReductionFlat);

            target.ParticipantGameStats.m_currentHitpoints -= damage;
        }
    }

    private static double GetFlatDamageIncrease(CombatDuelActorSubCircle caster, MagicSchool damageType) {
        double damageFlatIncrease = caster.GetStatBySchool(caster.ParticipantGameStats.m_dmgBonusFlat, damageType);
        return damageFlatIncrease + caster.ParticipantGameStats.m_dmgBonusFlatAll;
    }

    private static double GetPercentDamageIncrease(CombatDuelActorSubCircle caster, MagicSchool damageType) {
        double damagePercentIncrease = caster.GetStatBySchool(caster.ParticipantGameStats.m_dmgBonusPercent, damageType);
        return damagePercentIncrease + caster.ParticipantGameStats.m_dmgBonusPercentAll;
    }

    private static double GetFlatDamageReduction(CombatDuelActorSubCircle target, MagicSchool damageType) {
        double damageReductionFlat = target.GetStatBySchool(target.ParticipantGameStats.m_dmgReduceFlat, damageType);
        return damageReductionFlat + target.ParticipantGameStats.m_dmgReduceFlatAll;
    }

    private static double GetPercentDamageReduction(CombatDuelActorSubCircle target, MagicSchool damageType) {
        double damageReductionPercent = target.GetStatBySchool(target.ParticipantGameStats.m_dmgReducePercent, damageType);
        return damageReductionPercent + target.ParticipantGameStats.m_dmgReducePercentAll;
    }
}
