/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imlight.Server.WizardData.Models;
using Raven.Client.Documents;

namespace Imlight.Server.WizardData.Implementations;

public static class WorldDataCollection
{
    private const string CollectionName = "WorldData";
    private static readonly IDocumentStore Store;
    private static WizardWorldData _retrievedWorldData;

    static WorldDataCollection()
    {
        Store = DocumentStoreSingleton.Store;
    }

    /// <summary>
    /// Retrieves the world data from the database, or the cached version if it has already been retrieved.
    /// </summary>
    /// <returns></returns>
    public static WizardWorldData GetWorldData()
    {
        // The server should only ever have one world data object, so we can cache it. 
        if (_retrievedWorldData is not null)
            return _retrievedWorldData;
        
        using var session = Store.OpenSession();
        var worldData = session
            .Query<WizardWorldData>(collectionName: CollectionName)
            .FirstOrDefault();
        
        _retrievedWorldData = worldData;
        return worldData;
    }
    
    /// <summary>
    /// Updates the world data in the database.
    /// </summary>
    /// <param name="worldData"></param>
    public static void UpdateWorldData(WizardWorldData worldData)
    {
        using var session = Store.OpenSession();
        
        // Store the world data object in the database.
        session.Store(worldData);
        var metadata = session.Advanced.GetMetadataFor(worldData);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        session.SaveChanges();
    }
}