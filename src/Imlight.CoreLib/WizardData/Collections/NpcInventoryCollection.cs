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

public static class NpcInventoryCollection {

    /// <summary>
    /// Attempts to retrieve the inventory using the template ID of the NPC.
    /// </summary>
    /// <param name="templateID">The template ID of the NPC to be retrieved.</param>
    /// <param name="npcInventory">The NPC inventory containing the list of inventory items.</param>
    /// <returns>True if the NPC inventory was found, false otherwise.</returns>
    public static bool TryGetNpcInventory(ulong templateID, out NPCInventory npcInventory) 
        => SpiralDB.TryGetNpcInventory(templateID, out npcInventory);

    /// <summary>
    /// Preloads the inventories.
    /// SpiralDB loads all data at boot; this is a no-op kept for API compatibility.
    /// </summary>
    public static void PreloadInventories() { }

}
