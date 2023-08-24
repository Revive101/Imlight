using System;
using System.Security.Cryptography.X509Certificates;
using Raven.Client.Documents;

namespace Imlight.Server.WizardData;

public abstract class RavenDatabaseSingleton<T>
    where T : RavenDatabaseSingleton<T>
{
    private static readonly Lazy<T> Lazy =
        new(() => (Activator.CreateInstance(typeof(T), true) as T)!);
    public static T Instance => Lazy.Value;

    protected IDocumentStore _store;
    public IDocumentStore Store => _store ??= CreateStore();

    protected abstract IDocumentStore CreateStore();
}