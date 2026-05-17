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
 * DYE MAPPER
 * ========================================================================
 * 
 * PURPOSE:
 * Provides static utility methods for applying dye colors 
 * to items in the game's item system.
 * 
 * USAGE EXAMPLE:
 * // Apply primary dye to an item
 * DyeMapper.ApplyPrimaryDye(item, DyeColor.Red);
 * 
 * // Apply multiple dye colors to an item
 * DyeMapper.ApplyAllDye(item, DyeColor.Blue, DyeColor.Green, DyeColor.Yellow);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Items;

public static class DyeMapper {

    /// <summary>
    /// Applies the primary dye color to the specified WizClientObjectItem.
    /// </summary>
    /// <param name="item">The WizClientObjectItem to apply the dye color to.</param>
    /// <param name="primaryColor">The primary dye color to apply.</param>
    public static void ApplyPrimaryDye(WizClientObjectItem item, DyeColor primaryColor) {
        item.m_primaryColor = (int) primaryColor;

        var persistentSaveSuccess = WizardItemCollection.ApplyPrimaryDye(item, (int) primaryColor);
        if (!persistentSaveSuccess) {
            Logger.Error("Failed to save primary dye {0} for item {1}", 
                Logger.Args(primaryColor, item.m_globalID));

            return;
        }
    }

    /// <summary>
    /// Applies the secondary dye color to the specified WizClientObjectItem.
    /// </summary>
    /// <param name="item">The WizClientObjectItem to apply the secondary dye color to.</param>
    /// <param name="secondaryColor">The secondary dye color to apply.</param>
    public static void ApplySecondaryDye(WizClientObjectItem item, DyeColor secondaryColor) {
        item.m_secondaryColor = (int) secondaryColor;

        var persistentSaveSuccess = WizardItemCollection.ApplySecondaryDye(item, (int) secondaryColor);
        if (!persistentSaveSuccess) {
            Logger.Error("Failed to save secondary dye {0} for item {1}", 
                Logger.Args(secondaryColor, item.m_globalID));

            return;
        }
    }

    /// <summary>
    /// Applies the specified dye colors to the given WizClientObjectItem.
    /// </summary>
    /// <param name="item">The WizClientObjectItem to apply the dye colors to.</param>
    /// <param name="texture">The primary dye color.</param>
    /// <param name="decal">The secondary dye color.</param>
    /// <param name="decal2">The tertiary dye color.</param>
    public static void ApplyAllDye(WizClientObjectItem item, DyeColor texture, DyeColor decal, DyeColor decal2) {
        item.m_primaryColor = (int) texture;
        item.m_secondaryColor = (int) decal;
        item.m_pattern = (int) decal2;

        var persistentSaveSuccess = WizardItemCollection.ApplyAllDye(item, (int) texture, (int) decal, (int) decal2);
        if (!persistentSaveSuccess) {
            Logger.Error("Failed to save all dye {0} for item {1}", 
                Logger.Args(texture, item.m_globalID));

            return;
        }
    }

}
