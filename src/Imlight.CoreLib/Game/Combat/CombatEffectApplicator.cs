/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using System;
using System.Linq;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Applies the effects of a spell to the targets.
/// </summary>
internal static class CombatEffectApplicator {

    private const float DAMAGE_PERCENT_MAX = 2.45f; // todo: not correct. this should be a limit function
    private const float HANGING_EFFECT_CONSUME_TIME = 1.0f;

    internal static float ApplyEffect(SpellEffect effect, SpellEffect[] charms, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        if (effect.m_effectTarget == kEffectTarget.kGlobal) {
            ApplyGlobalEffect(effect, caster._duelActor.Duel);
            return cinematicTime;
        }

        switch (effect.m_effectType) {
            case kSpellEffects.kDamage:
                cinematicTime += ApplyFlatDamageEffect(effect, charms, caster, targets);
                break;
            case kSpellEffects.kDamageOverTime:
                cinematicTime += ApplyDamageOverTime(effect, charms, caster, targets);
                break;
            case kSpellEffects.kHeal:
                cinematicTime += ApplyFlatHealEffect(effect, charms, caster, targets);
                break;
            case kSpellEffects.kHealOverTime:
                cinematicTime += ApplyHealOverTime(effect, charms, caster, targets);
                break;
            case kSpellEffects.kStealHealth:
                cinematicTime += ApplyStealHealthEffect(effect, charms, caster, targets);
                break;
            case kSpellEffects.kModifyOutgoingDamage:
            case kSpellEffects.kModifyIncomingDamage:
            case kSpellEffects.kDispel:
            case kSpellEffects.kModifyAccuracy:
            case kSpellEffects.kModifyOutgoingHeal:
            case kSpellEffects.kModifyOutgoingHealFlat:
            case kSpellEffects.kModifyIncomingHeal:
            case kSpellEffects.kModifyIncomingHealFlat:
            case kSpellEffects.kModifyIncomingDamageType:
            case kSpellEffects.kAbsorbDamage:
                ApplyHangingEffect(effect, targets);
                break;
            case kSpellEffects.kStun:
                cinematicTime += ApplyStunEffect(effect, targets);
                break;
            case kSpellEffects.kPacify:
            case kSpellEffects.kTaunt:
                // If you're looking for the implementation, we don't do it here. It happens in the CombatEffectProcessor, when the
                // actor is informed an effect has happened. The handler is in the CombatAIActor.
                break;
            case kSpellEffects.kReshuffle:
                ApplyReshuffleEffect(targets[0]);
                break;
            case kSpellEffects.kRemoveCharm:
                cinematicTime += ApplyRemoveCharmEffect(effect, targets);
                break;
            case kSpellEffects.kRemoveWard:
                cinematicTime += ApplyRemoveWardEffect(effect, targets);
                break;
            case kSpellEffects.kStealCharm:
                cinematicTime += ApplyStealCharmEffect(effect, caster, targets);
                break;
            case kSpellEffects.kStealWard:
                cinematicTime += ApplyStealWardEffect(effect, caster, targets);
                break;
            default:
                break;
        }

        return cinematicTime;
    }

    private static float ApplyFlatDamageEffect(SpellEffect effect, SpellEffect[] charms, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var (cinematicTime, _) = ApplyDamageEffect(effect, charms, caster, targets);
        return cinematicTime;
    }

    private static float ApplyDamageOverTime(SpellEffect effect, SpellEffect[] charms, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var damageFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        // Calculate damage increase from caster stats.
        var damageFlatIncrease = GetFlatDamageIncrease(caster, effect.m_sDamageType);
        var damagePercentIncrease = GetPercentDamageIncrease(caster, effect.m_sDamageType);
        damagePercentIncrease = Math.Min(damagePercentIncrease, DAMAGE_PERCENT_MAX);
        damageFromCaster = (int) Math.Floor(damageFromCaster * (1 + damagePercentIncrease) + damageFlatIncrease);

        // Calculate damage changes from hanging effects.
        cinematicTime += charms.Length * HANGING_EFFECT_CONSUME_TIME;
        damageFromCaster = CombatCharms.GetOutgoingDamageFromCharms(charms, damageFromCaster);

        // Calculate damage from current bubble
        var duel = caster._duelActor.Duel;
        var bubbleDamageIncrease = GetGlobalEffectDamageModifier(duel, effect.m_sDamageType);
        damageFromCaster = (int) Math.Floor(damageFromCaster * (1 + bubbleDamageIncrease));

        foreach (var target in targets) {
            var targetSpecificDamage = damageFromCaster;

            // Calculate damage reduction from target stats
            var damageReductionFlat = GetFlatDamageReduction(target, effect.m_sDamageType);
            var damageReductionPercent = GetPercentDamageReduction(target, effect.m_sDamageType);
            var reducedDamage = (int) Math.Floor(targetSpecificDamage * (1 - damageReductionPercent) - damageReductionFlat);

            // Ensure that damage isn't negative.
            reducedDamage = Math.Max(reducedDamage, 0);
            reducedDamage /= effect.m_numRounds;

            var effectClone = new SpellEffect {
                m_disposition = effect.m_disposition,
                m_numRounds = effect.m_numRounds,
                m_paramPerRound = reducedDamage,
                m_sDamageType = effect.m_sDamageType,
                m_effectType = effect.m_effectType,
            };
            target._hangingEffects.Add(effectClone);
        }

        effect.m_paramPerRound = damageFromCaster / effect.m_numRounds;

        return cinematicTime;
    }

    private static float ApplyHealOverTime(SpellEffect effect, SpellEffect[] charms, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var healFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        // Calculate heal increase from gear
        var percentOutgoingHealIncrease = GetPercentOutgoingHealIncrease(caster);
        healFromCaster = (int) Math.Ceiling(healFromCaster * (1 + percentOutgoingHealIncrease));

        // Calculate heal increase/decrease from hanging effects on caster
        cinematicTime += charms.Length * HANGING_EFFECT_CONSUME_TIME;
        healFromCaster = ApplyHealingCharms(charms, healFromCaster);

        // Calculate heal increase from current bubble
        var duel = caster._duelActor.Duel;
        var bubbleHealIncrease = GetGlobalEffectHealingModifier(duel);
        healFromCaster = (int) Math.Ceiling(healFromCaster * (1 + bubbleHealIncrease));

        foreach (var target in targets) {
            // Calculate heal increase from target stats
            var percentIncomingHealIncrease = GetPercentIncomingHealIncrease(target);
            var healPerTarget = (int) Math.Ceiling(healFromCaster * (1 + percentIncomingHealIncrease));
            healPerTarget /= effect.m_numRounds;

            var effectClone = new SpellEffect {
                m_disposition = effect.m_disposition,
                m_numRounds = effect.m_numRounds,
                m_paramPerRound = healPerTarget,
                m_effectType = effect.m_effectType,
            };

            target._hangingEffects.Add(effectClone);
        }

        return cinematicTime;
    }

    private static float ApplyStealHealthEffect(SpellEffect effect, SpellEffect[] charms, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var (cinematicTime, damageDealt) = ApplyDamageEffect(effect, charms, caster, targets);
        var casterHealTotal = (int) Math.Floor(damageDealt * effect.m_healModifier);
        DoHealToTarget(caster, casterHealTotal);
        return cinematicTime;
    }

    private static (float cinematicTime, int damageDealt) ApplyDamageEffect(SpellEffect effect,
                                                                            SpellEffect[] charms,
                                                                            CombatDuelSubCircle caster,
                                                                            CombatDuelSubCircle[] targets) {
        int damageFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        // Calculate damage increase from caster stats.
        var damageFlatIncrease = GetFlatDamageIncrease(caster, effect.m_sDamageType);
        var damagePercentIncrease = 1 + GetPercentDamageIncrease(caster, effect.m_sDamageType);
        damageFromCaster = (int) Math.Floor((damageFromCaster * damagePercentIncrease) + damageFlatIncrease);

        // Calculate damage changes from hanging effects.
        cinematicTime += charms.Length * HANGING_EFFECT_CONSUME_TIME;
        damageFromCaster = CombatCharms.GetOutgoingDamageFromCharms(charms, damageFromCaster);

        var duel = caster._duelActor.Duel;
        var bubbleDamageIncrease = GetGlobalEffectDamageModifier(duel, effect.m_sDamageType);
        damageFromCaster = (int) Math.Floor(damageFromCaster * (1 + bubbleDamageIncrease));

        // Apply damage to each target
        var damageDealt = 0;
        foreach (var target in targets) {
            var damage = damageFromCaster;

            // Calculate damage changes from target hanging effects.
            var wards = CombatWards.FindAppliedWards(target, effect);
            wards = CombatWards.GetWardsBySchool([.. wards], effect.m_sDamageType, out var finalSchool);
            cinematicTime += wards.Count * HANGING_EFFECT_CONSUME_TIME;
            damage = CombatWards.GetIncomingDamageFromWards(wards, damage);

            damageDealt += DoDamageToTarget(target, damage, finalSchool);

            // Remove the wards that were applied to this spell from the target's hanging effects.
            target._hangingEffects.RemoveAll(x => wards.Contains(x) && x.m_paramPerRound <= 0);
        }

        return (cinematicTime, damageDealt);
    }

    private static float ApplyFlatHealEffect(SpellEffect effect, SpellEffect[] charms, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        int healFromCaster = effect.m_effectParam;
        var cinematicTime = 0.0f;

        // Calculate heal increase from gear
        var percentOutgoingHealIncrease = GetPercentOutgoingHealIncrease(caster);
        healFromCaster = (int) Math.Ceiling(healFromCaster * (1 + percentOutgoingHealIncrease));

        // Calculate heal increase/decrease from hanging effects on caster
        cinematicTime += charms.Length * HANGING_EFFECT_CONSUME_TIME;
        healFromCaster = ApplyHealingCharms(charms, healFromCaster);

        // Calculate heal increase from current bubble
        var duel = caster._duelActor.Duel;
        var bubbleHealIncrease = GetGlobalEffectHealingModifier(duel);
        healFromCaster = (int) Math.Ceiling(healFromCaster * (1 + bubbleHealIncrease));

        // Apply heal to each target
        foreach (var target in targets) {
            _ = DoHealToTarget(target, healFromCaster);
        }

        return cinematicTime;
    }

    private static void ApplyReshuffleEffect(CombatDuelSubCircle caster) 
        => caster.Reshuffle();

    private static float ApplyRemoveCharmEffect(SpellEffect effect, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        // -1 is a special value that means remove all charms.
        var charmRemoveCount = effect.m_effectParam == -1 ? effect.m_effectParam = int.MaxValue : effect.m_effectParam;

        foreach (var target in targets) {
            var hangingEffects = target._hangingEffects.ToArray();
            var charmsInQuesiton = CombatCharms.FindAppliedCharms(target, hangingEffects, effect.m_disposition)
                                               .Take(charmRemoveCount);

            cinematicTime += charmsInQuesiton.Count() * HANGING_EFFECT_CONSUME_TIME;

            target._hangingEffects.RemoveAll(x => charmsInQuesiton.Contains(x));
        }

        return cinematicTime;
    }

    private static float ApplyRemoveWardEffect(SpellEffect effect, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        // -1 is a special value that means remove all wards.
        var wardRemoveCount = effect.m_effectParam == -1 ? effect.m_effectParam = int.MaxValue : effect.m_effectParam;

        foreach (var target in targets) {
            // Remove all wards except for stun blocks.
            var wards = CombatWards.FindAppliedWards(target, effect)
                                   .Where(x => x.m_effectType != kSpellEffects.kStunBlock)
                                   .Take(wardRemoveCount);

            cinematicTime += wards.Count() * HANGING_EFFECT_CONSUME_TIME;

            target._hangingEffects.RemoveAll(x => wards.Contains(x));
        }

        return cinematicTime;
    }

    private static float ApplyStealCharmEffect(SpellEffect effect, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        // -1 is a special value that means remove all charms.
        var charmRemoveCount = effect.m_effectParam == -1 ? effect.m_effectParam = int.MaxValue : effect.m_effectParam;

        // This is the same as the remove charm function, but we're moving the charms from the target to the caster.

        foreach (var target in targets) {
            var hangingEffects = target._hangingEffects.ToArray();
            var charmsInQuesiton = CombatCharms.FindAppliedCharms(target, hangingEffects, effect.m_disposition)
                                               .Take(charmRemoveCount);

            cinematicTime += charmsInQuesiton.Count() * HANGING_EFFECT_CONSUME_TIME;

            target._hangingEffects.RemoveAll(x => charmsInQuesiton.Contains(x));
            caster._hangingEffects.AddRange(charmsInQuesiton);
        }

        return cinematicTime;
    }

    private static float ApplyStealWardEffect(SpellEffect effect, CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        // -1 is a special value that means remove all wards.
        var wardRemoveCount = effect.m_effectParam == -1 ? effect.m_effectParam = int.MaxValue : effect.m_effectParam;

        // This is the same as the remove ward function, but we're moving the wards from the target to the caster.

        foreach (var target in targets) {
            var wards = CombatWards.FindAppliedWards(target, effect)
                                   .Take(wardRemoveCount);

            cinematicTime += wards.Count() * HANGING_EFFECT_CONSUME_TIME;

            target._hangingEffects.RemoveAll(x => wards.Contains(x));
            caster._hangingEffects.AddRange(wards);
        }

        return cinematicTime;
    }

    private static void ApplyHangingEffect(SpellEffect effect, CombatDuelSubCircle[] targets) {
        foreach (var target in targets) {
            // If this is an absorb ward, set the initial value.
            if (effect.m_effectType == kSpellEffects.kAbsorbDamage) {
                effect.m_paramPerRound = effect.m_effectParam;
            }

            target._hangingEffects.Add(effect);
        }
    }

    private static float ApplyStunEffect(SpellEffect effect, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        foreach (var target in targets) {
            // If this target is dead, do nothing.
            if (!target.IsAlive) {
                continue;
            }

            // Check to see if this target has a stun block.
            var spellBlock = target._hangingEffects.FirstOrDefault(x => x.m_effectType == kSpellEffects.kStunBlock);
            if (spellBlock is not null) {
                // Remove the stun block and do nothing else.
                target._hangingEffects.Remove(spellBlock);
                cinematicTime += HANGING_EFFECT_CONSUME_TIME;

                continue;
            }

            if (target.TryStun()) {
                // Creature was stunned. Add a stun block hanging effect.
                var stunBlockEffect = new SpellEffect {
                    m_effectType = kSpellEffects.kStunBlock,
                    m_spellTemplateID = effect.m_spellTemplateID,
                };

                target._hangingEffects.Add(stunBlockEffect);
            }
        }

        return cinematicTime;
    }

    private static void ApplyGlobalEffect(SpellEffect spellEffect, Duel duel) {
        // todo: not good. some bubbles give two effects.
        duel.m_duelModifier.m_battlefieldEffects.Clear();
        duel.m_duelModifier.m_battlefieldEffects.Add(spellEffect);
    }

    private static int ApplyHealingCharms(SpellEffect[] charms, int heal) {
        foreach (var charm in charms) {
            var healChange = charm.m_effectParam / 100.0f;
            heal = (int) Math.Floor(heal * (1 + healChange));
        }

        return heal;
    }

    private static int DoDamageToTarget(CombatDuelSubCircle target, int damage, string damageType) {
        // Calculate damage reduction from target stats
        var damageReductionFlat = GetFlatDamageReduction(target, damageType);
        var damageReductionPercent = GetPercentDamageReduction(target, damageType);
        var reducedDamage = (int) Math.Floor(damage * (1 - damageReductionPercent) - damageReductionFlat);

        // Ensure that damage isn't negative.
        reducedDamage = Math.Max(reducedDamage, 0);

        target.DamageParticipant(reducedDamage);

        return reducedDamage;
    }

    private static int DoHealToTarget(CombatDuelSubCircle target, int heal) {
        var percentIncomingHealIncrease = GetPercentIncomingHealIncrease(target);
        heal = (int) Math.Ceiling(heal * (1 + percentIncomingHealIncrease));
        target.HealParticipant(heal);

        return heal;
    }

    private static float GetFlatDamageIncrease(CombatDuelSubCircle caster, string damageType) {
        var damageFlatIncrease = caster.GetStatBySchool(caster.ParticipantGameStats.m_dmgBonusFlat, damageType);

        return damageFlatIncrease + caster.ParticipantGameStats.m_dmgBonusFlatAll;
    }

    private static float GetPercentDamageIncrease(CombatDuelSubCircle caster, string damageType) {
        var damagePercentIncrease = caster.GetStatBySchool(caster.ParticipantGameStats.m_dmgBonusPercent, damageType);
        damagePercentIncrease += caster.ParticipantGameStats.m_dmgBonusPercentAll;
        damagePercentIncrease = Math.Min(damagePercentIncrease, DAMAGE_PERCENT_MAX);

        return damagePercentIncrease;;
    }

    private static float GetFlatDamageReduction(CombatDuelSubCircle target, string damageType) {
        var damageReductionFlat = target.GetStatBySchool(target.ParticipantGameStats.m_dmgReduceFlat, damageType);

        return damageReductionFlat + target.ParticipantGameStats.m_dmgReduceFlatAll;
    }

    private static float GetPercentDamageReduction(CombatDuelSubCircle target, string damageType) {
        var damageReductionPercent = target.GetStatBySchool(target.ParticipantGameStats.m_dmgReducePercent, damageType);

        return damageReductionPercent + target.ParticipantGameStats.m_dmgReducePercentAll;
    }

    private static float GetPercentOutgoingHealIncrease(CombatDuelSubCircle caster) {
        return caster.ParticipantGameStats.m_healBonusPercentAll;;
    }

    private static float GetPercentIncomingHealIncrease(CombatDuelSubCircle target) 
        => target.ParticipantGameStats.m_healIncBonusPercentAll;

    private static float GetGlobalEffectDamageModifier(Duel duel, string damageType) {
        var schoolBubble = duel.m_duelModifier.m_battlefieldEffects
            .FirstOrDefault(x => x.m_effectType == kSpellEffects.kModifyOutgoingDamage
                              && x.m_sDamageType == damageType);

        if (schoolBubble is not null) {
            return schoolBubble.m_effectParam / 100.0f;
        }

        return 0.0f;
    }

    private static float GetGlobalEffectHealingModifier(Duel duel) {
        var healBubble = duel.m_duelModifier.m_battlefieldEffects
            .FirstOrDefault(x => x.m_effectType == kSpellEffects.kModifyOutgoingHeal);

        if (healBubble is not null) {
            return healBubble.m_effectParam / 100.0f;
        }

        return 0.0f;
    }

}
