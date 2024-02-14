using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

internal static class ItemHelper {
    private static readonly string[] s_slotNames = {
        "Hat", "Robe", "Shoes", "Weapon", "Athame", "Amulet", "Ring", "Mount", "Deck",
    };

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

        // Iterate through the slot names and return the hash of the first slot name that matches the item's slot.
        foreach (var slotName in s_slotNames) {
            if (template.m_adjectiveList.Any(adj => string.Equals(adj, slotName, StringComparison.OrdinalIgnoreCase))) {
                return StringHash.Compute(slotName);
            }
        }

        // Log that we couldn't find the slot name, and print all the adjectives.
        Logger.Error($"Couldn't find slot name for item {0} with adjectives: {1}",
            Logger.Args(item.m_templateID, string.Join(", ", template.m_adjectiveList)));

        return 0;
    }

    /// <summary>
    /// Returns the slot hash of a given item template.
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
