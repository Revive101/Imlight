/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imlight.Server.Shared.WizardData.Models;
using Raven.Client.Documents;

namespace Imlight.Server.Shared.WizardData.Implementations;

public static class ZoneDataCollection
{
    private const string CollectionName = "ZoneData";
    private static readonly IDocumentStore Store;

    static ZoneDataCollection()
    {
        Store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds the zone data to the database.
    /// </summary>
    /// <param name="zoneData"></param>
    public static void AddZoneData(WizardZoneData zoneData)
    {
        using var session = Store.OpenSession();

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
    public static WizardZoneData GetZoneData(string zoneName)
    {
        using var session = Store.OpenSession();

        // Retrieve the zone data from the database.
        var zoneData = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .FirstOrDefault(x => x.ZoneName == zoneName);
        return zoneData;
    }
}