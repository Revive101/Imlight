using Imlight.Common.Formats;
using System;
using System.Collections.Generic;
using System.IO;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// Represents a singleton class that provides access to resources located in the root directory.
/// </summary>
/// <typeparam name="T">The type of the derived class.</typeparam>
public abstract class RootDirectoryResourceSingleton<T> where T : RootDirectoryResourceSingleton<T>, new() {
    private static readonly Lazy<T> s_instance = new(() => new T());

    public static T Instance => s_instance.Value;

    protected abstract string DirectoryName { get; }
    protected Dictionary<FileRecord, MemoryStream> Files { get; private set; }

    protected RootDirectoryResourceSingleton() {
        if (!Load()) {
            throw new Exception($"Failed to load resource directory {DirectoryName}.");
        }

        AfterLoad();
    }

    /// <summary>
    /// Loads the files from the root directory.
    /// </summary>
    /// <returns>True if the files are successfully loaded; otherwise, false.</returns>
    protected virtual bool Load() {
        Files = RootArchiveLoader.GetDirectoryStream(DirectoryName);

        // Try to load this file from the root archive loader.
        return Files.Count > 0;
    }

    /// <summary>
    /// This method is called after the resource is loaded.
    /// </summary>
    protected abstract void AfterLoad();
}
