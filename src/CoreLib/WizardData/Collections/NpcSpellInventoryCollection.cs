/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class NpcSpellInventoryCollection {
    public const string CollectionName = "NpcSpellInventory";
    private static readonly IDocumentStore s_store;

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
        using var session = s_store.OpenSession();

        npcSpellInventory = session.Query<NPCSpellInventory>(collectionName: CollectionName)
            .Where(x => x.TemplateID == templateID)
            .FirstOrDefault();

        return npcSpellInventory != null;
    }
}
