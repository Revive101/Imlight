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
    /// Removes a specific Dynamod from a DynamodSet based on the character ID and client tag.
    /// </summary>
    /// <param name="charId">The character ID of the DynamodSet.</param>
    /// <param name="clientTag">The client tag of the Dynamod to remove.</param>
    /// <returns><c>true</c> if the Dynamod was successfully removed; otherwise, <c>false</c>.</returns>
    public static bool RemoveDynamod(ulong charId, string clientTag) {
        using var session = s_store.OpenSession();

        var dynamodSet = session.Query<DynamodSet>(collectionName: CollectionName)
            .FirstOrDefault(ds => ds.CharId == charId);

        if (dynamodSet is null) {
            return false;
        }

        var removed = dynamodSet.RemoveDynamod(clientTag);
        if (!removed) {
            return false;
        }

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
    public static bool UpdateDynamodSet(DynamodSet dynamodSet, Dynamod newDynamod) {
        using var session = s_store.OpenSession();

        // Ensure that this dynamod set exists. If not, we'll add it instead.
        var existingDynamodSet = session.Query<DynamodSet>(collectionName: CollectionName)
            .FirstOrDefault(ds => ds.CharId == dynamodSet.CharId);
        if (existingDynamodSet is null) {
            dynamodSet.AddDynamod(newDynamod);

            return AddDynamodSet(dynamodSet);
        }

        existingDynamodSet.AddDynamod(newDynamod);

        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Deletes all Dynamod sets for the specified character ID.
    /// </summary>
    /// <param name="charId">The character ID of the DynamodSets to be deleted.</param>
    /// <returns><c>true</c> if the DynamodSets were successfully deleted; otherwise, <c>false</c>.</returns>
    public static bool DeleteAllDynamodSets(ulong charId) {
        using var session = s_store.OpenSession();

        // Get the sets from the dynamod collection.
        var dynamodSets = session.Query<DynamodSet>(collectionName: CollectionName)
            .Where(ds => ds.CharId == charId)
            .ToList();

        // If no sets were found, return false.
        if (dynamodSets.Count == 0) {
            return false;
        }

        // Delete the sets from the dynamod collection.
        foreach (var dynamodSet in dynamodSets) {
            session.Delete(dynamodSet);
        }

        // Save the changes.
        session.SaveChanges();

        return true;
    }

}
