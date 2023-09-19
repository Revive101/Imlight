/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Security.Cryptography.X509Certificates;
using DragonZoneTool.Models;
using Raven.Client.Documents;
using Serilog;

namespace DragonZoneTool.Managers;

public static class DragonDatabaseManager
{
    private static X509Certificate2 Certificate
        = new($"{Directory.GetCurrentDirectory()}/input/worlddata.client.certificate.pfx");
    private static string DatabaseName = "Imlight";
    private static string Url = "https://a.voidly.ravendb.community";
    private static string CollectionName = "ZoneData";
    
    private static IDocumentStore? _store;
    public static IDocumentStore? Store => _store ??= CreateStore();
    
    private static IDocumentStore? CreateStore()
    {
        Log.Information("Initializing remote RavenDB database for the first time..");
        Log.Information("Database name: {Name}", DatabaseName);

        var store = new DocumentStore
        {
            Urls = new[] { Url },
            Database = DatabaseName,
            Conventions =
            {
                MaxNumberOfRequestsPerSession = 16,
                UseOptimisticConcurrency = true,
                RequestTimeout = TimeSpan.FromSeconds(90),
                WaitForNonStaleResultsTimeout = TimeSpan.FromSeconds(90),
            },
            Certificate = Certificate
        }.Initialize();

        Log.Information("Database initialized");

        return store;
    }
    
    public static WizardZoneData? GetZoneData(string zoneName)
    {
        if (Store == null) 
            return null;
        
        // Retrieve the zone data from the database.
        using var session = Store.OpenSession();
        var zoneData = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .FirstOrDefault(x => x.ZoneName == zoneName);
        
        return zoneData;
    }
    
    public static WizardTeleportData? GetExistingTeleport(string zoneName, string triggerName)
    {
        var zoneData = GetZoneData(zoneName);
        return zoneData is null 
            ? null 
            : zoneData!.Teleports.FirstOrDefault(teleport => teleport.TriggerName == triggerName);
    }
    
    public static void DeleteExistingTeleport(string zoneName, string triggerName)
    {
        using var session = Store!.OpenSession();
        var zoneData = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .FirstOrDefault(x => x.ZoneName == zoneName);

        if (zoneData == null)
            return;
        
        var teleport = zoneData.Teleports.FirstOrDefault(x => x.TriggerName == triggerName);
        if (teleport == null)
            return;
        
        zoneData.Teleports.Remove(teleport);
        session.SaveChanges();
    }

    public static void AddNewTeleport(string zoneName, string triggerName, ServerTypeCache.ResTeleport result)
    {
        using var session = Store!.OpenSession();
        var zoneData = session
            .Query<WizardZoneData>(collectionName: CollectionName)
            .FirstOrDefault(x => x.ZoneName == zoneName);

        // If no zone data was found for this zone, create a new one.
        if (zoneData == null)
        {
            zoneData = new WizardZoneData
            {
                ZoneName = zoneName,
                Teleports = new List<WizardTeleportData>()
            };
            
            session.Store(zoneData);
            
            // Set collection metadata.
            var metadata = session.Advanced.GetMetadataFor(zoneData);
            metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        }
        
        zoneData.Teleports.Add(new WizardTeleportData
        {
            TriggerName = triggerName,
            Teleport = result
        });
        
        session.SaveChanges();
    }
}