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
 * COMBAT PARTICIPANT STATE SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Sets the participant flags that spell effects switch on and off: untargetable,
 * vanished and suspended pips, and takes stun blocks off a participant.
 *
 * USAGE EXAMPLE:
 * CombatParticipantStates.Apply(effect, targets);
 * CombatParticipantStates.EndTurn(caster);
 *
 * NOTE:
 * The flags live on the CombatParticipant the client reads. An untargetable participant
 * can only be targeted by itself; its rounds count down at the end of its own turn, and
 * zero rounds means it stays untargetable until an effect makes it targetable again.
 * Only players have their pips suspended.
 *
 * TODO:
 * - A suspend also clears the pip round rates; the server keeps none yet.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Participant flags set by spell effects.
/// </summary>
internal static class CombatParticipantStates {

    /// <summary>
    /// Whether the effect type sets a participant flag or removes stun blocks.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <returns>True for the effect types this type applies.</returns>
    internal static bool Handles(kSpellEffects effectType) => effectType is kSpellEffects.kUntargetable
                                                                         or kSpellEffects.kMakeTargetable
                                                                         or kSpellEffects.kForceTargetable
                                                                         or kSpellEffects.kVanish
                                                                         or kSpellEffects.kStopVanish
                                                                         or kSpellEffects.kSuspendPips
                                                                         or kSpellEffects.kResumePips
                                                                         or kSpellEffects.kStopAutoPass
                                                                         or kSpellEffects.kRemoveStunBlock;

    /// <summary>
    /// Applies the effect to every target that is in the duel.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <param name="targets">The targets of the effect.</param>
    /// <returns>The cinematic time of the removed stun blocks.</returns>
    internal static float Apply(SpellEffect effect, CombatDuelSubCircle[] targets) {
        var cinematicTime = 0.0f;

        foreach (var target in targets.Where(x => x.CombatParticipant is not null)) {
            cinematicTime += ApplyTo(effect, target);
        }

        return cinematicTime;
    }

    /// <summary>
    /// Whether a participant can be picked as the target of another participant's spell.
    /// </summary>
    /// <param name="target">The participant to target.</param>
    /// <returns>False while the participant is untargetable.</returns>
    internal static bool CanBeTargeted(CombatDuelSubCircle target) => target?.CombatParticipant?.m_untargetable != true;

    /// <summary>
    /// Drops the untargetable participants from an effect's targets, except the caster itself.
    /// </summary>
    /// <param name="targets">The effect's targets.</param>
    /// <param name="caster">The caster of the effect.</param>
    /// <returns>The targets the effect can reach.</returns>
    internal static CombatDuelSubCircle[] WithoutUntargetable(CombatDuelSubCircle[] targets, CombatDuelSubCircle caster)
        => [.. targets.Where(x => x == caster || CanBeTargeted(x))];

    /// <summary>
    /// Counts down the untargetable rounds of a participant whose turn ended.
    /// </summary>
    /// <param name="caster">The participant whose turn ended.</param>
    internal static void EndTurn(CombatDuelSubCircle caster) {
        var participant = caster.CombatParticipant;
        if (participant is null || !participant.m_untargetable || participant.m_untargetableRounds < 1) {
            return;
        }

        participant.m_untargetableRounds--;
        if (participant.m_untargetableRounds > 0) {
            return;
        }

        participant.m_untargetable = false;
        participant.m_untargetableRounds = 0;
    }

    private static float ApplyTo(SpellEffect effect, CombatDuelSubCircle target) {
        var participant = target.CombatParticipant;

        switch (effect.m_effectType) {
            case kSpellEffects.kUntargetable:
                participant.m_untargetable = true;
                participant.m_untargetableRounds = effect.m_numRounds;

                break;
            case kSpellEffects.kMakeTargetable:
            case kSpellEffects.kForceTargetable:
                participant.m_untargetable = false;
                participant.m_untargetableRounds = 0;

                break;
            case kSpellEffects.kVanish:
            case kSpellEffects.kStopVanish:
                participant.m_vanish = effect.m_effectType == kSpellEffects.kVanish;

                break;
            case kSpellEffects.kRemoveStunBlock:
                return RemoveStunBlocks(target, effect.m_effectParam);
            case kSpellEffects.kSuspendPips:
            case kSpellEffects.kResumePips:
            case kSpellEffects.kStopAutoPass:
                SetPipsSuspended(participant, effect.m_effectType == kSpellEffects.kSuspendPips);

                break;
        }

        return 0.0f;
    }

    private static void SetPipsSuspended(CombatParticipant participant, bool suspended) {
        if (!participant.m_isPlayer) {
            return;
        }

        if (suspended) {
            participant.m_pipRoundRates?.Clear();
        }

        participant.m_PipsSuspended = suspended;
    }

    private static float RemoveStunBlocks(CombatDuelSubCircle target, int count) {
        var hanging = target._hangingEffects;
        var removed = 0;

        while (count < 0 || removed < count) {
            var index = hanging.FindLastIndex(x => x.m_effectType == kSpellEffects.kStunBlock);
            if (index < 0) {
                break;
            }

            hanging.RemoveAt(index);
            removed++;
        }

        return removed * CombatHangingEffects.HANGING_EFFECT_CONSUME_TIME;
    }

}
