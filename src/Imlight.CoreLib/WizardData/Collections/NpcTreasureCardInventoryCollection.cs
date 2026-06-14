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
*/

using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class NpcTreasureCardInventoryCollection {

    /// <summary>
    /// Attempts to retrieve the treasure card inventory using the template ID of the NPC.
    /// </summary>
    /// <param name="templateID">The template ID of the NPC to be retrieved.</param>
    /// <param name="inventory">The NPC treasure card inventory.</param>
    /// <returns>True if the inventory was found, false otherwise.</returns>
    public static bool TryGetInventory(ulong templateID, out NpcTreasureCardInventory inventory)
        => SpiralDB.TryGetTreasureCardInventory(templateID, out inventory);

    /// <summary>
    /// Preloads the inventories.
    /// SpiralDB loads all data at boot; this is a no-op kept for API compatibility.
    /// </summary>
    public static void PreloadInventories() { }

}
