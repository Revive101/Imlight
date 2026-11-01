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
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Decides which items the two equipment slots of a house take. The player chooses a home by equipping a house in the
 * Backpack, and the client equips its structure in a second slot.
 *
 * USAGE EXAMPLE:
 * if (!HouseEquipment.Allows(item, message.SlotName)) {
 *     // the request is forged
 * }
 *
 * NOTE:
 * - An item's slot comes from its template, as for every other equipment: the slot whose name the template carries as an
 *   adjective. A house slot takes an item only when the template is a house template (it has a DeedBehaviorTemplate), and
 *   a house template goes only to the house slot its adjectives name.
 * - Equipping a house has no effect and spawns nothing.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

internal static class HouseEquipment {

    /// <summary>
    /// Checks whether a slot is one of the slots a house is equipped in.
    /// </summary>
    /// <param name="slot">An equipment slot.</param>
    /// <returns><c>true</c> for the castle slot and the structure slot.</returns>
    internal static bool IsHouseSlot(EquipmentSlotType slot)
        => slot is EquipmentSlotType.Islands or EquipmentSlotType.Structure;

    /// <summary>
    /// Checks a request to equip an item in a named slot against the rules of the house slots. Requests that name no house slot
    /// for an item that does not belong in one are none of this class's business.
    /// </summary>
    /// <param name="item">The item the client wants to equip.</param>
    /// <param name="slotName">The slot name in the client's request.</param>
    /// <returns><c>false</c> when the item or the slot is a house's and the other does not match.</returns>
    internal static bool Allows(WizClientObjectItem item, string slotName) {
        EquipmentSlotType? named = Enum.TryParse<EquipmentSlotType>(slotName, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
        var template = ItemHelper.GetItemTemplate(item);
        var held = template?.m_adjectiveList is null ? null : ItemHelper.GetItemSlot(template)?.SlotType;
        var involvesHouseSlot = (named is { } namedSlot && IsHouseSlot(namedSlot)) || (held is { } heldSlot && IsHouseSlot(heldSlot));
        if (!involvesHouseSlot) {
            return true;
        }

        return named == held && HouseTemplates.TryGet(ItemHelper.GetTemplateId(item), out _);
    }

}
