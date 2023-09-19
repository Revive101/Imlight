/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography.X509Certificates;
using Raven.Client.Documents;

namespace Imlight.Server.Shared.WizardData;

public abstract class RavenDatabaseSingleton<T>
    where T : RavenDatabaseSingleton<T>
{
    private static readonly Lazy<T> Lazy =
        new(() => (Activator.CreateInstance(typeof(T), true) as T)!);
    public static T Instance => Lazy.Value;

    protected abstract X509Certificate2 Certificate { get; }
    protected abstract string DatabaseName { get; }
    protected abstract string Url { get; }

    protected IDocumentStore _store;
    public IDocumentStore Store => _store ??= Url == string.Empty ? CreateEmbeddedStore() : CreateStore();
    public bool IsEmbedded { get; protected set; }

    protected abstract IDocumentStore CreateStore();
    protected abstract IDocumentStore CreateEmbeddedStore();
}