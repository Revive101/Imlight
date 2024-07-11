/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class NpcInventoryCollection {
    public const string CollectionName = "NpcInventory";
    private static readonly IDocumentStore s_store;

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
        using var session = s_store.OpenSession();

        npcInventory = session.Query<NPCInventory>(collectionName: CollectionName)
            .Where(x => x.TemplateID == templateID)
            .FirstOrDefault();

        return npcInventory != null;
    }
}
