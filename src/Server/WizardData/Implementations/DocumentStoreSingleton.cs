using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Imlight.Common.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using WizUnraveler.ObjectProperty.JSON;

namespace Imlight.Server.WizardData.Implementations;

/// <summary>
/// Singleton for the RavenDB document store.
/// </summary>
public static class DocumentStoreSingleton
{
    public const byte MaxNumberOfRequestsPerSession = 16;
    public const byte RequestTimeoutInSeconds = 90;
    public const byte WaitForNonStaleResultsTimeoutInSeconds = 5;

    private static readonly Lazy<IDocumentStore> store = new(CreateStore);
    private static readonly X509Certificate2 certificate 
        = new("/home/makima/.ssh/dragon-database/dragon.admin/dragon.admin.pfx");
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
                MaxNumberOfRequestsPerSession = MaxNumberOfRequestsPerSession,
                UseOptimisticConcurrency = true,
                RequestTimeout = TimeSpan.FromSeconds(RequestTimeoutInSeconds),
                WaitForNonStaleResultsTimeout = TimeSpan.FromSeconds(WaitForNonStaleResultsTimeoutInSeconds),
                
                // RavenDb Studio cannot properly display ulong values, so we convert them to strings. JavaScript moment.
                Serialization = new NewtonsoftJsonSerializationConventions()
                {
                    CustomizeJsonSerializer = s => s.Converters.Add(new ULongToStringConverter())
                }
            },
            Certificate = certificate,
        }.Initialize();

        Log.Information("Database initialized.");

        return store;
    }
}