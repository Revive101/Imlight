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
using Imlight.CoreLib.Shared.Resources;
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
                EffectListSpellEffect effectListSpellEffect => ChooseFromEffectList(effectListSpellEffect, combatAction.m_xPipCost, effectStack),
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
        var randomEffectIndex = Random.Shared.Next(0, count);
        var chosenEffect = randomSpellEffect.m_effectList[randomEffectIndex];

        effectStack.PushRandomEffectChoice(randomEffectIndex);

        return chosenEffect;
    }

    private static SpellEffect ChooseVariableEffect(VariableSpellEffect variableSpellEffect, int parameter, CombatEffectStack effectStack) {
        // X-pip spells carry one nested effect per pip tier, tagged by m_pipNum (1-based); resolve
        // the highest tier the pips paid can afford, for any list length.
        var effects = variableSpellEffect.m_effectList;
        if (effects is null || effects.Count == 0) {
            Logger.Error("Variable spell effect has no nested effects; cannot resolve.");

            return variableSpellEffect;
        }

        var pipsPaid = Math.Max(parameter, 0);
        var chosenEffect = effects
            .Where(e => e.m_pipNum > 0 && e.m_pipNum <= pipsPaid)
            .OrderByDescending(e => e.m_pipNum)
            .FirstOrDefault();
        if (chosenEffect is null) {
            // Defensive: no matching tier, clamp by index so a mistagged spell still resolves.
            var idx = Math.Clamp(pipsPaid == 0 ? 0 : pipsPaid - 1, 0, effects.Count - 1);
            chosenEffect = effects[idx];
        }

        // Tell the client which tier resolved; it re-simulates the spell.
        effectStack.PushRandomEffectChoice(effects.IndexOf(chosenEffect));

        return chosenEffect;
    }

    private static SpellEffect ChooseFromEffectList(EffectListSpellEffect effect, int pipCost, CombatEffectStack effectStack) {
        // Selects a nested effect from an EffectListSpellEffect by pip count.
        // EffectListSpellEffect entries are 1-indexed via m_pipNum: entry 0 = 1 pip, entry 4 = 5 pips.
        var idx = Math.Max(0, pipCost - 1);
        if (idx >= effect.m_effectList.Count) {
            idx = effect.m_effectList.Count - 1;
        }

        effectStack.PushRandomEffectChoice(idx);

        return effect.m_effectList[idx];
    }

    // X-pip spells are detected structurally: the template carries a VariableSpellEffect.
    internal static bool IsXPipSpell(Spell spell) {
        var template = CoreObjectFactory.GetCoreTemplate(spell.m_templateID) as SpellTemplate;

        return template?.m_effects?.Any(e => e is VariableSpellEffect) == true;
    }

    internal static byte GetXPipCost(Spell spell, CombatDuelSubCircle caster) {
        if (!IsXPipSpell(spell)) {
            return 0;
        }

        var pipCount = caster.CombatParticipant.m_pipCount;

        // All pips, capped at the spell's top tier (a minion stops at 4).
        var totalPips = (int) pipCount.m_genericPips;
        var isSpellMastered = caster.HasSchoolMastery(spell.m_magicSchoolID);
        totalPips += isSpellMastered ? pipCount.m_powerPips * 2 : pipCount.m_powerPips;

        return (byte) Math.Min(totalPips, GetMaxXPipTier(spell));
    }

    // Highest pip tier (m_pipNum) an X-pip spell offers; int.MaxValue when there is none.
    private static int GetMaxXPipTier(Spell spell) {
        var template = CoreObjectFactory.GetCoreTemplate(spell.m_templateID) as SpellTemplate;
        var variable = template?.m_effects?.OfType<VariableSpellEffect>().FirstOrDefault();
        if (variable?.m_effectList is null || variable.m_effectList.Count == 0) {
            return int.MaxValue;
        }

        return variable.m_effectList.Max(e => (int) e.m_pipNum);
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
