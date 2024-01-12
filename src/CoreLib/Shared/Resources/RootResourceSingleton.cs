using System;
using System.IO;

namespace Imlight.CoreLib.Shared.Resources;

public abstract class RootResourceSingleton<T> where T : RootResourceSingleton<T>, new() {
    private static readonly Lazy<T> s_instance = new(() => new T());

    public static T Instance => s_instance.Value;

    protected abstract string ResourceName { get; }
    protected MemoryStream Stream { get; private set; }

    protected RootResourceSingleton() {
        if (!Load()) {
            throw new Exception($"Failed to load resource {ResourceName}.");
        }

        AfterLoad();
    }

    protected virtual bool Load() {
        // Try to load this file from the root archive loader.
        Stream = RootArchiveLoader.GetFileStream(ResourceName);
        return Stream != null;
    }

    protected abstract void AfterLoad();
}
