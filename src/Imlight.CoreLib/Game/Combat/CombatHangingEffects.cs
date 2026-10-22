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
 * COMBAT HANGING EFFECT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Classifies hanging effects (charms, wards, over-time effects) the way the client
 * does, runs over-time ticks, copies spell effects into per-target hanging instances, and logs a
 * participant's hanging state.
 * 
 * USAGE EXAMPLE:
 * var copy = CombatHangingEffects.CopyForTarget(effect, context.SpellTemplateId);
 * target._hangingEffects.Add(copy);
 * 
 * NOTE:
 * A hanging effect is always its own instance: never add or write to a template effect.
 * 
 * TODO:
 * - Disposition of effect types the damage path does not use is unverified.
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Classification, copying and logging helpers for hanging effects.
/// </summary>
internal static class CombatHangingEffects {

    internal const float HANGING_EFFECT_CONSUME_TIME = 1.0f;
    internal const float HANGING_EFFECT_ADD_TIME = 1.0f;

    /// <summary>
    /// Whether the effect is a charm, which modifies the carrier's outgoing effects.
    /// </summary>
    /// <param name="effect">The hanging effect.</param>
    /// <returns>True for a charm.</returns>
    internal static bool IsCharm(SpellEffect effect) => effect.m_effectType is kSpellEffects.kPushCharm
                                                                           or kSpellEffects.kModifyOutgoingDamage
                                                                           or kSpellEffects.kModifyOutgoingHeal
                                                                           or kSpellEffects.kModifyOutgoingDamageType
                                                                           or kSpellEffects.kModifyOutgoingArmorPiercing
                                                                           or kSpellEffects.kModifyOutgoingStealHealth
                                                                           or kSpellEffects.kModifyAccuracy
                                                                           or kSpellEffects.kDispel
                                                                           or kSpellEffects.kCloakedCharm
                                                                           or kSpellEffects.kCritBoost
                                                                           or kSpellEffects.kCritBoostSchoolSpecific
                                                                           or kSpellEffects.kModifyOutgoingHealFlat
                                                                           or kSpellEffects.kModifyOutgoingDamageFlat;

    /// <summary>
    /// Whether the effect is a ward, which modifies the carrier's incoming effects.
    /// </summary>
    /// <param name="effect">The hanging effect.</param>
    /// <returns>True for a ward.</returns>
    internal static bool IsWard(SpellEffect effect) => effect.m_effectType is kSpellEffects.kModifyIncomingDamage
                                                                           or kSpellEffects.kMaximumIncomingDamage
                                                                           or kSpellEffects.kModifyIncomingHeal
                                                                           or kSpellEffects.kModifyIncomingDamageType
                                                                           or kSpellEffects.kModifyIncomingArmorPiercing
                                                                           or kSpellEffects.kModifyIncomingStealHealth
                                                                           or kSpellEffects.kBounceNext
                                                                           or kSpellEffects.kBouncePrevious
                                                                           or kSpellEffects.kBounceBack
                                                                           or kSpellEffects.kBounceAll
                                                                           or kSpellEffects.kAbsorbDamage
                                                                           or kSpellEffects.kAbsorbHeal
                                                                           or kSpellEffects.kCloakedWard
                                                                           or kSpellEffects.kStunResist
                                                                           or kSpellEffects.kPipConversion
                                                                           or kSpellEffects.kCritBlock
                                                                           or kSpellEffects.kStunBlock
                                                                           or kSpellEffects.kCloakedWardNoRemove
                                                                           or kSpellEffects.kPowerPipConversion
                                                                           or kSpellEffects.kDispelBlock
                                                                           or kSpellEffects.kConfusionBlock
                                                                           or kSpellEffects.kModifyIncomingHealFlat
                                                                           or kSpellEffects.kModifyIncomingDamageFlat
                                                                           or kSpellEffects.kModifyIncomingDamageOverTime
                                                                           or kSpellEffects.kModifyIncomingHealOverTime;

    /// <summary>
    /// Whether the effect is an over-time effect (damage, heal, deferred damage or a detonation marker).
    /// </summary>
    /// <param name="effect">The hanging effect.</param>
    /// <returns>True for an over-time effect.</returns>
    internal static bool IsOverTime(SpellEffect effect) => effect.m_effectType is kSpellEffects.kDetonateOverTime
                                                                               or kSpellEffects.kDamageOverTime
                                                                               or kSpellEffects.kHealOverTime
                                                                               or kSpellEffects.kDeferredDamage;

    /// <summary>
    /// Whether spells may remove or move the effect. Stun blocks and the other protected wards never move.
    /// </summary>
    /// <param name="effect">The hanging effect.</param>
    /// <returns>True when the effect can be removed or moved.</returns>
    internal static bool IsRemovable(SpellEffect effect) => effect.m_effectType is not (kSpellEffects.kStunBlock
                                                                                      or kSpellEffects.kCloakedWardNoRemove
                                                                                      or kSpellEffects.kDispelBlock
                                                                                      or kSpellEffects.kConfusionBlock);

    /// <summary>
    /// Whether the client counts the effect as harmful to its carrier. The sign of the effect param decides for most types.
    /// </summary>
    /// <param name="effect">The hanging effect.</param>
    /// <returns>True when the effect is harmful.</returns>
    internal static bool IsHarmful(SpellEffect effect) {
        switch (effect.m_effectType) {
            case kSpellEffects.kMinionRankDamage:
            case kSpellEffects.kModifyIncomingDamage:
            case kSpellEffects.kMaximumIncomingDamage:
            case kSpellEffects.kModifyIncomingArmorPiercing:
            case kSpellEffects.kModifyIncomingStealHealth:
            case kSpellEffects.kAbsorbHeal:
            case kSpellEffects.kCloakedWard:
            case kSpellEffects.kDamageOverTime:
            case kSpellEffects.kDeferredDamage:
            case kSpellEffects.kModifyIncomingDamageFlat:
            case kSpellEffects.kModifyIncomingDamageOverTime:
                return effect.m_effectParam >= 0;
            case kSpellEffects.kModifyIncomingHeal:
            case kSpellEffects.kModifyOutgoingDamage:
            case kSpellEffects.kModifyOutgoingHeal:
            case kSpellEffects.kModifyOutgoingArmorPiercing:
            case kSpellEffects.kModifyOutgoingStealHealth:
            case kSpellEffects.kAbsorbDamage:
            case kSpellEffects.kModifyAccuracy:
            case kSpellEffects.kCloakedCharm:
            case kSpellEffects.kStunResist:
            case kSpellEffects.kCritBoost:
            case kSpellEffects.kCritBlock:
            case kSpellEffects.kHealOverTime:
            case kSpellEffects.kModifyPowerPipChance:
            case kSpellEffects.kModifyPipRoundRate:
            case kSpellEffects.kModifyIncomingHealFlat:
            case kSpellEffects.kModifyOutgoingHealFlat:
            case kSpellEffects.kModifyOutgoingDamageFlat:
            case kSpellEffects.kModifyIncomingHealOverTime:
                return effect.m_effectParam < 0;
            case kSpellEffects.kModifyIncomingDamageType:
            case kSpellEffects.kDispel:
            case kSpellEffects.kStun:
            case kSpellEffects.kMindControl:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the effect's own disposition matches the disposition a spell asks for.
    /// </summary>
    /// <param name="effect">The hanging effect.</param>
    /// <param name="disposition">The wanted disposition.</param>
    /// <returns>True when the effect matches.</returns>
    internal static bool MatchesDisposition(SpellEffect effect, kHangingDisposition disposition) => disposition switch {
        kHangingDisposition.kBeneficial => !IsHarmful(effect),
        kHangingDisposition.kHarmful => IsHarmful(effect),
        _ => true,
    };

    /// <summary>
    /// Copies a base spell effect into a new instance for one target. Never use it on a wrapper effect,
    /// whose children it would drop.
    /// </summary>
    /// <param name="source">The effect to copy.</param>
    /// <param name="spellTemplateId">The template id of the spell that cast the effect.</param>
    /// <returns>The new instance.</returns>
    internal static SpellEffect CopyForTarget(SpellEffect source, uint spellTemplateId) => new() {
        m_effectType = source.m_effectType,
        m_effectParam = source.m_effectParam,
        m_disposition = source.m_disposition,
        m_sDamageType = source.m_sDamageType,
        m_damageType = source.m_damageType,
        m_pipNum = source.m_pipNum,
        m_actNum = source.m_actNum,
        m_effectTarget = source.m_effectTarget,
        m_numRounds = source.m_numRounds,
        m_paramPerRound = source.m_paramPerRound,
        m_healModifier = source.m_healModifier,
        m_spellTemplateID = spellTemplateId,
        m_enchantmentSpellTemplateID = source.m_enchantmentSpellTemplateID,
        m_act = source.m_act,
        m_cloaked = source.m_cloaked,
        m_bypassProtection = source.m_bypassProtection,
        m_armorPiercingParam = source.m_armorPiercingParam,
        m_chancePerTarget = source.m_chancePerTarget,
        m_protected = source.m_protected,
        m_converted = source.m_converted,
        m_rank = source.m_rank,
        m_shadowPactCasterSC = source.m_shadowPactCasterSC,
    };

    /// <summary>
    /// Moves an over-time effect on by one tick in the client layout: the first tick turns the total in the effect param
    /// into a per-round amount and the tick count, and every tick uses one count up.
    /// </summary>
    /// <param name="effect">The hanging over-time effect.</param>
    /// <returns>The amount this tick delivers.</returns>
    internal static int AdvanceOverTime(SpellEffect effect) {
        if (effect.m_paramPerRound == 0) {
            var rounds = Math.Max(effect.m_numRounds, 1);
            effect.m_paramPerRound = (int) (effect.m_effectParam / (float) rounds);
            effect.m_effectParam = rounds;

            // A total smaller than its round count would never settle on an amount, so it delivers nothing and ends.
            if (effect.m_paramPerRound == 0) {
                effect.m_effectParam = 0;

                return 0;
            }
        }

        effect.m_effectParam--;

        return effect.m_paramPerRound;
    }

    /// <summary>
    /// Delivers one over-time tick to its carrier. A damage tick goes through the carrier's wards like a hit does,
    /// a heal tick heals the carrier.
    /// </summary>
    /// <param name="carrier">The participant that carries the effect.</param>
    /// <param name="effect">The over-time effect that ticks.</param>
    /// <param name="amount">The amount to deliver.</param>
    /// <returns>The cinematic time the wards used.</returns>
    internal static float ApplyOverTimeTick(CombatDuelSubCircle carrier, SpellEffect effect, int amount) {
        if (effect.m_effectType == kSpellEffects.kHealOverTime) {
            // todo: are there wards that increase incoming healing? The client runs its ward pass on heal ticks too.
            carrier.HealParticipant(amount);

            return 0.0f;
        }

        // todo: the client runs its ward pass on every tick; that a tick consumes the wards it used is unconfirmed.
        var wards = CombatWards.FindAppliedWards(carrier, effect);
        wards = CombatWards.GetWardsBySchool([.. wards], effect.m_sDamageType, out _);
        var damage = CombatWards.GetIncomingDamageFromWards(wards, amount);
        carrier.DamageParticipant(damage);
        CombatWards.RemoveSpentWards(carrier, wards);

        return wards.Count * HANGING_EFFECT_CONSUME_TIME;
    }

    /// <summary>
    /// Logs the hanging effects of the caster and every target of an effect, newest first.
    /// </summary>
    /// <param name="caster">The caster of the effect.</param>
    /// <param name="targets">The targets of the effect.</param>
    internal static void LogHangingState(CombatDuelSubCircle caster, CombatDuelSubCircle[] targets) {
        var participants = new List<CombatDuelSubCircle>([caster]);
        participants.AddRange(targets);

        foreach (var participant in participants.Distinct()) {
            if (participant?._hangingEffects is null || participant.ParticipantGameStats is null) {
                continue;
            }

            var hanging = participant._hangingEffects
                .AsEnumerable()
                .Reverse()
                .Select(x => $"{x.m_effectType}:{x.m_effectParam}:{x.m_paramPerRound}:{x.m_numRounds}:{x.m_spellTemplateID}");

            Logger.Debug("Duel {0} | Slot {1} | Hanging | HP {2}/{3} | [{4}]",
                Logger.Args(caster._duelActor.Duel.m_duelID.Full, participant.SlotIndex,
                    participant.ParticipantGameStats.m_currentHitpoints,
                    participant.ParticipantGameStats.m_baseHitpoints, string.Join(", ", hanging)));
        }
    }

}
