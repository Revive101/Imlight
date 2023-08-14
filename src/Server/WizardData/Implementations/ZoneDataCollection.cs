using System.Linq;
using Raven.Client.Documents;

namespace Imlight.Server.WizardData.Implementations;

public static class ZoneDataCollection
{
    private const string CollectionName = "ZoneData";
    private static readonly IDocumentStore Store;

    static ZoneDataCollection()
    {
        Store = DocumentStoreSingleton.Store;
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