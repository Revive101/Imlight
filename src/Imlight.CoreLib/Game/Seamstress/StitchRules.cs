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
 * STITCH RULES
 * ========================================================================
 *
 * PURPOSE:
 * The pure rules of the seamstress: which items can be stitched together, what a stitch produces and
 * what an unstitch gives back. Nothing here touches the player, Crowns or the inventory.
 *
 * USAGE EXAMPLE:
 * if (!StitchRules.TryPlanStitch(displayItem, statsItem, out var plan, out var reason)) { deny(reason); }
 *
 * NOTE:
 * The result has the stats item's template, the display item's look (m_displayID) and the display item's dyes.
 * A stitched item's dyes belong to its appearance template. Bit 31 of m_pattern is the client's item lock.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Seamstress;

/// <summary>
/// What a stitch produces: the stats template, the appearance template and the appearance item's dyes.
/// </summary>
internal readonly record struct StitchPlan(uint StatsTemplateId, uint DisplayTemplateId, int PrimaryColor,
                                           int SecondaryColor, int Pattern, EquipmentSlotType Slot);

/// <summary>
/// One of the two items an unstitch gives back.
/// </summary>
internal readonly record struct UnstitchedItem(uint TemplateId, int PrimaryColor, int SecondaryColor, int Pattern);

/// <summary>
/// What an unstitch gives back: the stats item and the appearance item.
/// </summary>
internal readonly record struct UnstitchPlan(UnstitchedItem Stats, UnstitchedItem Appearance);

internal static class StitchRules {

    private const int MAX_DYE = 31;
    private const int PATTERN_WITHOUT_LOCK = 0x7FFFFFFF;

    internal static readonly IReadOnlyList<EquipmentSlotType> s_stitchableSlots = [
        EquipmentSlotType.Hat,
        EquipmentSlotType.Robe,
        EquipmentSlotType.Shoes,
        EquipmentSlotType.Weapon
    ];

    /// <summary>
    /// Finds the slot of an item template when it is one of the four stitchable slots.
    /// </summary>
    /// <param name="template">The item template.</param>
    /// <param name="slot">The stitchable slot of the template.</param>
    /// <returns>False for a rental, a template without exactly one slot adjective, or any other slot.</returns>
    internal static bool TryGetStitchableSlot(WizItemTemplate template, out EquipmentSlotType slot) {
        slot = default;
        if (template?.m_adjectiveList is null || ItemHelper.IsRental(template)) {
            return false;
        }

        var matches = Enum.GetValues<EquipmentSlotType>()
            .Where(candidate => template.m_adjectiveList.Any(adjective =>
                string.Equals(adjective, candidate.ToString(), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (matches.Count != 1 || !s_stitchableSlots.Contains(matches[0])) {
            return false;
        }

        slot = matches[0];

        return true;
    }

    /// <summary>
    /// Plans the item a stitch creates.
    /// </summary>
    /// <param name="display">The item whose look the result takes.</param>
    /// <param name="stats">The item whose stats the result takes.</param>
    /// <param name="plan">The planned result.</param>
    /// <param name="reason">Why the stitch is refused. Empty when it is allowed.</param>
    /// <returns>True when the two items can be stitched.</returns>
    internal static bool TryPlanStitch(WizClientObjectItem display, WizClientObjectItem stats, out StitchPlan plan,
                                       out string reason) {
        plan = default;
        if (display is null || stats is null) {
            reason = "an item is missing";

            return false;
        }

        if (ReferenceEquals(display, stats) || display.m_globalID == stats.m_globalID) {
            reason = "the same item was chosen twice";

            return false;
        }

        var displayTemplate = CoreObjectFactory.GetCoreTemplate(display.m_templateID) as WizItemTemplate;
        var statsTemplate = CoreObjectFactory.GetCoreTemplate(stats.m_templateID) as WizItemTemplate;
        if (!TryGetStitchableSlot(displayTemplate, out var displaySlot)) {
            reason = $"item {display.m_globalID.Full} cannot be stitched";

            return false;
        }

        if (!TryGetStitchableSlot(statsTemplate, out var statsSlot)) {
            reason = $"item {stats.m_globalID.Full} cannot be stitched";

            return false;
        }

        if (displaySlot != statsSlot) {
            reason = $"the items are for different slots ({displaySlot}, {statsSlot})";

            return false;
        }

        // A stitched stats item loses its old look, a stitched display item keeps the look it already has.
        plan = new StitchPlan(
            StatsTemplateId: (uint) stats.m_templateID,
            DisplayTemplateId: ItemHelper.GetDisplayTemplateId(display),
            PrimaryColor: display.m_primaryColor,
            SecondaryColor: display.m_secondaryColor,
            Pattern: display.m_pattern & PATTERN_WITHOUT_LOCK,
            Slot: displaySlot);
        reason = string.Empty;

        return true;
    }

    /// <summary>
    /// Plans the two items an unstitch gives back.
    /// </summary>
    /// <param name="item">The stitched item.</param>
    /// <param name="statItemColor1">The first dye layer chosen for the stats item.</param>
    /// <param name="statItemColor2">The second dye layer chosen for the stats item.</param>
    /// <param name="plan">The planned result.</param>
    /// <param name="reason">Why the unstitch is refused. Empty when it is allowed.</param>
    /// <returns>True when the item can be unstitched.</returns>
    internal static bool TryPlanUnstitch(WizClientObjectItem item, int statItemColor1, int statItemColor2,
                                         out UnstitchPlan plan, out string reason) {
        plan = default;
        if (item is null) {
            reason = "the item is missing";

            return false;
        }

        if (!ItemHelper.IsStitched(item)) {
            reason = $"item {item.m_globalID.Full} is not stitched";

            return false;
        }

        if (!IsDye(statItemColor1) || !IsDye(statItemColor2)) {
            reason = $"the dye layers {statItemColor1} and {statItemColor2} are out of range";

            return false;
        }

        var displayTemplateId = ItemHelper.GetDisplayTemplateId(item);
        if (CoreObjectFactory.GetCoreTemplate(item.m_templateID) is not WizItemTemplate
            || CoreObjectFactory.GetCoreTemplate(displayTemplateId) is not WizItemTemplate) {
            reason = $"a template of item {item.m_globalID.Full} was not found";

            return false;
        }

        plan = new UnstitchPlan(
            Stats: new UnstitchedItem((uint) item.m_templateID, statItemColor1, statItemColor2, 0),
            Appearance: new UnstitchedItem(displayTemplateId, item.m_primaryColor, item.m_secondaryColor,
                item.m_pattern & PATTERN_WITHOUT_LOCK));
        reason = string.Empty;

        return true;
    }

    private static bool IsDye(int color) => color is >= 0 and <= MAX_DYE;

}
