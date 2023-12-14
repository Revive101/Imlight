/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class WizardItemCollection {
    public const string CollectionName = "WizardItems";
    private static readonly IDocumentStore s_store;

    static WizardItemCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds an item to the WorldItem collection for a specific player.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="item">The item to be added.</param>
    public static bool AddItem(WizClientObjectItem item) {
        using var session = s_store.OpenSession();

        // Return false if this item already exists.
        if (session.Query<WizClientObjectItem>(CollectionName)
            .Any(x => x.m_characterId == item.m_characterId && x.m_globalID == item.m_globalID)) {
            return false;
        }

        // Add the item to the items collection.
        session.Store(item);

        // Set the collection name in the metadata.
        var metadata = session.Advanced.GetMetadataFor(item);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        // We're also going to add this item to the character's item list.
        // We don't want to query for the character because it will run the constructor again and we don't want that.
        // Instead, we're going to use a patch request to add the item id to the character's item list.
        var patchRequest = new PatchByQueryOperation(
            $"from Characters where CharId = '{item.m_characterId}'" +
            $"update {{ this.{nameof(Wizard.InventoryItemIds)}.Add('{item.m_globalID}'); }}");
        s_store.Operations.Send(patchRequest);

        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Adds items to the WorldItem collection for a specific player.
    /// Note that this method does not add the items to the character's item list.
    /// It is intended to be called from the character creation process, meaning that the character's item list will be serialized
    /// and saved to the database once it is added to an account.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="items">The items to be added.</param>
    public static bool AddDefaultItems(IEnumerable<WizClientObjectItem> items) {
        using var session = s_store.OpenSession();

        // Add the items to the items collection.
        foreach (var item in items)
        {
            // Return false if this item already exists.
            if (session.Query<WizClientObjectItem>(CollectionName)
                .Any(x => x.m_characterId == item.m_characterId && x.m_globalID == item.m_globalID)) {
                return false;
            }

            session.Store(item);

            // Set the collection name in the metadata.
            var metadata = session.Advanced.GetMetadataFor(item);
            metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        }

        // Save the changes.
        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Removes an item from the WorldItem collection for a specific player.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="item">The item to be removed.</param>
    /// <returns>True if the item was successfully removed, false otherwise.</returns>
    public static bool RemoveItem(WizClientObjectItem item) {
        using var session = s_store.OpenSession();

        // Get the item from the items collection.
        var associatedItem = session.Query<WizClientObjectItem>(CollectionName)
            .FirstOrDefault(x => x.m_characterId == item.m_characterId && x.m_globalID == item.m_globalID);

        // If the item was not found, return false.
        if (associatedItem == null) {
            return false;
        }

        // Delete the item from the items collection.
        session.Delete(associatedItem);

        // We're also going to add this item to the character's item list.
        // We don't want to query for the character because it will run the constructor again and we don't want that.
        // Instead, we're going to use a patch request to add the item id to the character's item list.
        var patchRequest = new PatchByQueryOperation(
            $"from Characters where CharId = '{item.m_characterId}'" +
            $"update {{ this.{nameof(Wizard.InventoryItemIds)}.Remove('{item.m_globalID}'); }}");

        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Removes an item from the WorldItem collection.
    /// </summary>
    /// <param name="charId">The ID of the player.</param>
    /// <param name="itemId">The ID of the item to remove.</param>
    /// <returns>True if the item was successfully removed, false otherwise.</returns>
    public static bool RemoveItem(ulong charId, ulong itemId) {
        using var session = s_store.OpenSession();

        // Get the item from the items collection.
        var associatedItem = session.Query<WizClientObjectItem>(CollectionName)
            .FirstOrDefault(x => x.m_characterId == charId && x.m_globalID == itemId);

        // If the item was not found, return false.
        if (associatedItem == null) {
            return false;
        }

        // Delete the item from the items collection.
        session.Delete(associatedItem);

        // We're also going to add this item to the character's item list.
        // We don't want to query for the character because it will run the constructor again and we don't want that.
        // Instead, we're going to use a patch request to add the item id to the character's item list.
        var patchRequest = new PatchByQueryOperation(
            $"from Characters where CharId = '{charId}'" +
            $"update {{ this.{nameof(Wizard.InventoryItemIds)}.Remove('{itemId}'); }}");

        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Tries to retrieve the entire inventory of a player.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="WorldItem">The list of WizClientObjectItem representing the player's WorldItem.</param>
    /// <returns>True if the inventory was successfully retrieved, false otherwise.</returns>
    public static bool TryGetWizardInventory(ulong playerId, out List<WizClientObjectItem> inventory) {
        using var session = s_store.OpenSession();

        // Get the items from the items collection.
        var items = session.Query<WizClientObjectItem>(collectionName: CollectionName)
            .Where(x => x.m_characterId == playerId)
            .ToList();

        // If no items were found, set the WorldItem to null and return false.
        if (items.Count == 0) {
            inventory = null;
            return false;
        }

        // Set the WorldItem to the retrieved items.
        inventory = items.ToList();
        return true;
    }
}
