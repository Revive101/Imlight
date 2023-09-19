/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography.X509Certificates;
using Imlight.Common.Configuration;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.WizardData;
using Raven.Client.Documents;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using Raven.Client.ServerWide;
using Raven.Embedded;

namespace Imlight.Server.Shared.WizardData.Implementations;

public class WorldDatabase : RavenDatabaseSingleton<WorldDatabase>
{
    protected readonly byte MaxNumberOfRequestsPerSession
        = ConfigurationManager.Settings.DatabaseMaxNumberOfRequestsPerSession;
    protected readonly byte RequestTimeoutInSeconds
        = ConfigurationManager.Settings.DatabaseRequestTimeoutInSeconds;
    protected readonly byte WaitForNonStaleResultsTimeoutInSeconds
        = ConfigurationManager.Settings.DatabaseWaitForNonStaleResultsTimeout;

    protected override X509Certificate2 Certificate { get; }
        = ConfigurationManager.Settings.WorldDatabaseUrl == string.Empty
            ? null
            : new X509Certificate2(ConfigurationManager.Settings.WorldDatabaseCertificatePath);
    protected override string DatabaseName { get; } = ConfigurationManager.Settings.WorldDatabaseName;
    protected override string Url { get; } = ConfigurationManager.Settings.WorldDatabaseUrl;

    protected override IDocumentStore CreateStore()
    {
        // If this is the first time we're creating the store, we need to create the database.
        Log.Information("Initializing remote RavenDB database for the first time..");
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

    protected override IDocumentStore CreateEmbeddedStore()
    {
        EmbeddedDatabaseManager.Start();
        return EmbeddedDatabaseManager.GetDocumentStore(DatabaseName);
    }
}
