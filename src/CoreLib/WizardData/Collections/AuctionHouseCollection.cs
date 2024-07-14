/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using System.Collections.Generic;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Collections;
internal class AuctionHouseCollection {
    public const string CollectionName = "AuctionHouse";

    private static readonly IDocumentStore s_store;
    private static bool s_isInitialized;
    private static AuctionHouseModel s_model;

    static AuctionHouseCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Saves new Auction House model/entries to the database.
    /// </summary>
    /// <param name="model"></param>
    public static void SaveAuctionHouseModel(AuctionHouseModel model) {
        using var session = s_store.OpenSession();

        // Delete the old Auction House entries.
        var oldModel = session
            .Query<AuctionHouseModel>(collectionName: CollectionName)
            .FirstOrDefault();
        if (oldModel is not null) {
            session.Delete(oldModel);
        }

        // Store the new one and set its metadata.
        session.Store(model);
        var metadata = session.Advanced.GetMetadataFor(model);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        s_model = model;
        s_isInitialized = true;

        session.SaveChanges();
    }

    /// <summary>
    /// Retrieves all Auction House entries available.
    /// </summary>
    /// <returns>A model of all available Auction House entries, or null if none found.</returns>
    public static AuctionHouseModel GetAllAuctionHouseEntries() {
        if (s_isInitialized) {
            return s_model;
        }

        using var session = s_store.OpenSession();

        s_model = session
            .Query<AuctionHouseModel>(collectionName: CollectionName)
            .FirstOrDefault();

        if (s_model is null) {
            s_model = new AuctionHouseModel();
            SaveAuctionHouseModel(s_model);
        }

        s_isInitialized = true;
        return s_model;
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

        if (s_model is null) {
            return null;
        }

        var auctionHouseEntry = s_model.AuctionHouseEntries
            .FirstOrDefault(x => x.m_templateID == templateID);

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

        s_model.AuctionHouseEntries.Add(entry);
        SaveAuctionHouseModel(s_model);
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

        var entry = s_model.AuctionHouseEntries
            .FirstOrDefault(x => x.m_templateID == templateID);

        var removed = s_model.AuctionHouseEntries.RemoveAll(x => x.m_templateID == templateID);
        SaveAuctionHouseModel(s_model);

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
