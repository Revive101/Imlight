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
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.Bit;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Pet;
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

    /// <summary>
    /// Checks that a template can be given to a wizard as an inventory item.
    /// </summary>
    /// <param name="templateId">The item template ID.</param>
    /// <returns>Why the item cannot be given, or null if it can.</returns>
    internal static string CheckGiveable(ulong templateId) {
        var template = CoreObjectFactory.GetCoreTemplate(templateId);
        if (template is null) {
            return "Invalid item id.";
        }

        // We can't add game objects to the inventory.
        if (template is not WizItemTemplate) {
            return $"Cannot add objects of type {template.GetType().Name} to inventory.";
        }

        if (PetFactory.IsPetTemplate((uint) templateId)) {
            return null;
        }

        if (template.m_behaviors.Any(b => b?.m_behaviorName.ToString() == "PetItemBehavior")) {
            return "Cannot add this pet item because it is not in the pet list.";
        }

        var probe = CoreObjectFactory.FinalizeCoreObject(templateId) as WizClientObjectItem;
        if (probe is not null) {
            CoreObjectFactory.InitializeCoreObjectBehaviors(probe, probe.m_templateID);
        }

        if (probe?.m_inactiveBehaviors?.Contains(null) == true) {
            return "Cannot add this item because one of its behaviors cannot be created.";
        }

        return null;
    }

    /// <summary>
    /// Adds a new copy of an item to a wizard's inventory. Check the template with <see cref="CheckGiveable"/> first.
    /// </summary>
    /// <param name="wizard">The wizard to give the item to.</param>
    /// <param name="templateId">The item template ID.</param>
    /// <param name="item">The item that was added.</param>
    /// <returns>The message that shows the item to the wizard's client, or null if the item could not be added.</returns>
    internal static GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM GiveItem(Wizard wizard, ulong templateId,
                                                                          out WizClientObjectItem item) {
        var added = PetFactory.IsPetTemplate((uint) templateId)
            ? wizard.AddHatchedPetToInventory((uint) templateId, out item)
            : wizard.AddItemToInventory(templateId, out item);
        if (!added) {
            return null;
        }

        var coSerializer = new CoreObjectSerializer(behaviors: SerializerFlags.None);
        if (!coSerializer.Serialize(item, (uint) (PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit),
                                    out var serializedItem)) {
            return null;
        }

        return new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
            GlobalID = wizard.GameObjectID,
            SerializedItem = serializedItem
        };
    }

}
