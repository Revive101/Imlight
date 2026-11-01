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
 * COMBAT SHADOW PIP SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Owns shadow pips in combat: the per-round shadow gauge roll, the per-team
 * gauge threshold, shadow pip spell costs and the modify shadow pips effect.
 *
 * USAGE EXAMPLE:
 * The duel calls RollShadowPip for each participant at every pip gain; the
 * pip checks and the cast resolver call HasShadowPipsFor and DeductShadowPips.
 *
 * NOTE:
 * The roll mirrors the client's shared combat code: each round adds a random
 * amount between rating * rating factor (at least 1) and the rating to the
 * gauge, and a full gauge grants a shadow pip and empties. The sigil template
 * gives the threshold type and factors. The client only reads the team
 * thresholds, so the team the threshold aggregates over is our reading.
 *
 * TODO:
 * - Is the team threshold taken over the participant's own team or the opposing team?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;

namespace Imlight.CoreLib.Game.Combat;

internal static class CombatShadowPips {

    private const float MINIMUM_SHADOW_GAUGE_GAIN = 1.0f;
    private const int GAUGE_PERCENT_STEP = 10;
    private const int GAUGE_PERCENT_HIGHEST_STEP = 9;

    /// <summary>
    /// Rolls the participant's shadow gauge for this round and grants a shadow pip when it fills.
    /// </summary>
    internal static void RollShadowPip(CombatDuelSubCircle circle) {
        if (!CanAddShadowPip(circle)) {
            return;
        }

        var duelActor = circle._duelActor;
        var duel = duelActor.Duel;
        UpdateDuelShadowSettings(duelActor);

        var participant = circle.CombatParticipant;
        var threshold = participant.m_originalTeam == (int) CombatTeam.Monster
            ? duel.m_shadowPipThresholdTeam1
            : duel.m_shadowPipThresholdTeam0;
        if (threshold <= 0 || SkipsFirstRoundRoll(duel, participant)) {
            participant.m_roundsSinceShadowPip++;

            return;
        }

        if (!FillGauge(circle.ParticipantGameStats, participant, threshold, duel)) {
            participant.m_roundsSinceShadowPip++;

            return;
        }

        participant.m_pipCount.m_shadowPips++;
        participant.m_roundsSinceShadowPip = 0;
    }

    /// <summary>
    /// Whether the participant holds the shadow pips the spell costs.
    /// </summary>
    internal static bool HasShadowPipsFor(CombatDuelSubCircle circle, Spell spell) {
        var cost = spell.m_pipCost?.m_shadowPips ?? 0;
        if (cost == 0) {
            return true;
        }

        var participant = circle.CombatParticipant;
        if (participant is null || participant.m_shadowSpellsDisabled) {
            return false;
        }

        return participant.m_pipCount.m_shadowPips >= cost;
    }

    /// <summary>
    /// Whether the participant's shadow pip cap could ever pay the spell's shadow pip cost.
    /// </summary>
    internal static bool CanEverHoldShadowPipsFor(CombatDuelSubCircle circle, Spell spell) {
        var cost = spell.m_pipCost?.m_shadowPips ?? 0;
        if (cost == 0) {
            return true;
        }

        return circle.ParticipantGameStats?.m_shadowPipMax >= cost;
    }

    /// <summary>
    /// Spends the spell's shadow pip cost from the caster.
    /// </summary>
    internal static void DeductShadowPips(CombatDuelSubCircle caster, Spell spell) {
        var cost = spell.m_pipCost?.m_shadowPips ?? 0;
        if (cost == 0 || caster.CombatParticipant is null) {
            return;
        }

        var pipCount = caster.CombatParticipant.m_pipCount;
        pipCount.m_shadowPips = (byte) Math.Max(pipCount.m_shadowPips - cost, 0);
    }

    /// <summary>
    /// Adds or removes shadow pips (signed effect param), clamped between zero and the target's shadow pip cap.
    /// </summary>
    internal static void ApplyModifyShadowPips(SpellEffect effect, CombatDuelSubCircle[] targets) {
        foreach (var target in targets) {
            if (!target.IsAlive || target.CombatParticipant is null) {
                continue;
            }

            var pipCount = target.CombatParticipant.m_pipCount;
            var cap = GetShadowPipCap(target);
            pipCount.m_shadowPips = (byte) Math.Clamp(pipCount.m_shadowPips + effect.m_effectParam, 0, cap);
        }
    }

    /// <summary>
    /// Empties the shadow gauge when a participant enters a duel.
    /// </summary>
    internal static void ResetGauge(ServerWizGameStats stats) {
        if (stats is null) {
            return;
        }

        stats.m_shadowPipRateAccumulated = 0;
        stats.m_shadowPipRateThreshold = 0;
        stats.m_shadowPipRatePercentage = 0;
    }

    private static bool CanAddShadowPip(CombatDuelSubCircle circle) {
        var participant = circle.CombatParticipant;
        if (participant is null || participant.m_shadowSpellsDisabled || circle.ParticipantGameStats is null) {
            return false;
        }

        return participant.m_pipCount.m_shadowPips < circle.ParticipantGameStats.m_shadowPipMax;
    }

    private static int GetShadowPipCap(CombatDuelSubCircle circle) {
        if (circle.CombatParticipant.m_shadowSpellsDisabled || circle.ParticipantGameStats is null) {
            return 0;
        }

        return Math.Max(circle.ParticipantGameStats.m_shadowPipMax, 0);
    }

    private static bool SkipsFirstRoundRoll(Duel duel, CombatParticipant participant) {
        // In PvP the team that acts first gets no shadow roll in the first round.
        return duel.m_bPVP && participant.m_teamID == duel.m_firstTeamToAct && duel.m_roundNum < 1;
    }

    private static void UpdateDuelShadowSettings(CombatDuelComponent duelActor) {
        var duel = duelActor.Duel;
        var sigil = duelActor.SigilTemplate;
        duel.m_shadowThresholdFactor = sigil?.m_shadowThresholdFactor ?? 0;
        duel.m_shadowPipRatingFactor = sigil?.m_shadowPipRatingFactor ?? 0;

        var shadowCircles = duelActor.ActiveSubCircles
            .Where(x => x.AddedToDuel && x.IsAlive && x.ParticipantGameStats?.m_shadowPipMax > 0)
            .ToArray();
        duel.m_defaultShadowPipRating = GetDefaultShadowPipRating(shadowCircles);

        var thresholdType = sigil?.m_shadowThresholdType ?? kShadow_Threshold_Type.SHADOW_THRESHOLD_MAX;
        duel.m_shadowPipThresholdTeam0 = GetTeamThreshold(shadowCircles, CombatTeam.Player, thresholdType, duel);
        duel.m_shadowPipThresholdTeam1 = GetTeamThreshold(shadowCircles, CombatTeam.Monster, thresholdType, duel);
    }

    private static float GetTeamThreshold(CombatDuelSubCircle[] shadowCircles,
                                          CombatTeam team,
                                          kShadow_Threshold_Type thresholdType,
                                          Duel duel) {
        var ratings = shadowCircles
            .Where(x => x.CombatParticipant.m_originalTeam == (int) team)
            .Select(x => GetRating(x.ParticipantGameStats, duel.m_defaultShadowPipRating))
            .ToArray();
        if (ratings.Length == 0) {
            return 0;
        }

        var aggregate = thresholdType switch {
            kShadow_Threshold_Type.SHADOW_THRESHOLD_MIN => ratings.Min(),
            kShadow_Threshold_Type.SHADOW_THRESHOLD_AVG => ratings.Average(),
            _ => ratings.Max(),
        };

        return aggregate * duel.m_shadowThresholdFactor;
    }

    private static float GetDefaultShadowPipRating(CombatDuelSubCircle[] shadowCircles) {
        // Unrated participants (creatures) roll with the level table's rating of the highest-rated level present.
        var levelRatings = shadowCircles
            .Select(x => MagicLevelsConfig.GetPlayerLevelInfo(x.ParticipantGameStats.MagicSchool, x.ParticipantGameStats.Level))
            .Where(x => x is not null)
            .Select(x => x.m_shadowPipRating)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(levelRatings, MINIMUM_SHADOW_GAUGE_GAIN);
    }

    private static float GetRating(ServerWizGameStats stats, float defaultRating) {
        var rating = stats.m_shadowPipRating + stats.m_bonusShadowPipRating;

        return rating == 0 ? defaultRating : rating;
    }

    private static bool FillGauge(ServerWizGameStats stats, CombatParticipant participant, float threshold, Duel duel) {
        var rating = GetRating(stats, duel.m_defaultShadowPipRating);
        var lowestGain = Math.Max(rating * duel.m_shadowPipRatingFactor, MINIMUM_SHADOW_GAUGE_GAIN);
        var gain = lowestGain + (Random.Shared.NextSingle() * (rating - lowestGain));
        var accumulated = stats.m_shadowPipRateAccumulated + gain;
        var filled = threshold <= accumulated;
        var percentage = 0;
        if (filled) {
            accumulated = 0;
        }
        else {
            percentage = RoundGaugePercent(accumulated / threshold);
        }

        Logger.Debug("Slot {0} | Shadow gauge | Threshold {1} | Accumulated {2} | Added {3} | Filled {4}",
            Logger.Args(participant.m_subcircle, threshold, accumulated, gain, filled));

        stats.m_shadowPipRateAccumulated = accumulated;
        stats.m_shadowPipRateThreshold = threshold;
        stats.m_shadowPipRatePercentage = percentage;
        participant.m_shadowPipRateThreshold = threshold;

        return filled;
    }

    private static int RoundGaugePercent(float fill) {
        // The client rounds to the nearest ten but never shows a gauge that has not filled as full.
        var percent = (int) (fill * 100);
        var steps = percent / GAUGE_PERCENT_STEP;
        if (steps < GAUGE_PERCENT_HIGHEST_STEP && percent % GAUGE_PERCENT_STEP >= GAUGE_PERCENT_STEP / 2) {
            steps++;
        }

        return steps * GAUGE_PERCENT_STEP;
    }

}
