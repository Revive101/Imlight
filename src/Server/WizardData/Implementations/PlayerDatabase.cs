using System;
using System.Security.Cryptography.X509Certificates;
using Imlight.Common.Configuration;
using Imlight.Common.Utilities;
using Raven.Client.Documents;
using Raven.Client.Json.Serialization.NewtonsoftJson;

namespace Imlight.Server.WizardData.Implementations;

public class PlayerDatabase : RavenDatabaseSingleton<PlayerDatabase>, IRavenDatabaseAccessor
{
    protected readonly byte MaxNumberOfRequestsPerSession 
        = ConfigurationManager.Settings.DatabaseMaxNumberOfRequestsPerSession;
    protected readonly byte RequestTimeoutInSeconds
        = ConfigurationManager.Settings.DatabaseRequestTimeoutInSeconds;
    protected readonly byte WaitForNonStaleResultsTimeoutInSeconds 
        = ConfigurationManager.Settings.DatabaseWaitForNonStaleResultsTimeout;
    
    public X509Certificate2 Certificate { get; } = new(ConfigurationManager.Settings.PlayerDatabaseCertificatePath);
    public string DatabaseName { get; } = ConfigurationManager.Settings.PlayerDatabaseName;
    public string Url { get; } = ConfigurationManager.Settings.PlayerDatabaseUrl;
    
    private IDocumentStore _store;
    public IDocumentStore Store => _store ??= CreateStore();
    
    protected override IDocumentStore CreateStore()
    {
        // If this is the first time we're creating the store, we need to create the database.
        Log.Information("Initializing RavenDB database for the first time..");
        Log.Information("Database name: {0}", Log.Args(DatabaseName));

        var store = new DocumentStore
        {
            Urls = new[] { Url },
            Database = DatabaseName,
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
            Certificate = Certificate
        }.Initialize();

        Log.Information("Database initialized.");

        return store;
    }
}