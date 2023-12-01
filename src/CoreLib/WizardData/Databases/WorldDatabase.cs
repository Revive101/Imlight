/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography.X509Certificates;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.CoreLib.WizardData.Implementations;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Databases;

public class WorldDatabase : RavenDatabaseSingleton<WorldDatabase> {
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

    protected override IDocumentStore CreateStore() {
        // If this is the first time we're creating the store, we need to create the database.
        Logger.Information("Initializing remote RavenDB database for the first time..");
        Logger.Information("Database name: {0}", Logger.Args(DatabaseName));

        var store = new DocumentStore {
            Urls = new[] { Url },
            Database = DatabaseName,
            Conventions =
            {
                MaxNumberOfRequestsPerSession = MaxNumberOfRequestsPerSession,
                UseOptimisticConcurrency = true,
                RequestTimeout = TimeSpan.FromSeconds(RequestTimeoutInSeconds),
                WaitForNonStaleResultsTimeout = TimeSpan.FromSeconds(WaitForNonStaleResultsTimeoutInSeconds),
            },
            Certificate = Certificate
        }.Initialize();

        Logger.Information("Database initialized.");

        return store;
    }

    protected override IDocumentStore CreateEmbeddedStore() {
        EmbeddedDatabaseManager.Start();
        return EmbeddedDatabaseManager.GetDocumentStore(DatabaseName);
    }
}
