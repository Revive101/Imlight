/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography.X509Certificates;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;

namespace Imlight.CoreLib.WizardData;

public abstract class RavenDatabaseSingleton<T> where T : RavenDatabaseSingleton<T> {
    // Singleton manager: only one instance of the database is allowed to exist.
    // Make it lazy so that it is only created when needed.
    private static readonly Lazy<T> s_lazy = new(() => (Activator.CreateInstance(typeof(T), true) as T)!);
    public static T Instance => s_lazy.Value;

    protected abstract X509Certificate2 Certificate { get; }
    protected abstract string DatabaseName { get; }
    protected abstract string Url { get; }

    // Create the store if it doesn't exist, otherwise return the existing store.
    // If no URL is specified, create an embedded store.
    protected IDocumentStore _store;
    public IDocumentStore Store => _store ??= Url == string.Empty ? CreateEmbeddedStore() : CreateStore();
    public bool IsEmbedded { get; protected set; }

    protected abstract IDocumentStore CreateStore();
    protected abstract IDocumentStore CreateEmbeddedStore();

    public CollectionStatistics GetDatabaseStatistics()
        => Store.Maintenance.Send(new GetCollectionStatisticsOperation());
}
