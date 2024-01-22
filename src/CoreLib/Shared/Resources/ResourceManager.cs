/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Formats;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.Shared.Resources;

public static class ResourceManager {
    private const string RootWadName = RootArchiveLoader.RootWadName;

    /// <summary>
    /// Tries to load an archive with the specified name.
    /// </summary>
    /// <param name="wadName">The name of the archive to load.</param>
    /// <param name="wad">When this method returns, contains the loaded KiWad object if the archive was successfully loaded; otherwise, the default value.</param>
    /// <returns><c>true</c> if the archive was successfully loaded; otherwise, <c>false</c>.</returns>
    public static bool TryLoadArchive(string wadName, out KiWad wad) {
        wad = default;

        // The root.wad is highly prevalent, so we cache it in memory.
        if (wadName == RootWadName) {
            throw new InvalidOperationException("Root.wad should not be loaded directly. Use the RootArchiveLoader class instead.");
        }

        // Otherwise, load it as normal.
        var cachedWad = ResourceWad(wadName);
        if (cachedWad is null) {
            return false;
        }

        wad = cachedWad;
        return true;
    }

    /// <summary>
    /// Tries to load a file from a specified WAD archive.
    /// </summary>
    /// <param name="wadName">The name of the WAD archive.</param>
    /// <param name="fileName">The name of the file to load.</param>
    /// <param name="fileStream">When this method returns, contains the file stream if the file was successfully loaded; otherwise, the default value.</param>
    /// <returns><c>true</c> if the file was successfully loaded; otherwise, <c>false</c>.</returns>
    public static bool TryLoadFile(string wadName, string fileName, out MemoryStream fileStream) {
        fileStream = default;

        if (wadName == RootWadName) {
            throw new InvalidOperationException("Root.wad should not be loaded directly. Use the RootArchiveLoader class instead.");
        }

        if (!TryLoadArchive(wadName, out var wad)) {
            return false;
        }

        fileStream = wad.OpenFile(fileName);
        return true;

    }

    /// <summary>
    /// Loads and deserializes a file of type T from a specified WAD archive.
    /// </summary>
    /// <typeparam name="T">The type of the file to be deserialized.</typeparam>
    /// <param name="wadName">The name of the WAD archive.</param>
    /// <param name="fileName">The name of the file to be deserialized.</param>
    /// <returns>The deserialized file of type T, or null if the file could not be loaded or deserialized.</returns>
    public static T LoadDeserializedFile<T>(string wadName, string fileName) where T : PropertyClass {
        if (wadName == RootWadName) {
            throw new InvalidOperationException("Root.wad should not be loaded directly. Use the RootArchiveLoader class instead.");
        }

        if (!TryLoadArchive(wadName, out var wad)) {
            return null;
        }

        var serializer = new FileSerializer();
        return serializer.OpenClass<T>(wad, fileName);
    }

    private static KiWad ResourceWad(string wadName) {
        // Check if the file is already cached. If it is, just return that.
        var cachedWad = LocalWadCache.GetCachedWad(wadName);
        if (cachedWad is not null) {
            return cachedWad;
        }

        // Otherwise, download it from the patch server.
        // If Imlight is running without the patch server, we'll just return null.
        if (!PatchServerFascade.EndpointReached) {
            Logger.Warning($"Imlight tried to load an uncached KIWAD while the patch server was not available.");
            return null;
        }

        if (!PatchServerFascade.DownloadWadFromPatchServer(wadName, out var stream)) {
            Logger.Error("Failed to download wad {WadName} from patch server", Logger.Args(wadName));
            return null;
        }

        // If we successfully downloaded it, we'll also cache it so we don't have to do that again.
        stream.Seek(0, SeekOrigin.Begin);
        var wad = new KiWad(stream);
        LocalWadCache.CacheWad(wadName, wad);

        return wad;
    }
}
