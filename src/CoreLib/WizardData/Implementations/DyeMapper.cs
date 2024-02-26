/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class DyeMapper {
    /// <summary>
    /// Applies the primary dye color to the specified WizClientObjectItem.
    /// </summary>
    /// <param name="item">The WizClientObjectItem to apply the dye color to.</param>
    /// <param name="primaryColor">The primary dye color to apply.</param>
    public static void ApplyPrimaryDye(WizClientObjectItem item, DyeColor primaryColor) {
        // todo: check if the item is dyeable
        // todo: check if the item has this dye as an option
        item.m_primaryColor = (int) primaryColor;

        var persistentSaveSuccess = WizardItemCollection.ApplyPrimaryDye(item, (int) primaryColor);
        if (!persistentSaveSuccess) {
            Logger.Error("Failed to save primary dye {0} for item {1}", Logger.Args(primaryColor, item.m_globalID));
            return;
        }
    }

    /// <summary>
    /// Applies the secondary dye color to the specified WizClientObjectItem.
    /// </summary>
    /// <param name="item">The WizClientObjectItem to apply the secondary dye color to.</param>
    /// <param name="secondaryColor">The secondary dye color to apply.</param>
    public static void ApplySecondaryDye(WizClientObjectItem item, DyeColor secondaryColor) {
        // todo: check if the item is dyeable
        // todo: check if the item has this dye as an option
        item.m_secondaryColor = (int) secondaryColor;

        var persistentSaveSuccess = WizardItemCollection.ApplySecondaryDye(item, (int) secondaryColor);
        if (!persistentSaveSuccess) {
            Logger.Error("Failed to save secondary dye {0} for item {1}", Logger.Args(secondaryColor, item.m_globalID));
            return;
        }
    }
}
