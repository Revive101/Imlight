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
 * COMBAT OVER-TIME DURATION SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Lengthens, shortens and ends the over-time effects hanging on a target, as the
 * client's modify-over-time-duration and reduce-over-time handlers do.
 *
 * USAGE EXAMPLE:
 * var cinematicTime = CombatOverTimeDurations.Apply(effect, targets);
 *
 * NOTE:
 * Both handlers first move an untouched over-time effect into the ticking layout
 * (CombatHangingEffects.AdvanceOverTime), so m_effectParam counts the ticks left.
 * An effect with no ticks left is removed.
 *
 * TODO:
 * - The client walks the hanging list from its head; that its head is the oldest effect is unconfirmed.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Changes the tick count of the over-time effects hanging on a target.
/// </summary>
internal static class CombatOverTimeDurations {

    private const int UNLIMITED_DURATION_CHANGES = 999;

    /// <summary>
    /// Whether the effect type changes over-time durations.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <returns>True for a modify-over-time-duration or reduce-over-time effect.</returns>
    internal static bool Handles(kSpellEffects effectType) => effectType is kSpellEffects.kModifyOverTimeDuration
                                                                         or kSpellEffects.kReduceOverTime;

    /// <summary>
    /// Changes the ticks left of the matching over-time effects on every target. A modify-over-time-duration
    /// effect adds its m_numRounds to up to m_effectParam effects (all when below 1); a reduce-over-time effect
    /// takes one tick off up to m_effectParam effects (all when negative).
    /// </summary>
    /// <param name="effect">The duration effect; m_disposition picks the over-time effects it changes.</param>
    /// <param name="targets">The targets of the effect.</param>
    /// <returns>The cinematic time of the changed effects.</returns>
    internal static float Apply(SpellEffect effect, CombatDuelSubCircle[] targets) {
        var isModify = effect.m_effectType == kSpellEffects.kModifyOverTimeDuration;
        if (isModify && effect.m_numRounds == 0) {
            return 0.0f;
        }

        var tickChange = isModify ? effect.m_numRounds : -1;
        var limit = isModify && effect.m_effectParam < 1 ? UNLIMITED_DURATION_CHANGES : effect.m_effectParam;
        var changed = 0;

        foreach (var target in targets) {
            changed += ChangeTicks(target, effect.m_disposition, tickChange, limit);
        }

        return changed * CombatHangingEffects.HANGING_EFFECT_CONSUME_TIME;
    }

    private static int ChangeTicks(CombatDuelSubCircle target, kHangingDisposition disposition, int tickChange, int limit) {
        var hanging = target._hangingEffects;
        var changed = 0;

        for (var i = 0; i < hanging.Count && changed != limit; i++) {
            var overTime = hanging[i];
            if (!CombatHangingEffects.IsOverTime(overTime) || !CombatHangingEffects.MatchesDisposition(overTime, disposition)) {
                continue;
            }

            MoveToTickLayout(overTime);
            overTime.m_effectParam += tickChange;
            changed++;
            if (overTime.m_effectParam >= 1) {
                continue;
            }

            hanging.RemoveAt(i);
            i--;
        }

        return changed;
    }

    private static void MoveToTickLayout(SpellEffect overTime) {
        if (overTime.m_paramPerRound != 0 || overTime.m_numRounds == 0) {
            return;
        }

        overTime.m_paramPerRound = (int) (overTime.m_effectParam / (float) overTime.m_numRounds);
        overTime.m_effectParam = Math.Max(overTime.m_numRounds, 1);
    }

}
