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
 * ITEM HELPER
 * ========================================================================
 * 
 * PURPOSE:
 * Provides utility methods for item template and equipment slot operations, 
 * including retrieving public item representations and determining 
 * equipment slot types.
 * 
 * USAGE EXAMPLE:
 * // Get the item template for a specific item
 * var itemTemplate = ItemHelper.GetItemTemplate(wizClientItem);
 * 
 * // Get the equipment slot for an item template
 * var equipmentSlot = ItemHelper.GetItemSlot(itemTemplate);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.Bit;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Items;

internal static class ItemHelper {
    
    /// <summary>
    /// Gets the <see cref="EquippedItemInfoList"/> for a <see cref="Wizard"/>. This is a lightweight version of the
    /// actual equipment that is used to publicly display the character's equipment.
    /// </summary>
    /// <param name="item">The Wizard item.</param>
    /// <returns>The public, lightweight version of the given item.</returns>
    internal static WizardEquippedItemInfo GetPublicItem(WizClientObjectItem item) {
        var publicItem = new WizardEquippedItemInfo {
            m_itemID = (uint) item.m_templateID,
            m_pattern = (Bui5) item.m_pattern,
            m_baseColor = (Bui5) item.m_primaryColor,
            m_trimColor = (Bui5) item.m_secondaryColor,
        };

        return publicItem;
    }

    /// <summary>
    /// Gets the item template for a given <see cref="WizClientObjectItem"/>.
    /// </summary>
    /// <param name="item">The item in question.</param>
    /// <returns>The template for the given item. Null, if it wasn't found.</returns>
    internal static WizItemTemplate GetItemTemplate(WizClientObjectItem item) {
        var template = CoreObjectFactory.GetCoreTemplate(item.m_templateID);

        return (WizItemTemplate) template;
    }

    /// <summary>
    /// Gets the slot of a WizClientObjectItem from the template adjectives.
    /// </summary>
    /// <param name="itemTemplate"></param>
    /// <returns>The EquipmentSlot of the item; null if a matching adjective is not found.</returns>
    internal static EquipmentSlot GetItemSlot(WizItemTemplate itemTemplate) {
        if (itemTemplate?.m_adjectiveList is null) {
            throw new InvalidOperationException("Item template does not have an adjective list.");
        }

        // Iterate through the EquipmentSlot enum and return the first slot that matches the item's slot.
        foreach (var slot in Enum.GetValues(typeof(EquipmentSlotType)).Cast<EquipmentSlotType>()) {
            // Sanitize the slot name.
            var slotName = slot.ToString().Split('.')[^1];

            // Check if any of the items adjectives match the slot name.
            if (itemTemplate.m_adjectiveList.Any(adj => string.Equals(adj, slotName, StringComparison.OrdinalIgnoreCase))) {
                return new EquipmentSlot {
                    SlotType = slot,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the slot hash of a given item template.
    /// </summary>
    /// <param name="template">The item template.</param>
    /// <returns>The hash value of the item slot.</returns>
    internal static uint GetItemSlotHash(WizItemTemplate template) {
        var slotType = GetItemSlot(template).SlotType;
        var slotName = slotType.ToString().Split('.')[^1];
        
        return StringHash.Compute(slotName);
    }
    
}
