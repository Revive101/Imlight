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
    public const string RootWadName = "Root.wad";
    private const uint PatchServerDownloadTimeoutSeconds = 360;
    private static KiWad s_rootWad;

    /// <summary>
    /// Gets a WAD file from storage. If it's not found in the local cache, it will instead
    /// download it from the available patch server endpoint.
    /// </summary>
    public static bool TryLoadFile(string wadName, out KiWad wad) {
        wad = default;
        if (wadName == RootWadName) {
            // Root is always loaded into memory. If it's not, we'll load it.
            if (s_rootWad is null) {
                var rootCache = LoadWad(RootWadName);
                if (rootCache is null) {
                    Logger.Error("Could not load vital {WadName} into memory!", Logger.Args(RootWadName));
                    return false;
                }
                s_rootWad = rootCache;
            }

            wad = s_rootWad;
        }
        else {
            var cachedWad = LoadWad(wadName);
            if (cachedWad is null) {
                return false;
            }

            wad = cachedWad;
        }

        return true;
    }

    /// <summary>
    /// Gets a file record from a KIWAD in file storage. If it's not found in the local cache, it will instead
    /// be downloaded from the patch server endpoint.
    /// </summary>
    /// <param name="wadName">The name of the KIWAD.</param>
    /// <param name="fileName">The name of the file record inside the KIWAD.</param>
    /// <param name="fileStream">The output file stream that will return if the file record is found.</param>
    /// <returns>True, if the file was found or downloaded; otherwise, false.</returns>
    public static bool TryLoadFile(string wadName, string fileName, out MemoryStream fileStream) {
        fileStream = default;

        if (!TryLoadFile(wadName, out var wad)) {
            return false;
        }

        fileStream = wad.OpenFile(fileName);
        return true;

    }

    /// <summary>
    /// Loads a file from the cache, or downloads it from the patch server as needed, and deserializes the file.
    /// Any file from root is safe, as root is always loaded into memory.
    /// Otherwise, load the entire KIWAD using <see cref="TryLoadFile(string,out Wad)"/>
    /// and use <see cref="FileSerializer"/> to open individual files from it.
    /// </summary>
    /// <param name="wadName">The name of the wad.</param>
    /// <param name="fileName">The name of the file record.</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The deserialized property class. Null if it was not found, could not be downloaded,
    /// or could not be deserialized.</returns>
    public static T LoadDeserializedFile<T>(string wadName, string fileName) where T : PropertyClass {
        if (!TryLoadFile(wadName, out var wad)) {
            return null;
        }

        var serializer = new FileSerializer();
        return serializer.OpenClass<T>(wad, fileName);
    }

    /// <summary>
    /// Loads a wad from the cache, or downloads it from the patch server as needed.
    /// </summary>
    /// <param name="wadName"></param>
    /// <returns></returns>
    private static KiWad LoadWad(string wadName) {
        // Check if the file is already cached. If it is, just return that.
        var cachedWad = LocalWadCache.GetCachedWad(wadName);
        if (cachedWad is not null) {
            return cachedWad;
        }

        // Otherwise, download it from the patch server.
        // If Imlight is running without the patch server, we'll just return null.
        if (!PatchServer.EndpointReached) {
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
