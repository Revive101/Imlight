/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Imlight.Common;
using Imlight.Common.Configuration;
using Raven.Client.Documents;
using Raven.Client.Json.Serialization.NewtonsoftJson;

namespace Imlight.CoreLib.WizardData.Implementations;

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
            : GetCertificate();
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

                // RavenDb Studio cannot properly display ulong values, so we convert them to strings. JavaScript moment.
                Serialization = new NewtonsoftJsonSerializationConventions()
                {
                    CustomizeJsonSerializer = s => s.Converters.Add(new ULongToStringConverter())
                }
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

    private static X509Certificate2 GetCertificate() {
        if (ConfigurationManager.Settings.WorldDatabaseUrl == string.Empty) {
            return null;
        }

        // The certificate path is relative to the working directory.
        // We need to get the absolute path.
        var absolutePath = Path.GetFullPath(ConfigurationManager.Settings.WorldDatabaseCertificatePath);

        // If there is no file at this path, log an error and return null.
        if (!File.Exists(absolutePath)) {
            Logger.Error("No certificate found at path {0}",
                Logger.Args(absolutePath));
            return null;
        }

        return new X509Certificate2(absolutePath);
    }
}
