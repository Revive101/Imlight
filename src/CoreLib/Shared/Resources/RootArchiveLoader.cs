/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Formats;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// Loads the Root.wad archive into memory.
/// </summary>
public static class RootArchiveLoader {
    internal const string RootWadName = "Root.wad";
    internal static bool IsLoaded { get; private set; }
    private static KiWad s_rootWad;

    internal static KiWad GetRootWad() => s_rootWad;

    /// <summary>
    /// Reloads the Root.wad file into memory.
    /// </summary>
    public static void ReloadRootWad() {
        Logger.Information("Loading Root.wad into memory..");

        s_rootWad = ResourceWad();
        if (s_rootWad is not null) {
            IsLoaded = true;
        }

        Logger.Information("Root.wad successfully loaded into memory.");
    }

    /// <summary>
    /// Gets a <see cref="MemoryStream"/> for the specified file name.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>A <see cref="MemoryStream"/> containing the file data.</returns>
    internal static MemoryStream GetFileStream(string fileName) {
        if (s_rootWad is null) {
            ReloadRootWad();
        }

        var t = s_rootWad;
        var file = s_rootWad.OpenFile(fileName) ?? throw new Exception($"Could not find file {fileName} in Root.wad!");

        return file;
    }

    /// <summary>
    /// Retrieves a file of type T from the root archive.
    /// </summary>
    /// <typeparam name="T">The type of the file to retrieve.</typeparam>
    /// <param name="fileName">The name of the file to retrieve.</param>
    /// <returns>The file of type T.</returns>
    internal static T GetFile<T>(string fileName) where T : PropertyClass {
        if (s_rootWad is null) {
            ReloadRootWad();
        }

        // Validate that the file exists.
        if (!s_rootWad.Files.TryGetValue(fileName, out var _)) {
            return null;
        }

        var serializer = new FileSerializer();
        return serializer.OpenClass<T>(s_rootWad, fileName);
    }

    /// <summary>
    /// Retrieves a dictionary of file records and memory streams for files within a specified directory.
    /// </summary>
    /// <param name="directoryName">The name of the directory.</param>
    /// <returns>A dictionary containing file records as keys and memory streams as values.</returns>
    internal static Dictionary<FileRecord, MemoryStream> GetDirectoryStream(string directoryName) {
        if (s_rootWad is null) {
            ReloadRootWad();
        }

        var files = new Dictionary<FileRecord, MemoryStream>();

        foreach (var file in s_rootWad.Files.Where(x => x.Key.StartsWith(directoryName) && x.Key != directoryName)) {
            var fileName = file.Key;
            var fileRecord = file.Value;

            var stream = s_rootWad.OpenFile(fileName);
            files.Add(fileRecord, stream);
        }

        return files;
    }

    private static KiWad ResourceWad() {
        // Check if the file is already cached. If it is, just return that.
        var cachedWad = LocalWadCache.GetCachedWad(RootWadName);
        if (cachedWad is not null) {
            return cachedWad;
        }

        // Otherwise, download it from the patch server.
        // If Imlight is running without the patch server, we'll just return null.
        if (!PatchServerFascade.EndpointReached) {
            Logger.Warning($"Imlight tried to load an uncached KIWAD while the patch server was not available.");
            return null;
        }

        if (!PatchServerFascade.DownloadWadFromPatchServer(RootWadName, out var stream)) {
            Logger.Error("Failed to download wad {WadName} from patch server", Logger.Args(RootWadName));
            return null;
        }

        // If we successfully downloaded it, we'll also cache it so we don't have to do that again.
        stream.Seek(0, SeekOrigin.Begin);
        var wad = new KiWad(stream);
        LocalWadCache.CacheWad(RootWadName, wad);

        return wad;
    }
}
