using Imlight.Common.ObjectProperty.PropertyReflection;
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
}
