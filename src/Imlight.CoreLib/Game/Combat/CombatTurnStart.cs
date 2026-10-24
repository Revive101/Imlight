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
 * COMBAT TURN START
 * ========================================================================
 *
 * PURPOSE:
 * Runs what happens to a participant when its action comes up, before the
 * action resolves: deferred damage counts down and lands, auto-pass turns the
 * action into a pass, confusion may turn the spell on the other side, and the
 * confusion and clue rounds count down.
 *
 * USAGE EXAMPLE:
 * cinematicTime += CombatTurnStart.Apply(action);
 *
 * NOTE:
 * The order follows the client's round loop: deferred damage, then the
 * auto-pass check, then confusion, then the clue.
 *
 * TODO:
 * - A confused team spell still hits the side it was cast at; the client also turns team target types.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Spells;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Applies the start-of-turn rules to the participant whose action is about to resolve.
/// </summary>
internal static class CombatTurnStart {

    /// <summary>
    /// Applies the start-of-turn rules to an action's caster. May clear the action's spell (a pass) or change its target.
    /// </summary>
    /// <param name="action">The action about to resolve.</param>
    /// <returns>The cinematic time the start of the turn adds.</returns>
    internal static float Apply(QueuedCombatAction action) {
        var caster = action.SpellCaster;
        if (caster?.CombatParticipant is null) {
            return 0.0f;
        }

        var cinematicTime = TickDeferredDamage(caster);
        if (!caster.IsAlive) {
            return cinematicTime;
        }

        if (caster.CombatParticipant.m_autoPass) {
            action.Spell = null;

            return cinematicTime;
        }

        ApplyConfusion(action);
        TickClue(caster);

        return cinematicTime;
    }

    private static float TickDeferredDamage(CombatDuelSubCircle caster) {
        // Each turn takes a round off; the whole amount lands as one tick through the carrier's wards when none are left.
        var deferred = caster._hangingEffects
            .Where(x => x.m_effectType == kSpellEffects.kDeferredDamage)
            .ToList();
        var cinematicTime = 0.0f;

        foreach (var effect in deferred) {
            effect.m_numRounds--;
            if (effect.m_numRounds > 0) {
                continue;
            }

            caster._hangingEffects.Remove(effect);
            cinematicTime += SpellCinematics.GetOverTimeTickTime()
                           + CombatHangingEffects.ApplyOverTimeTick(caster, effect, effect.m_effectParam);
        }

        return cinematicTime;
    }

    private static void ApplyConfusion(QueuedCombatAction action) {
        var caster = action.SpellCaster;
        var participant = caster.CombatParticipant;
        var duel = caster._duelActor.Duel;
        participant.m_confusedTarget = false;

        if (participant.m_confused < 1 || CombatStatusEffects.WasConfusedInRound(caster, duel.m_roundNum)) {
            participant.m_confusionDisplay = false;

            return;
        }

        if (action.Spell is not null && action.SelectedTarget is not null && RollsConfused(participant)) {
            RetargetConfusedAction(action);
        }

        participant.m_confused--;
        participant.m_confusionDisplay = true;
    }

    private static bool RollsConfused(CombatParticipant participant)
        => Random.Shared.Next(1, 101) <= participant.m_confusionTrigger;

    private static void RetargetConfusedAction(QueuedCombatAction action) {
        var caster = action.SpellCaster;
        var castsAtSelf = action.SpellTemplate?.m_effects?.All(x => x.m_effectTarget == kEffectTarget.kSelf) ?? true;
        if (castsAtSelf) {
            return;
        }

        // A spell aimed at an ally goes to an enemy and the other way round.
        var aimedAtAlly = action.SelectedTarget.CurrentTeam == caster.CurrentTeam;
        var candidates = caster._duelActor.ActiveSubCircles
            .Where(x => x.IsAlive && x.AddedToDuel && (x.CurrentTeam == caster.CurrentTeam) != aimedAtAlly)
            .ToArray();
        if (candidates.Length == 0) {
            return;
        }

        action.SelectedTarget = candidates[Random.Shared.Next(candidates.Length)];
        caster.CombatParticipant.m_confusedTarget = true;

        Logger.Debug("Duel {0} | Slot {1} | Confused, spell turned on slot {2}",
            Logger.Args(caster._duelActor.Duel.m_duelID.Full, caster.SlotIndex, action.SelectedTarget.SlotIndex));
    }

    private static void TickClue(CombatDuelSubCircle caster) {
        if (caster.CombatParticipant.m_clue > 0) {
            caster.CombatParticipant.m_clue--;
        }
    }

}
