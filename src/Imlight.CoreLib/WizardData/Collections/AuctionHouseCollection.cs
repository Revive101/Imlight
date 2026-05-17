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
using System.Collections.Generic;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Collections;

internal class AuctionHouseCollection {
    
    public const string CollectionName = "AuctionHouse";

    private static readonly IDocumentStore s_store;
    private static bool s_isInitialized;
    private static List<AuctionHouseEntry> s_entries;

    static AuctionHouseCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Retrieves all Auction House entries available.
    /// </summary>
    /// <returns>A list of all available Auction House entries, or null if none found.</returns>
    public static List<AuctionHouseEntry> GetAllAuctionHouseEntries() {
        if (s_isInitialized) {
            return s_entries;
        }

        using var session = s_store.OpenSession();

        // Retrieve all Auction House entries.
        s_entries = [.. session.Query<AuctionHouseEntry>(collectionName: CollectionName)];

        if (s_entries is null) {
            s_entries = [];
        }

        s_isInitialized = true;

        return s_entries;
    }

    /// <summary>
    /// Retrieves an Auction House entry by template ID.
    /// </summary>
    /// <param name="templateID">The template ID of an object.</param>
    /// <returns>The Auction House entry, or null if not found.</returns>
    public static AuctionHouseEntry GetAuctionHouseEntry(ulong templateID) {
        if (!s_isInitialized) {
            GetAllAuctionHouseEntries();
        }

        if (s_entries is null) {
            return null;
        }

        var auctionHouseEntry = s_entries.FirstOrDefault(x => x.m_templateID == templateID);

        return auctionHouseEntry;
    }

    /// <summary>
    /// Adds an Auction House entry to the collection.
    /// </summary>
    /// <param name="entry">The Auction House entry to add.</param>
    public static void AddAuctionHouseEntry(AuctionHouseEntry entry) {
        if (!s_isInitialized) {
            GetAllAuctionHouseEntries();
        }

        using var session = s_store.OpenSession();

        session.Store(entry);
        var metaData = session.Advanced.GetMetadataFor(entry);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        s_entries.Add(entry);

        session.SaveChanges();
    }


    /// <summary>
    /// Removes an Auction House entry record from the collection based on the specified template ID.
    /// </summary>
    /// <param name="templateID">The template ID of the object to remove the entry for.</param>
    /// <returns>True if the Auction House entry was successfully removed, false otherwise.</returns>
    public static bool RemoveAuctionHouseEntry(ulong templateID) {
        if (!s_isInitialized) {
            GetAllAuctionHouseEntries();
        }

        using var session = s_store.OpenSession();

        var entry = session.Query<AuctionHouseEntry>(collectionName: CollectionName)
            .FirstOrDefault(entry => entry.m_templateID == templateID);

        // Remove the entry from the collection.
        if (entry != null) {
            session.Delete(entry);
            session.SaveChanges();
        }

        var removed = s_entries.RemoveAll(x => x.m_templateID == templateID);
        return removed != 0;
    }

    /// <summary>
    /// Updates the entry in the Auction House collection for a specific template ID.
    /// </summary>
    /// <param name="entry">The new Auction House entry to update with.</param>
    /// <returns>True if the Auction House entry was updated, false if the entry could not be found.</returns>
    public static bool UpdateAuctionHouseEntry(AuctionHouseEntry entry) {
        if (!s_isInitialized) {
            GetAllAuctionHouseEntries();
        }

        var removeSuccess = RemoveAuctionHouseEntry(entry.m_templateID);

        if (!removeSuccess) {
            return false;
        }

        AddAuctionHouseEntry(entry);

        return true;
    }

}
