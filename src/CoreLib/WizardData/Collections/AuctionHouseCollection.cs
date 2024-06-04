/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using System.Collections.Generic;
using Imlight.CoreLib.WizardData.Databases;
using Raven.Client.Documents;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Collections;
internal class AuctionHouseCollection {
    public const string CollectionName = "AuctionHouse";
    private static readonly IDocumentStore s_store;

    static AuctionHouseCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Retrieves an Auction House entry by template ID.
    /// </summary>
    /// <param name="templateID">The template ID of an object.</param>
    /// <returns>The Auction House entry, or null if not found.</returns>
    public static AuctionHouseEntry GetAuctionHouseEntry(ulong templateID) {
        using var session = s_store.OpenSession();

        var auctionHouseEntry = session.Query<AuctionHouseEntry>(collectionName: CollectionName)
            .FirstOrDefault(x => x.m_templateID == templateID);

        return auctionHouseEntry;
    }

    /// <summary>
    /// Retrieves all Auction House entries available.
    /// </summary>
    /// <returns>A list of all available Auction House entries, or null if none found.</returns>
    public static List<AuctionHouseEntry> GetAllAuctionHouseEntries() {
        using var session = s_store.OpenSession();

        var auctionHouseEntries = session.Query<AuctionHouseEntry>(collectionName: CollectionName)
            .ToList();

        return auctionHouseEntries;
    }

    /// <summary>
    /// Adds an Auction House entry to the collection.
    /// </summary>
    /// <param name="entry">The Auction House entry to add.</param>
    public static void AddAuctionHouseEntry(AuctionHouseEntry entry) {
        using var session = s_store.OpenSession();

        session.Store(entry);
        var metaData = session.Advanced.GetMetadataFor(entry);
        metaData[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }


    /// <summary>
    /// Removes an Auction House entry record from the collection based on the specified template ID.
    /// </summary>
    /// <param name="templateID">The template ID of the object to remove the entry for.</param>
    /// <returns>True if the Auction House entry was successfully removed, false otherwise.</returns>
    public static bool RemoveAuctionHouseEntry(ulong templateID) {
        using var session = s_store.OpenSession();
        var entry = session.Query<AuctionHouseEntry>(collectionName: CollectionName)
            .Where(x => x.m_templateID == templateID)
            .FirstOrDefault();

        if (entry != null) {
            session.Delete(entry);
            session.SaveChanges();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates the entry in the Auction House collection for a specific template ID.
    /// </summary>
    /// <param name="entry">The new Auction House entry to udpate with.</param>
    /// <returns>True if the Auction House entry was updated, false if the entry could not be found.</returns>
    public static bool UpdateAuctionHouseEntry(AuctionHouseEntry entry) {
        using var session = s_store.OpenSession();

        // Check if the AuctionHouseEntry already exists
        var existingEntry = session.Query<AuctionHouseEntry>(collectionName: CollectionName)
            .Where(x => x.m_templateID == entry.m_templateID)
            .FirstOrDefault();

        if (existingEntry != null) {
            existingEntry.m_templateID = entry.m_templateID;
            existingEntry.m_numForSale = entry.m_numForSale;
        }
        else {
            return false;
        }

        session.SaveChanges();
        return true;
    }

}
