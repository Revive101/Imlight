using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using System;
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
    /// Gets the slot hash of a WizClientObjectItem.
    /// </summary>
    /// <param name="item">The WizClientObjectItem to get the slot hash from.</param>
    /// <returns>The slot hash of the item, or 0 if the item template is null or the adjective list count is less than 2.</returns>
    internal static uint GetItemSlotHash(WizClientObjectItem item) {
        var template = GetItemTemplate(item);
        if (template == null) {
            return 0;
        }

        // Get the slot hash from the item template.
        if (template.m_adjectiveList.Count < 2) {
            return 0;
        }
        else {
            // The second adjective is the slot name.
            var slotName = template.m_adjectiveList[1];
            return StringHash.Compute(slotName);
        }
    }

    /// <summary>
    /// Calculates the hash value for the slot of an item based on its template.
    /// </summary>
    /// <param name="template">The item template.</param>
    /// <returns>The hash value of the item slot.</returns>
    internal static uint GetItemSlotHash(WizItemTemplate template) {
        if (template == null) {
            return 0;
        }

        // Get the slot hash from the item template.
        if (template.m_adjectiveList.Count < 2) {
            return 0;
        }
        else {
            // The second adjective is the slot name.
            var slotName = template.m_adjectiveList[1];
            return StringHash.Compute(slotName);
        }
    }
}
