using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.Login.Models;
using Raven.Client.Documents;

namespace Imlight.Server.WizardData;

/// <summary>
/// Singleton for the RavenDB document store.
/// </summary>
internal static class DocumentStoreSingleton
{
    private static readonly Lazy<IDocumentStore> store = new(CreateStore);
    private static readonly X509Certificate2 certificate = new("/home/makima/.ssh/dragon-database/dragon.pfx");
    private static readonly string databaseName = "Imlight";
    private static readonly string url = "https://a.voidly.ravendb.community";
    
    public static IDocumentStore Store => store.Value;

    private static IDocumentStore CreateStore()
    {
        // If this is the first time we're creating the store, we need to create the database.
        Log.Information("Initializing RavenDB database for the first time..");
        Log.Information("Database name: {0}", Log.Args(databaseName));
        
        var store = new DocumentStore
        {
            Urls = new[] { url },
            Database = databaseName,
            Conventions =
            {
                MaxNumberOfRequestsPerSession = 16,
                UseOptimisticConcurrency = true,
            },
            Certificate = certificate,
        }.Initialize();

        Log.Information("Database initialized.");

        return store;
    }
}