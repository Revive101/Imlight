/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty;
using Imcodec.Wad;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Resources;

/// <summary>
/// Loads the Root.wad archive into memory.
/// </summary>
internal static class RootArchiveLoader {

    internal const string ROOT_WAD_NAME = "Root.wad";

    internal static bool IsLoaded { get; private set; }
    internal static Archive GetRootWad() => s_rootWad;
    private static readonly Lock s_lock = new();
    private static Archive s_rootWad;

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
        lock (s_lock) {
            if (s_rootWad is null) {
                ReloadRootWad();
            }

            var file = s_rootWad?.OpenFile(fileName) 
                ?? throw new Exception($"Could not find file {fileName} in Root.wad!");

            return new MemoryStream(file.ToArray());
        }
    }

    /// <summary>
    /// Retrieves a file of type T from the root archive.
    /// </summary>
    /// <typeparam name="T">The type of the file to retrieve.</typeparam>
    /// <param name="fileName">The name of the file to retrieve.</param>
    /// <returns>The file of type T.</returns>
    internal static T GetFile<T>(string fileName) where T : PropertyClass {
        lock (s_lock) {
            if (s_rootWad is null) {
                ReloadRootWad();
            }

            // Validate that the file exists.
            if (!s_rootWad.Files.TryGetValue(fileName, out var _)) {
                return null;
            }

            var fileData = s_rootWad.OpenFile(fileName);
            if (fileData is null) {
                return null;
            }

            // Deserialize the file data into the specified type.
            var serializer = new BindSerializer();
            if (!serializer.Deserialize(fileData?.ToArray(), out T file)) {
                return null;
            }

            return file;
        }
    }

    /// <summary>
    /// Retrieves a dictionary of file records and memory streams for files within a specified directory.
    /// </summary>
    /// <param name="directoryName">The name of the directory.</param>
    /// <returns>A dictionary containing file records as keys and memory streams as values.</returns>
    internal static Dictionary<FileEntry, Memory<byte>?> GetDirectoryStream(string directoryName) {
        if (s_rootWad is null) {
            ReloadRootWad();
        }

        var files = new Dictionary<FileEntry, Memory<byte>?>();

        foreach (var file in s_rootWad.Files
            .Where(x => x.Key.StartsWith(directoryName) && x.Key != directoryName)) {
            var fileName = file.Key;
            var fileRecord = file.Value;

            var stream = s_rootWad.OpenFile(fileName);
            files.Add(fileRecord.Value, stream);
        }

        return files;
    }

    private static Archive ResourceWad() {
        // Check if the file is already cached. If it is, just return that.
        var cachedWad = LocalWadCache.GetCachedWad(ROOT_WAD_NAME);
        if (cachedWad is not null) {
            return cachedWad;
        }

        // Otherwise, download it from the patch server.
        // If Imlight is running without the patch server, we'll just return null.
        if (!PatchServerFascade.EndpointReached) {
            throw new Exception("Patch server is not reachable. Cannot load Root.wad.");
        }

        if (!PatchServerFascade.DownloadWadFromPatchServer(ROOT_WAD_NAME, out var stream)) {
            Logger.Error("Failed to download wad {WadName} from patch server", Logger.Args(ROOT_WAD_NAME));

            return null;
        }

        // If we successfully downloaded it, we'll also cache it so we don't have to do that again.
        stream.Seek(0, SeekOrigin.Begin);
        var wad = ArchiveParser.Parse(stream);
        LocalWadCache.CacheWad(ROOT_WAD_NAME, wad);

        return wad;
    }

}
