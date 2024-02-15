using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

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
        // Iterate through the EquipmentSlot enum and return the first slot that matches the item's slot.
        foreach (var slot in Enum.GetValues(typeof(EquipmentSlot)).Cast<EquipmentSlot>()) {
            // Sanitize the slot name.
            var slotName = slot.ToString().Split('.')[^1];

            // Check if any of the items adjectives match the slot name.
            if (itemTemplate.m_adjectiveList.Any(adj => string.Equals(adj, slotName, StringComparison.OrdinalIgnoreCase))) {
                return slot;
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
        var slot = GetItemSlot(template);
        var slotName = slot?.ToString().Split('.')[^1];
        return slot is null ? 0 : StringHash.ComputeHash(slotName);
    }
}
