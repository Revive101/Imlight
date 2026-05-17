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

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using System.Collections.Concurrent;
using Raven.Client.Documents.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class NpcInventoryCollection {

    public const string CollectionName = "NpcInventory";
    private static readonly IDocumentStore s_store;

    private static readonly ConcurrentDictionary<ulong, NPCInventory> s_cachedInventories = new();
    private static bool s_isPreloaded = false;

    static NpcInventoryCollection() {
        s_store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds the inventory to the NPCInventory collection for a specific NPC.
    /// </summary>
    /// <param name="npcInventory">The inventory to be added.</param>
    public static void AddNpcInventory(NPCInventory npcInventory) {
        using var session = s_store.OpenSession();

        session.Store(npcInventory);
        var metadata = session.Advanced.GetMetadataFor(npcInventory);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Updates the inventory in the NPCInventory collection for a specific NPC.
    /// </summary>
    /// <param name="npcInventory">The inventory to update with.</param>
    /// <returns>True if the NPC inventory was updated, false if the NPC could not be found.</returns>
    public static bool UpdateNpcInventory(NPCInventory npcInventory) {
        using var session = s_store.OpenSession();

        // Check if the NPCInventory already exists
        var existingNpcInventory = session.Query<NPCInventory>(collectionName: CollectionName)
            .Where(x => x.TemplateID == npcInventory.TemplateID)
            .FirstOrDefault();

        if (existingNpcInventory != null) {
            existingNpcInventory.Inventory = npcInventory.Inventory;
        } else {
            return false;
        }

        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Attempts to retrieve the inventory using the template ID of the NPC.
    /// </summary>
    /// <param name="templateID">The template ID of the NPC to be retrieved.</param>
    /// <param name="npcInventory">The NPC inventory containing the list of inventory items.</param>
    /// <returns>True if the NPC inventory was found, false otherwise.</returns>
    public static bool TryGetNpcInventory(ulong templateID, out NPCInventory npcInventory) {
        if (!s_isPreloaded) {
            PreloadInventories();
        }

        if (s_cachedInventories.TryGetValue(templateID, out npcInventory)) {
            return true;
        }

        npcInventory = null;

        return false;
    }

    /// <summary>
    /// Preloads the inventories for the specified template IDs.
    /// </summary>
    public static void PreloadInventories() {
        using var session = s_store.OpenSession();

        var npcInventories = session.Query<NPCInventory>(collectionName: CollectionName)
            .ToList();

        foreach (var npcInventory in npcInventories) {
            s_cachedInventories.TryAdd(npcInventory.TemplateID, npcInventory);
        }

        s_isPreloaded = true;
    }
    
}
