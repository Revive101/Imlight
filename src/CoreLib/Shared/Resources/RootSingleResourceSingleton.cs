/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// Represents a base class for implementing a singleton pattern for a resource that is loaded from a root archive.
/// </summary>
/// <typeparam name="T">The type of the derived class.</typeparam>
public abstract class RootSingleResourceSingleton<T> where T : RootSingleResourceSingleton<T>, new() {
    private static readonly Lazy<T> s_instance = new(() => new T());

    public static T Instance => s_instance.Value;

    protected abstract string ResourceName { get; }
    protected MemoryStream Stream { get; private set; }

    protected RootSingleResourceSingleton() {
        if (!Load()) {
            throw new Exception($"Failed to load resource {ResourceName}.");
        }

        AfterLoad();
    }

    /// <summary>
    /// Loads the file from the root archive loader.
    /// </summary>
    /// <returns>True if the file is successfully loaded, otherwise false.</returns>
    protected virtual bool Load() {
        // Try to load this file from the root archive loader.
        Stream = RootArchiveLoader.GetFileStream(ResourceName);
        return Stream != null;
    }

    /// <summary>
    /// This method is called after the resource is loaded.
    /// </summary>
    protected abstract void AfterLoad();
}
