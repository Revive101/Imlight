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

public static class NpcSpellInventoryCollection {

    public const string CollectionName = "NpcSpellInventory";
    private static readonly IDocumentStore s_store;

    private static readonly ConcurrentDictionary<ulong, NPCSpellInventory> s_npcSpellInventories = [];
    private static bool s_isPreloaded = false;

    static NpcSpellInventoryCollection() {
        s_store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds the spell inventory to the NPCSpellInventory collection for a specific NPC.
    /// </summary>
    /// <param name="npcSpellInventory">The inventory to be added.</param>
    public static void AddNpcSpellInventory(NPCSpellInventory npcSpellInventory) {
        using var session = s_store.OpenSession();

        session.Store(npcSpellInventory);
        var metadata = session.Advanced.GetMetadataFor(npcSpellInventory);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Updates the spell inventory in the NPCSpellInventory collection for a specific NPC.
    /// </summary>
    /// <param name="npcSpellInventory">The inventory to update with.</param>
    /// <returns>True if the NPC inventory was updated, false if the NPC could not be found.</returns>
    public static bool UpdateNpcInventory(NPCSpellInventory npcSpellInventory) {
        using var session = s_store.OpenSession();

        // Check if the NPCInventory already exists
        var existingNpcSpellInventory = session.Query<NPCSpellInventory>(collectionName: CollectionName)
            .Where(x => x.TemplateID == npcSpellInventory.TemplateID)
            .FirstOrDefault();

        if (existingNpcSpellInventory != null) {
            existingNpcSpellInventory.Spells = npcSpellInventory.Spells;
        }
        else {
            return false;
        }

        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Attempts to retrieve the spell inventory using the template ID of the NPC.
    /// </summary>
    /// <param name="templateID">The template ID of the NPC to be retrieved.</param>
    /// <param name="npcSpellInventory">The NPC spell inventory containing the list of spell.</param>
    /// <returns>True if the NPC inventory was found, false otherwise.</returns>
    public static bool TryGetNpcInventory(ulong templateID, out NPCSpellInventory npcSpellInventory) {
        if (!s_isPreloaded) {
            PreloadNpcSpellInventories();
        }

        if (s_npcSpellInventories.TryGetValue(templateID, out npcSpellInventory)) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Preloads the NPC spell inventories into memory.
    /// </summary>
    public static void PreloadNpcSpellInventories() {
        using var session = s_store.OpenSession();

        var npcSpellInventories = session.Query<NPCSpellInventory>(collectionName: CollectionName)
            .ToList();

        foreach (var npcSpellInventory in npcSpellInventories) {
            s_npcSpellInventories.TryAdd(npcSpellInventory.TemplateID, npcSpellInventory);
        }

        s_isPreloaded = true;
    }
    
}
