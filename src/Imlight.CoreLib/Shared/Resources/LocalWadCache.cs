/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Patch;
using Imlight.CoreLib.Shared.Cryptography;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Imlight.CoreLib.Shared.Resources;

internal class FileDefinition {

    public string Filename { get; set; }
    public uint Size { get; set; }
    public uint Crc { get; set; }

}

/// <summary>
/// The LocalWadCache is a local database that stores KIWAD files.
/// It is not recommended to source files from here. Instead, use <see cref="ResourceManager"/>,
/// which will automatically source files from the cache or the patch server, whichever is available.
/// </summary>
public static class LocalWadCache {

    private static readonly string s_path = ConfigurationManager.Settings["Patch Server.LocalWadCachePath"];
    private static bool s_hasInitialized;

    static LocalWadCache() => Initialize();

    public static void Initialize() {
        if (s_hasInitialized) {
            return;
        }
        s_hasInitialized = true;

        // Create the cache file if it doesn't exist.
        if (!File.Exists(s_path)) {
            File.Create(s_path).Dispose();
            Logger.Information("Created local cache file at {Path}", Logger.Args(s_path));
        }

        // If the patch server is available, we'll update the cache.
        if (PatchServer.EndpointReached) {
            Logger.Information("Begin updating local cache..");
            UpdateCache();
            Logger.Information("Local cache updated!");
        }
        else {
            Logger.Information("Local cache will not be updated because the patch server is not available.");
        }
    }

    internal static Archive GetCachedWad(string wadName) {
        wadName = SanitizeWadName(wadName);
        using var db = new LiteDatabase(s_path);
        var fs = db.GetStorage<FileDefinition>();

        var file = fs.Find(f => f.Filename == wadName)
            .FirstOrDefault();

        var stream = file.OpenRead();

        return file is null 
            ? null 
            : ArchiveParser.Parse(stream);
    }

    internal static List<FileDefinition> GetAllCachedFiles() {
        using var db = new LiteDatabase(s_path);
        var fs = db.GetStorage<FileDefinition>();

        var allFiles = fs.FindAll();

        return [.. allFiles.Select(file => file.Id)];
    }

    internal static void CacheWad(string fileName, Archive wad) {
        fileName = SanitizeWadName(fileName);
        using var db = new LiteDatabase(s_path);
        var fs = db.GetStorage<FileDefinition>();

        // Search to see if this file exists. If it does, we'll warn the user
        // but we will overwrite the file regardless.
        var file = fs.Find(f => f.Filename == fileName)
            .FirstOrDefault();
        if (file is not null) {
            Logger.Warning("LocalCache already contains a file definition for {FileName}! " +
                        $"File will be overwritten.", Logger.Args(fileName));
        }

        // Create a new FileDefinition. Create a new byte array from the content stream.
        var wadCrc = GetWadCrc(wad);
        var def = new FileDefinition {
            Filename = fileName,
            Size = wad.Size(),
            Crc = wadCrc,
        };

        var contentStream = new MemoryStream(wad.Package().ToArray(), writable: false);
        fs.Upload(def, fileName, contentStream);
    }

    internal static void DeleteWad(string fileName) {
        fileName = SanitizeWadName(fileName);
        using var db = new LiteDatabase(s_path);
        var fs = db.GetStorage<FileDefinition>();

        var file = fs.Find(f => f.Filename == fileName)
            .FirstOrDefault();

        if (file is not null) {
            fs.Delete(file.Id);

            return;
        }

        Logger.Warning("LocalCache tried to delete a file {FileName} it did not contain!", Logger.Args(fileName));
    }

    private static void UpdateCache() {
        if (!PatchServer.EndpointReached) {
            Logger.Warning($"Tried to update the local cache while the patch server was not available.");
            return;
        }

        // To check for new files, Imlight will always download the LatestFileList from the patch server. It will
        // use the file sizes to determine which cached files are outdated.
        var latestFileList = PatchServerFascade.GetLatestFileList();

        // Iterate through every cached file and check to see if its size matches the latest.
        var cachedFiles = GetAllCachedFiles();
        foreach (var file in cachedFiles) {
            // Imlight's cache removes the '/' character to match zone transfer data. There's also a naming
            // inconsistency. The game client uses a path while Imlight does not.
            var internalFileName = SanitizeWadName(file.Filename);          // Filename for Imlight.
            var documentFileName = $"Data/GameData/{internalFileName}.wad"; // Filename for game client.

            // Search for this file in the LatestFileList.
            var latestFile = latestFileList.Files
                .Find(f => f.SourceFileName == documentFileName);
            if (latestFile is null) {
                Logger.Warning("Cached file {FileName} does not exist in the LatestFileList!",
                    Logger.Args(internalFileName));

                continue;
            }

            // If the file size matches, we don't need to update it.
            if (latestFile.Size == file.Size) {
                Logger.Verbose("Cached file {FileName} did not require update", 
                    Logger.Args(latestFile.SourceFileName));

                continue;
            }

            Logger.Debug("Cached file {FileName} needed update", Logger.Args(file.Filename));
            UpdateCachedFile(file);
        }
    }

    private static void UpdateCachedFile(FileDefinition file) {
        DeleteWad(file.Filename);

        // If the file is Root.wad, we'll also clear the cached version in memory.
        if (file.Filename.Contains("Root")) {
            RootArchiveLoader.ReloadRootWad();
        }

        if (!PatchServerFascade.DownloadWadFromPatchServer(file.Filename, out var stream)) {
            Logger.Error("Failed to update wad {WadName}.", Logger.Args(file.Filename));
            return;
        }

        // If we successfully downloaded it, we'll also cache it so we don't have to do that again.
        stream.Seek(0, SeekOrigin.Begin);
        var wad = ArchiveParser.Parse(stream);
        var betterWadName = SanitizeWadName(file.Filename);
        CacheWad(betterWadName, wad);
    }

    private static string SanitizeWadName(string originalName) {
        var betterWadName = originalName.Replace('/', '-');
        // Remove the `.wad` extension if one exists.
        if (originalName.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)) {
            betterWadName = originalName[..^4];
        }
        // Scope down to the wad name.
        betterWadName = betterWadName.Split('/')[^1];

        return betterWadName;
    }

    private static uint GetWadCrc(Archive wad) {
        // todo: this is not working
        // TEST: Marleybone-MB_Station-MB_Station_Hub.wad has a CRC32 hash of 2084731962.
        //                                           The header CRC32 hash is 3169958109.
        /*
        var data = wad.GetData();
        var wadHeaderSize = (int) wad.HeaderSize;
        var segmentSize = data.Length - wadHeaderSize;

        var wadMeatSegment = new byte[segmentSize];
        Buffer.BlockCopy(data, wadHeaderSize, wadMeatSegment, 0, segmentSize);
        var crc = Crc32.GetHash(uint.MaxValue, wadMeatSegment) ^ uint.MaxValue;
        return crc;
        */
        return 0;
    }

}
