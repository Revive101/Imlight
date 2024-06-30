/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class DynamodCollection {
    public const string CollectionName = "DynamicMod";

    private static readonly IDocumentStore s_store;

    static DynamodCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a DynamodSet to the collection.
    /// </summary>
    /// <param name="dynamodSet">The DynamodSet to add.</param>
    /// <returns>
    ///   <c>true</c> if the DynamodSet was added successfully;
    ///   <c>false</c> if the DynamodSet already exists in the collection.
    /// </returns>
    public static bool AddDynamodSet(DynamodSet dynamodSet) {
        using var session = s_store.OpenSession();

        // Ensure that this dynamod set does not already exist
        var dynamodSetExists = session.Query<DynamodSet>(collectionName: CollectionName)
            .Any(ds => ds.CharId == dynamodSet.CharId);
        if (dynamodSetExists) {
            return false;
        }

        session.Store(dynamodSet);
        var metaData = session.Advanced.GetMetadataFor(dynamodSet);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Removes a DynamodSet from the collection based on the specified character ID.
    /// </summary>
    /// <param name="charId">The character ID of the DynamodSet to remove.</param>
    /// <returns><c>true</c> if the DynamodSet was successfully removed; otherwise, <c>false</c>.</returns>
    public static bool RemoveDynamodSet(ulong charId) {
        using var session = s_store.OpenSession();

        var dynamodSet = session.Query<DynamodSet>(collectionName: CollectionName)
            .FirstOrDefault(ds => ds.CharId == charId);

        if (dynamodSet == null) {
            return false;
        }

        session.Delete(dynamodSet);
        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Retrieves a DynamodSet from the collection based on the specified character ID.
    /// </summary>
    /// <param name="charId">The character ID of the DynamodSet to retrieve.</param>
    /// <returns>The DynamodSet with the specified character ID, or <c>null</c> if not found.</returns>
    public static DynamodSet GetDynamodSet(ulong charId) {
        using var session = s_store.OpenSession();

        return session.Query<DynamodSet>(collectionName: CollectionName)
            .FirstOrDefault(ds => ds.CharId == charId);
    }

    /// <summary>
    /// Updates a DynamodSet in the database.
    /// </summary>
    /// <param name="dynamodSet">The DynamodSet to update.</param>
    /// <returns>True if the DynamodSet was successfully updated, false otherwise.</returns>
    public static bool UpdateDynamodSet(DynamodSet dynamodSet) {
        using var session = s_store.OpenSession();

        // Ensure that this dynamod set exists. If not, we'll add it instead.
        var dynamodSetExists = session.Query<DynamodSet>(collectionName: CollectionName)
            .Any(ds => ds.CharId == dynamodSet.CharId);
        if (!dynamodSetExists) {
            AddDynamodSet(dynamodSet);
        }

        session.Store(dynamodSet);
        var metaData = session.Advanced.GetMetadataFor(dynamodSet);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
        return true;
    }
}
