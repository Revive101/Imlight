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
 * COMBAT STATUS EFFECTS
 * ========================================================================
 *
 * PURPOSE:
 * Applies the effects that set a participant's combat state instead of its
 * health: dispels that a dispel block can stop, confusion, auto-pass, clues
 * and forced targetability.
 *
 * USAGE EXAMPLE:
 * cinematicTime += CombatStatusEffects.Apply(effect, caster, targets, context);
 *
 * NOTE:
 * CombatTurnStart counts the confusion and clue rounds down and acts on
 * auto-pass and confusion when the participant's turn comes.
 *
 * TODO:
 *
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Linq;
using System.Runtime.CompilerServices;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Applies the effects that change a participant's combat state.
/// </summary>
internal static class CombatStatusEffects {

    private static readonly ConditionalWeakTable<CombatDuelSubCircle, StrongBox<int>> s_confusionRound = new();

    /// <summary>
    /// Applies a status effect to its targets.
    /// </summary>
    /// <param name="effect">The spell effect to apply.</param>
    /// <param name="caster">The caster of the spell.</param>
    /// <param name="targets">The targets of the spell effect.</param>
    /// <param name="context">The per-action values.</param>
    /// <returns>The cinematic time the effect adds.</returns>
    internal static float Apply(SpellEffect effect,
                                CombatDuelSubCircle caster,
                                CombatDuelSubCircle[] targets,
                                CombatEffectContext context) {
        var cinematicTime = 0.0f;

        foreach (var target in targets) {
            if (target?.CombatParticipant is null) {
                continue;
            }

            cinematicTime += ApplyToTarget(effect, caster, target, context);
        }

        return cinematicTime;
    }

    /// <summary>
    /// Whether the participant's confusion was applied during the given round, so its turn must not count it down yet.
    /// </summary>
    /// <param name="participant">The confused participant.</param>
    /// <param name="round">The current duel round.</param>
    /// <returns>True when the confusion is new this round.</returns>
    internal static bool WasConfusedInRound(CombatDuelSubCircle participant, int round)
        => s_confusionRound.TryGetValue(participant, out var applied) && applied.Value == round;

    private static float ApplyToTarget(SpellEffect effect,
                                       CombatDuelSubCircle caster,
                                       CombatDuelSubCircle target,
                                       CombatEffectContext context) {
        var participant = target.CombatParticipant;

        switch (effect.m_effectType) {
            case kSpellEffects.kDispel:
                return ApplyDispel(effect, target, context);
            case kSpellEffects.kConfusion:
                return ApplyConfusion(effect, caster, target);
            case kSpellEffects.kAutoPass:
            case kSpellEffects.kStopAutoPass:
                participant.m_autoPass = effect.m_effectType == kSpellEffects.kAutoPass;

                return 0.0f;
            case kSpellEffects.kClue:
                // A shorter clue never replaces a longer one.
                if (participant.m_clue < effect.m_effectParam) {
                    participant.m_clue = effect.m_effectParam;
                }

                return 0.0f;
            case kSpellEffects.kForceTargetable:
                participant.m_untargetable = false;
                participant.m_untargetableRounds = 0;

                return 0.0f;
            default:
                return 0.0f;
        }
    }

    private static float ApplyDispel(SpellEffect effect, CombatDuelSubCircle target, CombatEffectContext context) {
        if (TakeBlock(target, kSpellEffects.kDispelBlock)) {
            return CombatHangingEffects.HANGING_EFFECT_CONSUME_TIME;
        }

        target._hangingEffects.Add(CombatHangingEffects.CopyForTarget(effect, context.SpellTemplateId));

        return target.IsAlive ? CombatHangingEffects.HANGING_EFFECT_ADD_TIME : 0.0f;
    }

    private static float ApplyConfusion(SpellEffect effect, CombatDuelSubCircle caster, CombatDuelSubCircle target) {
        var participant = target.CombatParticipant;

        // The client ignores a new confusion while one is running, and never confuses a monster.
        if (participant.m_confused > 0 || participant.m_isMonster != 0) {
            return 0.0f;
        }

        if (TakeBlock(target, kSpellEffects.kConfusionBlock)) {
            return CombatHangingEffects.HANGING_EFFECT_CONSUME_TIME;
        }

        participant.m_confused = effect.m_numRounds;
        participant.m_confusionTrigger = effect.m_effectParam;
        s_confusionRound.AddOrUpdate(target, new StrongBox<int>(caster._duelActor.Duel.m_roundNum));

        Logger.Debug("Duel {0} | Slot {1} | Confused for {2} rounds at {3} percent",
            Logger.Args(caster._duelActor.Duel.m_duelID.Full, target.SlotIndex, effect.m_numRounds, effect.m_effectParam));

        return 0.0f;
    }

    private static bool TakeBlock(CombatDuelSubCircle target, kSpellEffects blockType) {
        var block = target._hangingEffects.LastOrDefault(x => x.m_effectType == blockType);
        if (block is null) {
            return false;
        }

        target._hangingEffects.Remove(block);

        return true;
    }

}
