/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Raven.Client.Documents;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.WizardData.Databases;

public class PlayerDatabase : RavenDatabaseSingleton<PlayerDatabase> {
    protected readonly byte MaxNumberOfRequestsPerSession
        = ConfigurationManager.Settings["Database.DatabaseMaxNumberOfRequestsPerSession"].AsByte();
    protected readonly byte RequestTimeoutInSeconds
        = ConfigurationManager.Settings["Database.DatabaseRequestTimeoutInSeconds"].AsByte();
    protected readonly byte WaitForNonStaleResultsTimeoutInSeconds
        = ConfigurationManager.Settings["Database.DatabaseWaitForNonStaleResultsTimeout"].AsByte();

    protected override X509Certificate2 Certificate { get; }
        = ConfigurationManager.Settings["Database.PlayerDatabaseUrl"] == string.Empty
            ? null
            : GetCertificate();
    protected override string DatabaseName { get; } = ConfigurationManager.Settings["Database.PlayerDatabaseName"];
    protected override string Url { get; } = ConfigurationManager.Settings["Database.PlayerDatabaseUrl"];

    protected override IDocumentStore CreateStore() {
        // If this is the first time we're creating the store, we need to create the database.
        Logger.Information("Initializing remote RavenDB database for the first time..");
        Logger.Information("Database name: {0}", Logger.Args(DatabaseName));

        var store = new DocumentStore {
            Urls = [Url],
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
        var store = EmbeddedDatabaseManager.GetDocumentStore(DatabaseName);
        IsEmbedded = true;

        return store;
    }

    private static X509Certificate2 GetCertificate() {
        if (ConfigurationManager.Settings["Database.WorldDatabaseUrl"] == string.Empty) {
            return null;
        }

        // The certificate path is relative to the working directory.
        // We need to get the absolute path.
        var absolutePath = Path.GetFullPath(ConfigurationManager.Settings["Database.PlayerDatabaseCertificatePath"]);

        // If there is no file at this path, log an error and return null.
        if (!File.Exists(absolutePath)) {
            Logger.Error("No certificate found at path {0}",
                Logger.Args(absolutePath));
                
            return null;
        }

        return X509CertificateLoader.LoadCertificateFromFile(absolutePath);
    }
}
