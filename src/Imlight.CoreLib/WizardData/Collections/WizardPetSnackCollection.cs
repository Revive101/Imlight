/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Collections;

public static class WizardPetSnackCollection {

    public const string CollectionName = "WizardPetSnacks";
    private static readonly IDocumentStore s_store;

    static WizardPetSnackCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a pet snack to the WizardPetSnacks collection for a specific player.
    /// </summary>
    /// <param name="snack">The snack to be added.</param>
    /// <returns>True if the snack was successfully added, false otherwise.</returns>
    public static bool AddSnack(ClientPetSnackItem snack) {
        using var session = s_store.OpenSession();

        // Return false if this snack already exists.
        if (session.Query<ClientPetSnackItem>(collectionName: CollectionName)
            .Any(x => x.m_characterId == snack.m_characterId && x.m_globalID == snack.m_globalID)) {
            return false;
        }

        // Add the snack to the snacks collection.
        session.Store(snack);

        // Set the collection name in the metadata.
        var metadata = session.Advanced.GetMetadataFor(snack);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        // We're also going to add this snack to the character's snack list.
        // We don't want to query for the character because it will run the constructor again and we don't want that.
        // Instead, we're going to use a patch request to add the snack id to the character's snack list.
        var patchRequest = new PatchByQueryOperation(
            $"from Characters where CharId = '{snack.m_characterId}'" +
            $"update {{ this.{nameof(Wizard.PetSnackBehavior.SnackItemIds)}.Add('{snack.m_globalID}'); }}");
        s_store.Operations.Send(patchRequest);

        session.SaveChanges();
        
        return true;
    }

    /// <summary>
    /// Updates a pet snack in the WizardPetSnacks collection for a specific player.
    /// </summary>
    /// <param name="snack">The updated snack to be saved.</param>
    /// <returns>True if the snack was successfully updated, false otherwise.</returns>
    public static bool UpdateSnack(ClientPetSnackItem snack) {
        using var session = s_store.OpenSession();

        // Get the snack from the snacks collection.
        var associatedSnack = session
            .Query<ClientPetSnackItem>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_characterId == snack.m_characterId && x.m_globalID == snack.m_globalID);

        // If the snack was not found, return false.
        if (associatedSnack == null) {
            return false;
        }

        // Update the snack in the snacks collection.
        associatedSnack.m_quantity = snack.m_quantity;

        session.SaveChanges();
        
        return true;
    }

    /// <summary>
    /// Removes an snack from the WizardPetSnacks collection for a specific player.
    /// </summary>
    /// <param name="snack">The snack to be removed.</param>
    /// <returns>True if the snack was successfully removed, false otherwise.</returns>
    public static bool RemoveSnack(ClientPetSnackItem snack) {
        using var session = s_store.OpenSession();

        // Get the snack from the snacks collection.
        var associatedSnack = session
            .Query<ClientPetSnackItem>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_characterId == snack.m_characterId && x.m_globalID == snack.m_globalID);

        // If the snack was not found, return false.
        if (associatedSnack == null) {
            return false;
        }

        // Delete the snack from the snacks collection.
        session.Delete(associatedSnack);

        // We're also going to add this snack to the character's snack list.
        // We don't want to query for the character because it will run the constructor again and we don't want that.
        // Instead, we're going to use a patch request to add the snack id to the character's snack list.
        var patchRequest = new PatchByQueryOperation(
            $"from Characters where CharId = '{snack.m_characterId}'" +
            $"update {{ this.{nameof(Wizard.PetSnackBehavior.SnackItemIds)}.Remove('{snack.m_globalID}'); }}");

        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Removes an snack from the WizardPetSnacks collection.
    /// </summary>
    /// <param name="charId">The ID of the player.</param>
    /// <param name="snackId">The ID of the snack to remove.</param>
    /// <returns>True if the snack was successfully removed, false otherwise.</returns>
    public static bool RemoveSnack(ulong charId, ulong snackId) {
        using var session = s_store.OpenSession();

        // Get the snack from the snacks collection.
        var associatedSnack = session.Query<ClientPetSnackItem>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_characterId == charId && x.m_globalID == snackId);

        // If the snack was not found, return false.
        if (associatedSnack == null) {
            return false;
        }

        // Delete the snack from the snacks collection.
        session.Delete(associatedSnack);

        // We're also going to add this snack to the character's snack list.
        // We don't want to query for the character because it will run the constructor again and we don't want that.
        // Instead, we're going to use a patch request to add the snack id to the character's snack list.
        var patchRequest = new PatchByQueryOperation(
            $"from Characters where CharId = '{charId}'" +
            $"update {{ this.{nameof(Wizard.PetSnackBehavior.SnackItemIds)}.Remove('{snackId}'); }}");

        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Tries to retrieve the entire snack bag of a player.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="snackBag">The list of ClientPetSnackItem representing the player's WorldItem.</param>
    /// <returns>True if the inventory was successfully retrieved, false otherwise.</returns>
    public static bool TryGetWizardSnackBag(ulong playerId, out List<ClientPetSnackItem> snackBag) {
        using var session = s_store.OpenSession();

        // Get the snacks from the snacks collection.
        var snacks = session.Query<ClientPetSnackItem>(collectionName: CollectionName)
            .Where(x => x.m_characterId == playerId)
            .ToList();

        // If no snacks were found, set the snack bag to null and return false.
        if (snacks.Count == 0) {

            snackBag = null;
            return false;
        }

        // Set the snack bag to the retrieved snacks.
        snackBag = [.. snacks];

        return true;
    }

    /// <summary>
    /// Tries to delete the entire snack bag of a player.
    /// </summary>
    /// <param name="playerID">The ID of the player.</param>
    /// <returns>True if the player's snacks were deleted, false if the player had no snacks to delete.</returns>
    public static bool DeleteSnackBag(ulong playerId) {
        using var session = s_store.OpenSession();

        // Get the snacks from the snacks collection.
        var snacks = session.Query<ClientPetSnackItem>(collectionName: CollectionName)
            .Where(x => x.m_characterId == playerId)
            .ToList();

        // If no snacks were found, return false.
        if (snacks.Count == 0) {
            return false;
        }

        // Delete the snacks from the snacks collection.
        foreach (var snack in snacks) {
            session.Delete(snack);
        }

        session.SaveChanges();

        return true;
    }

}
