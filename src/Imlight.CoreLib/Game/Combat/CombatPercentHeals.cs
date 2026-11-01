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
 * COMBAT PERCENT HEAL SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Heals targets by a percent of their maximum health, as the client's heal and
 * set-heal-percent handlers do.
 *
 * USAGE EXAMPLE:
 * CombatPercentHeals.Apply(effect, targets);
 *
 * NOTE:
 * The max health heal caps its percent at 100; the set heal percent does not. Neither
 * goes through the heal boosts, which the client only applies to plain heals.
 *
 * TODO:
 *
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Heals by a percent of the target's maximum health.
/// </summary>
internal static class CombatPercentHeals {

    private const int MAX_HEALTH_HEAL_PERCENT_CAP = 100;

    /// <summary>
    /// Whether the effect type heals by a percent of maximum health.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <returns>True for a max health heal or a set heal percent.</returns>
    internal static bool Handles(kSpellEffects effectType) => effectType is kSpellEffects.kMaxHealthHeal
                                                                         or kSpellEffects.kSetHealPercent;

    /// <summary>
    /// Heals every living target by the effect's percent of its maximum health.
    /// </summary>
    /// <param name="effect">The heal effect; m_effectParam is the percent.</param>
    /// <param name="targets">The targets of the effect.</param>
    internal static void Apply(SpellEffect effect, CombatDuelSubCircle[] targets) {
        var percent = effect.m_effectType == kSpellEffects.kMaxHealthHeal
            ? Math.Min(effect.m_effectParam, MAX_HEALTH_HEAL_PERCENT_CAP)
            : effect.m_effectParam;

        foreach (var target in targets) {
            if (!target.IsAlive) {
                continue;
            }

            var heal = (int) (percent * target.ParticipantGameStats.m_baseHitpoints / 100.0f);
            target.HealParticipant(heal);
        }
    }

}
