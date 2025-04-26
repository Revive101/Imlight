/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * COMBAT SPELL TARGETING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Determines spell targets and processes the effects chain of combat actions,
 * bridging between the queued actions and their actual application to targets.
 * 
 * USAGE EXAMPLE:
 * bool spellWorthCasting = CombatActionResolver.ProcessedQueuedCombatAction(
 *     action, ref combatAction, ref cinematicTime);
 * 
 * NOTE:
 * Handles random spell selections, X-pip cost calculations, and manages
 * the effect stack for proper client synchronization.
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Processes queued combat actions and determines their effects during spell resolution.
/// </summary>
/// <remarks>
/// This utility class handles the selection of targets for specific effect types, resolves 
/// random effect choices, and processes X-pip spell costs. It coordinates with the CombatEffectApplicator
/// to apply the actual effects to targets.
/// </remarks>
internal static class CombatActionResolver {

    /// <summary>
    /// Processes a queued combat action and determines if it is worth casting.
    /// </summary>
    /// <param name="action">The queued combat action to process.</param>
    /// <param name="combatAction">The combat action to be modified.</param>
    /// <param name="cinematicTime">The cinematic time to be updated.</param>
    /// <returns>True if the spell is worth casting, otherwise false.</returns>
    internal static bool ProcessedQueuedCombatAction(QueuedCombatAction action, ref CombatAction combatAction, ref float cinematicTime) {
        var spellWorthCasting = false;
        var effectStack = new CombatEffectStack();
        var charmsAffectingThisSpell = new List<SpellEffect>();
        var allEffects = action.SpellTemplate.m_effects.ToList();
        combatAction.m_xPipCost = GetXPipCost(action.Spell, action.SpellCaster);

        foreach (var spellEffect in action.SpellTemplate.m_effects) {
            var chosenEffect = spellEffect;

            // A spell effect may ask us to choose a random effect from a list of effects.
            // If that's the case, we need to choose an effect and push the index of the chosen effect
            // to the effect stack.
            chosenEffect = spellEffect switch {
                RandomSpellEffect randomSpellEffect => ChooseRandomEffect(randomSpellEffect, effectStack),
                VariableSpellEffect variableSpellEffect => ChooseVariableEffect(variableSpellEffect, combatAction.m_xPipCost, effectStack),
                _ => spellEffect,
            };
            allEffects.Add(chosenEffect);

            charmsAffectingThisSpell = CombatCharms.FindAppliedCharms(action.SpellCaster, [.. allEffects]);

            var targets = GetEffectTargets(chosenEffect, action.SpellCaster, action.SelectedTarget);
            if (targets.Length == 0) {
                continue;
            }

            // If the spell has any targets that are alive or on the same team as the caster, it's worth casting.
            // If the spell has a global target, it's worth casting.
            if (!spellWorthCasting && (targets.Any(x => x.IsAlive)
                                    || targets.Any(x => x.OccupiedTeam == action.SpellCaster.OccupiedTeam))
                                    || chosenEffect.m_effectTarget == kEffectTarget.kGlobal) {
                spellWorthCasting = true;
            }

            // Inform each of the targets of this spell that they've been targeted by this effect.
            UpdateCombatActionTargets(ref combatAction, targets);
            InformDuelParticipantsOfEffect(action.SpellCaster, targets, chosenEffect);

            cinematicTime += CombatEffectApplicator.ApplyEffect(chosenEffect,
                                                                [.. charmsAffectingThisSpell],
                                                                action.SpellCaster,
                                                                targets);
        }

        // Remove all charms that were applied to this spell from the caster's hanging effects.
        action.SpellCaster._hangingEffects.RemoveAll(x => charmsAffectingThisSpell.Contains(x));
        combatAction.m_effectChosen = effectStack.GetStackAsUint();

        CheckForPolarCombatActionTargets(ref combatAction, action.SpellTemplate);

        return spellWorthCasting;
    }

    private static SpellEffect ChooseRandomEffect(RandomSpellEffect randomSpellEffect, CombatEffectStack effectStack) {
        var count = randomSpellEffect.m_effectList.Count;
        var randomEffectIndex = new Random().Next(0, count);
        var chosenEffect = randomSpellEffect.m_effectList[randomEffectIndex];

        effectStack.PushRandomEffectChoice(randomEffectIndex);

        return chosenEffect;
    }

    private static SpellEffect ChooseVariableEffect(VariableSpellEffect variableSpellEffect, int parameter, CombatEffectStack effectStack) {
        // Variable spell effects are for x pip spells. There should be a total of 14 nested spell effects
        // for each pip level of the spell.
        if (variableSpellEffect.m_effectList.Count != 14) {
            Logger.Error("Variable spell effect does not have 14 nested effects.");
            
            return variableSpellEffect;
        }

        // Make sure we're not out of bounds.
        parameter = Math.Min(parameter, 13);

        effectStack.PushRandomEffectChoice(parameter);
        var chosenEffect = variableSpellEffect.m_effectList[parameter];

        return chosenEffect;
    }

    private static byte GetXPipCost(Spell spell, CombatDuelSubCircle caster) {
        if (!spell.m_pipCost.m_xPipSpell) {
            return 0;
        }

        var pipCount = caster.CombatParticipant.m_pipCount;

        // x pip spells will consume all pips.
        var totalCost = pipCount.m_genericPips; ;

        var isSpellMastered = caster.HasSchoolMastery(spell.m_magicSchoolID);
        totalCost += isSpellMastered ? (byte) (pipCount.m_powerPips * 2) : pipCount.m_powerPips;

        return totalCost;
    }

    private static void UpdateCombatActionTargets(ref CombatAction combatAction, IEnumerable<CombatDuelSubCircle> targets) {
        foreach (var target in targets) {
            if (!combatAction.m_targetSubcircleList.Contains(target.SlotIndex)) {
                combatAction.m_targetSubcircleList.Add(target.SlotIndex);
            }
        }
    }

    private static void CheckForPolarCombatActionTargets(ref CombatAction combatAction, SpellTemplate spellTemplate) {
        // Spells that target both the caster and target(s) should be treated as targeting the target(s) only.
        // If the caster is left as a target, they will receive the benefit/curse from the spell twice.
        var casterIndex = combatAction.m_spellCaster;
        if (combatAction.m_targetSubcircleList.Count > 1
            && spellTemplate.m_effects.Any(x => x.m_effectTarget == kEffectTarget.kSelf)) {
            combatAction.m_targetSubcircleList.Remove(casterIndex);
        }
    }

    private static void InformDuelParticipantsOfEffect(CombatDuelSubCircle caster, CombatDuelSubCircle[] targets, SpellEffect effect) {
        var allParticipants = caster._duelActor.ActiveSubCircles.Select(x => x.ParticipantActor).ToArray();
        var msg = new COMBAT_106_PROTOCOL.MSG_COMBATEFFECT {
            Caster = caster,
            Targets = targets,
            Effect = effect,
        };

        foreach (var participant in allParticipants) {
            participant.Tell(msg, null);
        }
    }

    private static CombatDuelSubCircle[] GetEffectTargets(SpellEffect effect, CombatDuelSubCircle caster, CombatDuelSubCircle target) {
        var targets = Array.Empty<CombatDuelSubCircle>();
        var _activeSubCircles = caster._duelActor.ActiveSubCircles;

        switch (effect.m_effectTarget) {
            case kEffectTarget.kEnemySingle:
            case kEffectTarget.kFriendlySingle:
                targets = [target];
                break;
            case kEffectTarget.kSelf:
            case kEffectTarget.kInvalidTarget:
                targets = [caster];
                break;
            case kEffectTarget.kFriendlyTeam:
            case kEffectTarget.kFriendlyTeamAllAtOnce:
                targets = [.. _activeSubCircles.Where(x => x.OccupiedTeam == caster.OccupiedTeam)];
                break;
            case kEffectTarget.kEnemyTeam:
            case kEffectTarget.kEnemyTeamAllAtOnce:
                targets = [.. _activeSubCircles.Where(x => x.OccupiedTeam != caster.OccupiedTeam)];
                break;
            case kEffectTarget.kGlobal:
                return [];
        }

        return targets;
    }

}
