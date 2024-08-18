/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal static class CombatActionResolver {
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

            charmsAffectingThisSpell = CombatCharms.FindAppliedCharms(action.SpellCaster, allEffects.ToArray());

            var targets = GetEffectTargets(chosenEffect, action.SpellCaster, action.SelectedTarget);
            if (targets.Length == 0) {
                continue;
            }

            // If the spell has any targets that are alive or on the same team as the caster, it's worth casting.
            // If the spell has a global target, it's worth casting.
            if (!spellWorthCasting && (targets.Any(x => x.IsAlive)
                                    || targets.Any(x => x.OccupiedTeam == action.SpellCaster.OccupiedTeam))
                                    || chosenEffect.m_effectTarget == SpellEffect.kEffectTarget.kGlobal) {
                spellWorthCasting = true;
            }

            // Inform each of the targets of this spell that they've been targeted by this effect.
            UpdateCombatActionTargets(ref combatAction, targets);
            InformDuelParticipantsOfEffect(action.SpellCaster, targets, chosenEffect);

            cinematicTime += CombatEffectApplicator.ApplyEffect(chosenEffect,
                                                                charmsAffectingThisSpell.ToArray(),
                                                                action.SpellCaster,
                                                                targets);
        }

        // Remove all charms that were applied to this spell from the caster's hanging effects.
        action.SpellCaster._hangingEffects.RemoveAll(x => charmsAffectingThisSpell.Contains(x));

        combatAction.m_effectChosen = effectStack.GetStackAsUint();
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
        var totalCost = pipCount.m_genericPips;;

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
            case SpellEffect.kEffectTarget.kEnemySingle:
            case SpellEffect.kEffectTarget.kFriendlySingle:
                targets = new[] { target };
                break;
            case SpellEffect.kEffectTarget.kSelf:
            case SpellEffect.kEffectTarget.kInvalidTarget:
                targets = new[] { caster };
                break;
            case SpellEffect.kEffectTarget.kFriendlyTeam:
            case SpellEffect.kEffectTarget.kFriendlyTeamAllAtOnce:
                targets = _activeSubCircles.Where(x => x.OccupiedTeam == caster.OccupiedTeam).ToArray();
                break;
            case SpellEffect.kEffectTarget.kEnemyTeam:
            case SpellEffect.kEffectTarget.kEnemyTeamAllAtOnce:
                targets = _activeSubCircles.Where(x => x.OccupiedTeam != caster.OccupiedTeam).ToArray();
                break;
            case SpellEffect.kEffectTarget.kGlobal:
                return Array.Empty<CombatDuelSubCircle>();
        }

        return targets;
    }
}
