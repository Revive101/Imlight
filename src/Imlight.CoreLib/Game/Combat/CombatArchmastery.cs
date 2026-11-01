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
 * COMBAT ARCHMASTERY
 * ========================================================================
 *
 * PURPOSE:
 * Runs archmastery in a duel: sets each participant's archmastery school, gauge cap and fill scale from
 * the deck, fills the gauge after every pip gain, and spends it turning power pips into school pips
 * (and generic pips into power pips once a power pip's worth is stored).
 *
 * USAGE EXAMPLE:
 * CombatArchmastery.InitializeForPlayer(circle) when the participant is built;
 * CombatArchmastery.Accumulate(circle) after the circle's pip gain.
 *
 * NOTE:
 * Mirrors the client's CombatParticipant::InitPipConversionFromDeck, Duel::UpdateTeamPipConversion and
 * CombatParticipant::AccumulatePipConversion. The fill rate is the participant's archmastery over the
 * opposing team's highest archmastery, so a fight against foes without archmastery fills at the cap.
 *
 * TODO:
 * - Creatures get a school but no gauge cap: their deck template's cap is not in the creature stats.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// The archmastery gauge and school pip conversion of duel participants.
/// </summary>
internal static class CombatArchmastery {

    private const float MIN_ARCHMASTERY_THAT_FILLS = 0.01f;
    private const float UNOPPOSED_ARCHMASTERY_DIVISOR = 1.0f;

    /// <summary>
    /// A participant's total archmastery: base and bonus from level and gear.
    /// </summary>
    internal static float GetTotalArchmastery(ServerWizGameStats stats)
        => stats is null ? 0f : stats.m_archmasteryBase + stats.m_archmasteryBonus;

    /// <summary>
    /// Sets the archmastery state of a player from the equipped deck.
    /// </summary>
    internal static void InitializeForPlayer(CombatDuelSubCircle circle) {
        var participant = circle.CombatParticipant;
        ResetGauge(participant);
        if (!TryGetEquippedDeck(circle._wizard, out var deck, out var deckTemplate)) {
            return;
        }

        var spells = (deck.m_spellList ?? []).Select(s => (s.m_templateID, s.m_quantity));
        var nonTreasureCount = ReadDeckSpells(participant, spells);
        participant.m_maxArchmasteryPoints = deckTemplate.m_maxArchmasteryPoints;
        participant.m_deckFullness = GetDeckFullness(participant, nonTreasureCount, deckTemplate.m_maxSpells);
        participant.m_archmasterySchool = PickSchool(participant, deck.m_archmasterySchool);
    }

    /// <summary>
    /// Sets the archmastery state of a creature from its spell list.
    /// </summary>
    internal static void InitializeForCreature(CombatDuelSubCircle circle, IEnumerable<CombatDeckSpellData> spells) {
        var participant = circle.CombatParticipant;
        ResetGauge(participant);
        ReadDeckSpells(participant, spells.Select(s => (s.TemplateId, s.Quantity)));
        participant.m_deckFullness = 1.0f;

        // Live sends a creature's own school even when its deck holds no school pip spell.
        var primary = (uint) participant.m_primaryMagicSchoolID;
        participant.m_archmasterySchool = CombatPipPayment.IsSchoolPipSchool(primary)
            ? primary
            : PickSchool(participant, 0);
    }

    /// <summary>
    /// Changes the school a participant's gauge converts to.
    /// </summary>
    internal static bool SelectSchool(CombatDuelSubCircle circle, uint school) {
        if (circle.CombatParticipant is null || !CombatPipPayment.IsSchoolPipSchool(school)) {
            return false;
        }

        circle.CombatParticipant.m_archmasterySchool = school;

        return true;
    }

    /// <summary>
    /// Fills the participant's gauge for this pip gain and converts pips with what it holds.
    /// </summary>
    internal static void Accumulate(CombatDuelSubCircle circle, IEnumerable<CombatDuelSubCircle> circles) {
        var participant = circle.CombatParticipant;
        var duel = circle._duelActor?.Duel;
        var config = WizStatisticEffectConfigLoader.Config;
        if (participant is null || duel is null || config is null) {
            return;
        }

        RaiseTeamMaximums(duel, circles);
        var divisor = participant.m_originalTeam == 0 ? duel.m_maxArchmasteryTeam0 : duel.m_maxArchmasteryTeam1;
        if (participant.m_archmasterySchool == 0 || divisor <= MIN_ARCHMASTERY_THAT_FILLS) {
            return;
        }

        if (!participant.m_isPlayer && participant.m_archmasteryFlags == 0) {
            return;
        }

        var archmastery = GetTotalArchmastery(circle.ParticipantGameStats);
        if (archmastery <= MIN_ARCHMASTERY_THAT_FILLS) {
            return;
        }

        var fill = participant.m_deckFullness * (archmastery / divisor) * config.m_archmasteryFillRateScaler;
        fill = Math.Min(fill, config.m_archmasteryFillRateMax);
        participant.m_archmasteryPoints = Math.Min(participant.m_archmasteryPoints + fill,
                                                   participant.m_maxArchmasteryPoints);

        var converted = ConvertPowerPipsToSchoolPips(participant, config.m_archmasteryConversionCost);
        ConvertGenericPipsToPowerPips(participant, config.m_archmasteryPowerPipCost);
        if (converted && !participant.m_isPlayer) {
            RotateToNextSchool(participant);
        }
    }

    private static void ResetGauge(CombatParticipant participant) {
        participant.m_deckFullness = 0;
        participant.m_maxArchmasteryPoints = 0;
        participant.m_archmasteryPoints = 0;
        participant.m_archmasteryFlags = 0;
        participant.m_archmasterySchool = 0;
    }

    private static bool TryGetEquippedDeck(Wizard wizard, out DeckBehavior deck, out DeckBehaviorTemplate deckTemplate) {
        deck = null;
        deckTemplate = null;
        var deckSlot = wizard?.EquipmentBehavior?.SlotList?.FirstOrDefault(s => s.SlotType == EquipmentSlotType.Deck);
        if (deckSlot is null) {
            return false;
        }

        var deckItem = wizard.EquipmentBehavior.EquippedItems?.FirstOrDefault(i => i.m_globalID == deckSlot.ItemId);
        if (deckItem is null || !CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(deckItem, out deck)) {
            return false;
        }

        deckTemplate = CoreObjectFactory.GetCoreTemplate(deckItem.m_templateID)?.m_behaviors
            .OfType<DeckBehaviorTemplate>()
            .FirstOrDefault();

        return deckTemplate is not null;
    }

    private static uint ReadDeckSpells(CombatParticipant participant, IEnumerable<(uint TemplateId, uint Quantity)> spells) {
        var nonTreasureCount = 0u;
        foreach (var (templateId, quantity) in spells) {
            if (CoreObjectFactory.GetCoreTemplate(templateId) is not SpellTemplate template) {
                continue;
            }

            if (!IsPlayableHere(participant, template)) {
                continue;
            }

            participant.m_archmasteryFlags |= GetSchoolPipCostMask(template.m_spellRank);
            if (!template.m_Treasure) {
                nonTreasureCount += quantity;
            }
        }

        return nonTreasureCount;
    }

    private static bool IsPlayableHere(CombatParticipant participant, SpellTemplate template) {
        var pvpOnlyBlocked = template.m_PvP && !participant.m_ignoreSpellsPvPOnlyFlag && !participant.m_pvp;
        var pveOnlyBlocked = template.m_PvE && !participant.m_ignoreSpellsPvEOnlyFlag && participant.m_pvp;

        return !pvpOnlyBlocked && !pveOnlyBlocked;
    }

    private static uint GetSchoolPipCostMask(SpellRank rank) {
        if (rank is null) {
            return 0;
        }

        var mask = 0u;
        for (var i = 0; i < CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT; i++) {
            if (CombatPipPayment.GetSchoolPipCost(rank, i) != 0) {
                mask |= 1u << i;
            }
        }

        return mask;
    }

    private static float GetDeckFullness(CombatParticipant participant, uint nonTreasureCount, int maxSpells) {
        if (!participant.m_isPlayer) {
            return 1.0f;
        }

        var config = WizStatisticEffectConfigLoader.Config;
        var minimum = participant.m_pvp
            ? Math.Max(config?.m_archmasteryFullnessMinPvP ?? 0f, 0f)
            : config?.m_archmasteryFullnessMinPvE ?? 0f;

        // A deck without a size counts as full in PvE and as empty in PvP.
        var fullness = maxSpells < 1
            ? (participant.m_pvp ? 0f : 1f)
            : (float) nonTreasureCount / maxSpells;

        return Math.Max(minimum, fullness);
    }

    private static uint PickSchool(CombatParticipant participant, uint deckSchool) {
        if (deckSchool != 0) {
            return deckSchool;
        }

        var primary = (uint) participant.m_primaryMagicSchoolID;
        var primaryIndex = CombatPipPayment.SchoolIndexOf(primary);
        var primaryIsValid = primaryIndex < CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT;
        if (primaryIsValid && (participant.m_isPlayer || HasSchoolBit(participant.m_archmasteryFlags, primaryIndex))) {
            return primary;
        }

        for (var i = 0; i < CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT; i++) {
            if (HasSchoolBit(participant.m_archmasteryFlags, i)) {
                return CombatPipPayment.SchoolAt(i);
            }
        }

        return CombatPipPayment.SchoolAt(0);
    }

    private static bool HasSchoolBit(uint mask, int index) => (mask >> index & 1) != 0;

    private static void RaiseTeamMaximums(Duel duel, IEnumerable<CombatDuelSubCircle> circles) {
        foreach (var circle in circles) {
            if (!circle.AddedToDuel || circle.CombatParticipant is null) {
                continue;
            }

            var archmastery = GetTotalArchmastery(circle.ParticipantGameStats);

            // Each team's divisor is the highest archmastery on the other team.
            if (circle.CombatParticipant.m_originalTeam == 0 && archmastery > duel.m_maxArchmasteryTeam1) {
                duel.m_maxArchmasteryTeam1 = archmastery;
                duel.m_maxArchmasteryTeam0 = duel.m_maxArchmasteryTeam0 == 0
                    ? UNOPPOSED_ARCHMASTERY_DIVISOR
                    : duel.m_maxArchmasteryTeam0;
            }
            else if (circle.CombatParticipant.m_originalTeam == 1 && archmastery > duel.m_maxArchmasteryTeam0) {
                duel.m_maxArchmasteryTeam0 = archmastery;
                duel.m_maxArchmasteryTeam1 = duel.m_maxArchmasteryTeam1 == 0
                    ? UNOPPOSED_ARCHMASTERY_DIVISOR
                    : duel.m_maxArchmasteryTeam1;
            }
        }
    }

    private static bool ConvertPowerPipsToSchoolPips(CombatParticipant participant, float conversionCost) {
        var pips = participant.m_pipCount;
        var schoolIndex = CombatPipPayment.SchoolIndexOf(participant.m_archmasterySchool);
        if (conversionCost <= 0 || schoolIndex >= CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT) {
            return false;
        }

        var converted = false;
        while (participant.m_archmasteryPoints >= conversionCost && pips.m_powerPips > 0) {
            participant.m_archmasteryPoints -= conversionCost;
            pips.m_powerPips--;
            var schoolPips = CombatPipPayment.GetSchoolPips(pips, schoolIndex);
            CombatPipPayment.SetSchoolPips(pips, schoolIndex, (byte) (schoolPips + 1));
            converted = true;
        }

        return converted;
    }

    private static void ConvertGenericPipsToPowerPips(CombatParticipant participant, float powerPipCost) {
        var pips = participant.m_pipCount;
        if (powerPipCost <= 0) {
            return;
        }

        while (participant.m_archmasteryPoints >= powerPipCost && pips.m_genericPips > 0) {
            participant.m_archmasteryPoints -= powerPipCost;
            pips.m_genericPips--;
            pips.m_powerPips++;
        }
    }

    private static void RotateToNextSchool(CombatParticipant participant) {
        var index = Math.Min(CombatPipPayment.SchoolIndexOf(participant.m_archmasterySchool),
                             CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT - 1);
        for (var step = 0; step < CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT; step++) {
            index = (index + 1) % CombatPipPayment.SCHOOL_PIP_SCHOOL_COUNT;
            if (HasSchoolBit(participant.m_archmasteryFlags, index)) {
                participant.m_archmasterySchool = CombatPipPayment.SchoolAt(index);

                return;
            }
        }
    }

}
