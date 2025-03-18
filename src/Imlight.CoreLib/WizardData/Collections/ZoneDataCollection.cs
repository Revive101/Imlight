/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.World;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class ZoneDataCollection {

    private const string CollectionName = "ZoneTransfer";

    private static readonly IDocumentStore s_store;

    static ZoneDataCollection() {
        s_store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds the zone data to the database.
    /// </summary>
    /// <param name="zoneData"></param>
    public static void AddZoneData(WizardZoneData zoneData) {
        using var session = s_store.OpenSession();

        // Store the zone data in the database.
        session.Store(zoneData);
        var metadata = session.Advanced.GetMetadataFor(zoneData);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Updates the zone data in the database.
    /// </summary>
    /// <param name="zoneName"></param>
    /// <returns></returns>
    public static WizardZoneData GetZoneData(string zoneName) {
        using var session = s_store.OpenSession();

        // Retrieve the zone data from the database.
        var zoneData = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .FirstOrDefault(x => x.ZoneName == zoneName);
            
        return zoneData;
    }

}
