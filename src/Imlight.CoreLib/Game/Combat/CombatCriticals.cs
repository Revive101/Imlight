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
 * COMBAT CRITICAL STRIKE SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Owns the critical hit / block stat lookups, the level-scaled rating to
 * chance conversion, and the single roll that decides whether a cast crits.
 * 
 * USAGE EXAMPLE:
 * Called from CombatActionResolver once per cast; the resulting multiplier
 * threads into CombatEffectApplicator through the action resolver.
 * 
 * NOTE:
 * Modern model: the crit chance depends on BOTH ratings (crit vs
 * crit + K * block), so it cannot be computed from the attacker's rating
 * alone. One roll decides the outcome; there is no separate block roll.
 * The landed multiplier varies between 1 and 2 by the crit/block ratio.
 * K(level) is the client's level-scaled crit divisor (100 + 3 * level in
 * WizStatisticEffectConfig), clamped into the 200-400 band. Heals roll the
 * rating against K alone, so they can never be blocked, and land the full
 * x2. Vengeance and Conviction add to their side of the roll, then are
 * consumed. Crits are gated by the config's level threshold.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Effects;

namespace Imlight.CoreLib.Game.Combat;

internal static class CombatCriticals {

    private const float MAX_CHANCE = 0.95f; // Client has a soft cap.
    private const float RATING_K_MIN = 200f;
    private const float RATING_K_MAX = 400f;
    private const float RATING_K_SLOPE = 1.25f;
    private const int RATING_K_BASE_LEVEL = 10;

    internal static float RatingToChance(float rating, int level) {
        if (rating <= 0f) {
            return 0f;
        }

        return Math.Min(MAX_CHANCE, rating / (rating + GetRatingK(level)));
    }

    internal static bool RollsCritical(CombatDuelSubCircle caster, CombatDuelSubCircle target, string school, bool isHeal) {
        // One combined roll: the target's block is part of the crit chance, so a
        // separate block roll does not exist. Heals have no block side; they roll
        // the rating against K alone and can never be blocked.
        var level = GetLevel(caster);
        if (level < WizStatisticEffectConfigLoader.GetCriticalHitLevelThreshold()) {
            return false;
        }

        var crit = GetCriticalRating(caster, school);
        var block = isHeal ? 1f : GetBlockRating(target, school);
        var chance = Math.Min(MAX_CHANCE, crit / (crit + GetRatingK(level) * block));

        // Vengeance and Conviction modify only their side of the roll, then are
        // consumed. A heal roll has no block side, so Conviction does not apply.
        chance += GetCritBoostPercent(caster);
        if (!isHeal) {
            chance -= GetCritBlockPercent(target);
        }

        chance = Math.Clamp(chance, 0f, MAX_CHANCE);

        return Rolls(chance);
    }

    internal static float GetCritMultiplier(CombatDuelSubCircle caster, CombatDuelSubCircle target, string school, bool isHeal) {
        // The landed crit multiplier, from the crit/block ratio:
        // 2 - (3 * block) / (crit + 3 * block). Ranges from just above 1
        // (block near crit) to 2 (crit far above block). Heals are never
        // blocked, so they always land the full x2.
        var crit = GetCriticalRating(caster, school);
        var block = isHeal ? 0f : GetBlockRating(target, school);

        var denominator = crit + 3f * block;
        if (denominator <= 0f) {
            return 1f;
        }

        return 2f - 3f * block / denominator;
    }

    private static float GetCritBoostPercent(CombatDuelSubCircle caster) {
        var boost = caster._hangingEffects.FirstOrDefault(x => x.m_effectType == kSpellEffects.kCritBoost);
        if (boost is null) {
            return 0f;
        }

        caster._hangingEffects.Remove(boost);

        return boost.m_effectParam / 100f;
    }

    private static float GetCritBlockPercent(CombatDuelSubCircle target) {
        var block = target._hangingEffects.FirstOrDefault(x => x.m_effectType == kSpellEffects.kCritBlock);
        if (block is null) {
            return 0f;
        }

        target._hangingEffects.Remove(block);

        return block.m_effectParam / 100f;
    }

    private static float GetCriticalRating(CombatDuelSubCircle sc, string school) {
        var stats = sc.ParticipantGameStats;
        if (stats is null) {
            return 0f;
        }

        return stats.m_criticalHitRatingAll + sc.GetStatBySchool(stats.m_criticalHitRatingBySchool, school);
    }

    private static float GetBlockRating(CombatDuelSubCircle sc, string school) {
        var stats = sc.ParticipantGameStats;
        if (stats is null) {
            return 0f;
        }

        return stats.m_blockRatingAll + sc.GetStatBySchool(stats.m_blockRatingBySchool, school);
    }

    private static int GetLevel(CombatDuelSubCircle sc) => sc.ParticipantGameStats?.Level ?? 0;

    private static float GetRatingK(int level) {
        // The level-scaled K: the client's crit divisor (100 + 3 * level in the
        // shipped config), clamped into the 200-400 band. Falls back to a linear
        // curve when the config is missing.
        var divisor = WizStatisticEffectConfigLoader.GetCritDivisor(level);
        if (divisor <= 0f) {
            divisor = RATING_K_MIN + (level - RATING_K_BASE_LEVEL) * RATING_K_SLOPE;
        }

        return Math.Clamp(divisor, RATING_K_MIN, RATING_K_MAX);
    }

    private static bool Rolls(float chance) {
        if (chance <= 0f) {
            return false;
        }

        return Random.Shared.Next(0, 10000) < (int) (chance * 10000f);
    }

}
