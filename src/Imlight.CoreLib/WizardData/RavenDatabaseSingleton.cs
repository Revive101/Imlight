/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.WizardData;

public abstract class RavenDatabaseSingleton<T> where T : RavenDatabaseSingleton<T> {

    // Singleton manager: only one instance of the database is allowed to exist.
    // Make it lazy so that it is only created when needed.
    private static readonly Lazy<T> s_lazy = new(() => (Activator.CreateInstance(typeof(T), true) as T)!);
    public static T Instance => s_lazy.Value;

    protected readonly byte MaxNumberOfRequestsPerSession
        = ConfigurationManager.Settings["Database.DatabaseMaxNumberOfRequestsPerSession"].AsByte();
    protected readonly byte RequestTimeoutInSeconds
        = ConfigurationManager.Settings["Database.DatabaseRequestTimeoutInSeconds"].AsByte();
    protected readonly byte WaitForNonStaleResultsTimeoutInSeconds
        = ConfigurationManager.Settings["Database.DatabaseWaitForNonStaleResultsTimeout"].AsByte();

    protected abstract string DatabaseName { get; }
    protected abstract string Url { get; }
    protected abstract string CertificatePath { get; }

    protected virtual X509Certificate2 Certificate =>
        string.IsNullOrEmpty(Url) ? null : GetCertificate();

    // Create the store if it doesn't exist, otherwise return the existing store.
    // If no URL is specified, create an embedded store.
    protected IDocumentStore _store;
    public IDocumentStore Store => _store ??= string.IsNullOrEmpty(Url) ? CreateEmbeddedStore() : CreateStore();
    public bool IsEmbedded { get; protected set; }

    protected virtual IDocumentStore CreateStore() {
        // If this is the first time we're creating the store, we need to create the database.
        Logger.Information("Initializing remote RavenDB database for the first time..");
        Logger.Information("Database name: {0}", Logger.Args(DatabaseName));

        var store = new DocumentStore {
            Urls = [Url],
            Database = DatabaseName,
            Conventions = {
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

    protected virtual IDocumentStore CreateEmbeddedStore() {
        EmbeddedDatabaseManager.Start();
        var store = EmbeddedDatabaseManager.GetDocumentStore(DatabaseName);
        IsEmbedded = true;

        return store;
    }

    protected virtual X509Certificate2 GetCertificate() {
        if (string.IsNullOrEmpty(Url)) {
            return null;
        }

        // The certificate path is relative to the working directory.
        // We need to get the absolute path.
        var absolutePath = Path.GetFullPath(CertificatePath);

        // If there is no file at this path, log an error and return null.
        if (!File.Exists(absolutePath)) {
            Logger.Error("No certificate found at path {0}",
                Logger.Args(absolutePath));

            return null;
        }

        return new X509Certificate2(absolutePath);
    }

    public CollectionStatistics GetDatabaseStatistics()
        => Store.Maintenance.Send(new GetCollectionStatisticsOperation());

}