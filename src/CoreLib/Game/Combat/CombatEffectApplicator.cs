/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal class CombatEffectApplicator {
    private const float DAMAGE_PERCENT_MAX = 2.0f;
    private const float HANGING_EFFECT_CONSUME_TIME = 1.0f;

    private readonly CombatDuelActorSubCircle[] _subCircles;
    private CombatDuelActorSubCircle[] _activeSubCircles => _subCircles.Where(x => x.AddedToDuel && x.IsAlive).ToArray();

    // ctor
    public CombatEffectApplicator(CombatDuelActorSubCircle[] actorSubCircles) {
        _subCircles = actorSubCircles;
    }

    internal float ApplyCombatAction(QueuedCombatAction action, out CombatAction combatAction) {
        var effectStack = new CombatEffectStack();
        var cinematicTime = 0.0f;

        if (action.Spell is not null) {
            foreach (var spellEffect in action.SpellTemplate.m_effects) {
                var effect = spellEffect;

                // If this is a random spell effect, we need to determine which effect to use.
                if (spellEffect is RandomSpellEffect randomSpellEffect) {
                    var count = randomSpellEffect.m_effectList.Count;
                    var randomEffectIndex = new Random().Next(0, count);
                    effect = randomSpellEffect.m_effectList[randomEffectIndex];

                    // Push the random effect choice onto the stack.
                    effectStack.PushRandomEffectChoice(randomEffectIndex);
                }

                cinematicTime += ApplyEffect(effect, action.SpellCaster, action.TargetSubcircle);
            }
        }

        combatAction = new CombatAction {
            m_effectChosen = effectStack.GetStackAsUint(),
            m_spellCaster = action.SpellCaster.SlotIndex,
            m_targetSubcircleList = new List<int> { action.TargetSubcircle.SlotIndex },
            m_showCast = true,
            m_spellHits = (char) 1, // Determines spell fizzel. 0 = fizzel, >=1 = hit
            m_spell = action.Spell,
        };

        return cinematicTime;
    }

    private float ApplyEffect(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target) {
        var targets = new CombatDuelActorSubCircle[] { target };
        var cinematicTime = 0.0f;

        switch (effect.m_effectTarget)
        {
            case SpellEffect.kEffectTarget.kEnemySingle:
            case SpellEffect.kEffectTarget.kFriendlySingle:
            case SpellEffect.kEffectTarget.kSelf:
                targets = new[] { target };
                break;
            case SpellEffect.kEffectTarget.kFriendlyTeam:
            case SpellEffect.kEffectTarget.kFriendlyTeamAllAtOnce:
                targets = _activeSubCircles.Where(x => x.OccupiedTeam == caster.OccupiedTeam).ToArray();
                break;
            case SpellEffect.kEffectTarget.kEnemyTeam:
            case SpellEffect.kEffectTarget.kEnemyTeamAllAtOnce:
                targets = _activeSubCircles.Where(x => x.OccupiedTeam != caster.OccupiedTeam).ToArray();
                break;
        }

        switch (effect.m_effectType) {
            case SpellEffect.kSpellEffects.kDamage:
                cinematicTime += ApplyEffectDamage(effect, caster, targets);
                break;
            case SpellEffect.kSpellEffects.kHeal:
                ApplyEffectHeal(effect, caster, targets);
                break;
            case SpellEffect.kSpellEffects.kModifyOutgoingDamage:
            case SpellEffect.kSpellEffects.kModifyIncomingDamage:
                ApplyHangingEffect(effect, caster, target);
                break;
            default:
                break;
        }

        return cinematicTime;
    }

    private static float ApplyEffectDamage(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        int damage = effect.m_effectParam;
        var cinematicTime = 0.0f;

        if (!Enum.TryParse(typeof(MagicSchool), effect.m_sDamageType, out var damageTypeObj)) {
            throw new ArgumentException("Invalid damage type");
        }
        var damageType = (MagicSchool) damageTypeObj;

        // Calculate damage increase from caster stats.
        double damageFlatIncrease = GetFlatDamageIncrease(caster, damageType);
        double damagePercentIncrease = GetPercentDamageIncrease(caster, damageType);
        damagePercentIncrease = Math.Min(damagePercentIncrease, DAMAGE_PERCENT_MAX);

        // Calculate damage changes from hanging effects.
        cinematicTime += CalculateBladeCinematicTime(effect.m_sDamageType, caster);
        damage = ApplyBlades(effect.m_sDamageType, damage, caster);

        damage = (int) Math.Floor(damage * (1 + damagePercentIncrease) + damageFlatIncrease);

        // Apply damage to each target
        foreach (var target in targets) {
            // Calculate damage reduction from target stats
            double damageReductionFlat = GetFlatDamageReduction(target, damageType);
            double damageReductionPercent = GetPercentDamageReduction(target, damageType);
            damage = (int) Math.Floor(damage * (1 - damageReductionPercent) - damageReductionFlat);

            // Calculate damage changes from target hanging effects.
            cinematicTime += CalculateWardCinematicTime(effect.m_sDamageType, target);
            damage = ApplyWards(effect.m_sDamageType, damage, target);

            target.ParticipantGameStats.m_currentHitpoints -= damage;
        }

        return cinematicTime;
    }

    private static void ApplyEffectHeal(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle[] targets) {
        int heal = effect.m_effectParam;

        // Calculate heal increase
        var percentOutgoingHealIncrease = GetPercentOutgoingHealIncrease(caster);
        heal = (int) Math.Ceiling(heal * (1 + percentOutgoingHealIncrease));

        // Apply heal to each target
        foreach (var target in targets) {
            var percentIncomingHealIncrease = GetPercentIncomingHealIncrease(target);
            heal = (int) Math.Ceiling(heal * (1 + percentIncomingHealIncrease));

            target.ParticipantGameStats.m_currentHitpoints += heal;
        }
    }

    private static void ApplyHangingEffect(SpellEffect effect, CombatDuelActorSubCircle caster, CombatDuelActorSubCircle target) {
        target.HangingEffects.Add(effect);
    }

    private static int ApplyBlades(string school, int damage, CombatDuelActorSubCircle caster) {
        var blades = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyOutgoingDamage)
            .ToList();

        foreach (var blade in blades.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            var damageChange = blade.m_effectParam / 100.0f;
            damage = (int) Math.Floor(damage * (1 + damageChange));

            caster.HangingEffects.Remove(blade);
        }

        return damage;
    }

    private static int ApplyWards(string school, int damage, CombatDuelActorSubCircle caster) {
        var wards = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamage)
            .ToList();

        foreach (var ward in wards.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            var damageChange = ward.m_effectParam / 100.0f;
            damage = (int) Math.Floor(damage * (1 + damageChange));

            caster.HangingEffects.Remove(ward);
        }

        return damage;
    }

    private static float CalculateBladeCinematicTime(string school, CombatDuelActorSubCircle caster) {
        var wards = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamage)
            .ToList();
        var cinematicTime = 0.0f;

        foreach (var ward in wards.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            cinematicTime += HANGING_EFFECT_CONSUME_TIME;
        }

        return cinematicTime;
    }

    private static float CalculateWardCinematicTime(string school, CombatDuelActorSubCircle caster) {
        var wards = caster.HangingEffects
            .Where(x => x.m_effectType == SpellEffect.kSpellEffects.kModifyIncomingDamage)
            .ToList();
        var cinematicTime = 0.0f;

        foreach (var ward in wards.Where(x => x.m_sDamageType == school || x.m_sDamageType == "All")) {
            cinematicTime += HANGING_EFFECT_CONSUME_TIME;
        }

        return cinematicTime;
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
}
