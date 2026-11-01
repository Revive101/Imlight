/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * LOCAL WAD CACHE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages local caching and retrieval of game resource files (WAD files) 
 * using a LiteDB database, supporting file versioning and patch management.
 * 
 * USAGE EXAMPLE:
 * // Get a cached WAD file
 * Archive cachedWad = LocalWadCache.GetCachedWad("SomeResourceFile");
 * 
 * // Cache a new WAD file
 * LocalWadCache.CacheWad(fileName, wadArchive);
 * 
 * NOTE:
 * 
 * TODO:
 * - Implement functional CRC32 hash calculation
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;
using Imcodec.Wad;
using Imlight.Common;
using Imlight.CoreLib.Patch;

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
internal static class LocalWadCache {

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
            Logger.Debug("Begin updating local cache..");
            UpdateCache();
        }
        else {
            Logger.Debug("Local cache will not be updated because the patch server is not available.");
        }
    }

    internal static Archive GetCachedWad(string wadName) {
        wadName = SanitizeWadName(wadName);
        using var db = new LiteDatabase(s_path);
        var fs = db.GetStorage<FileDefinition>();

        var file = fs.Find(f => f.Filename == wadName)
            .FirstOrDefault();

        if (file is null) {
            Logger.Debug("Game file {FileName} is not in the local cache yet.", Logger.Args(wadName));

            return null;
        }

        // The LiteDB stream is file IO and very slow.
        // Instead, copy the stream to a memory stream.
        var liteDbStream = fs.OpenRead(file.Id);
        var fileStream = new MemoryStream();
        liteDbStream.CopyTo(fileStream);
        fileStream.Seek(0, SeekOrigin.Begin);

        return file is null 
            ? null 
            : ArchiveParser.Parse(fileStream);
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
            Logger.Debug("Game file {FileName} is already in the local cache and is overwritten.",
                Logger.Args(fileName));
        }

        // Create a new FileDefinition. Create a new byte array from the content stream.
        var wadCrc = GetWadCrc(wad);
        var def = new FileDefinition {
            Filename = fileName,
            Size = wad.Size(),
            Crc = wadCrc,
        };

        var contentStream = wad.GetData();
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

        Logger.Debug("Game file {FileName} was not in the local cache, so there was nothing to delete.",
            Logger.Args(fileName));
    }

    private static void UpdateCache() {
        if (!PatchServer.EndpointReached) {
            Logger.Warning("Tried to update the local cache while the patch server was not available.");

            return;
        }

        // To check for new files, Imlight will always download the LatestFileList from the patch server. It will
        // use the file sizes to determine which cached files are outdated.
        var latestFileList = PatchServerFascade.GetLatestFileList();
        if (latestFileList?.Files is null) {
            Logger.Warning("The local cache was not checked for updates because the patch server sent no game file list.");

            return;
        }

        // Iterate through every cached file and check to see if its size matches the latest.
        var cachedFiles = GetAllCachedFiles();
        var updatedFiles = 0;
        var failedFiles = new List<string>();
        foreach (var file in cachedFiles) {
            // Imlight's cache removes the '/' character to match zone transfer data. There's also a naming
            // inconsistency. The game client uses a path while Imlight does not.
            var internalFileName = SanitizeWadName(file.Filename);          // Filename for Imlight.
            var documentFileName = $"Data/GameData/{internalFileName}.wad"; // Filename for game client.

            // Search for this file in the LatestFileList.
            var latestFile = latestFileList.Files
                .Find(f => f.SourceFileName == documentFileName);
            if (latestFile is null) {
                Logger.Debug("Cached file {FileName} is not in the patch server's game file list.",
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
            if (TryUpdateCachedFile(file)) {
                updatedFiles++;
            }
            else {
                failedFiles.Add(file.Filename);
            }
        }

        Logger.Information("Local cache checked: {Updated} of {Cached} game files updated.",
            Logger.Args(updatedFiles, cachedFiles.Count));
        if (failedFiles.Count > 0) {
            Logger.Warning("{Failed} game files could not be updated and keep their old copy: {Files}",
                Logger.Args(failedFiles.Count, string.Join(", ", failedFiles)));
        }
    }

    private static bool TryUpdateCachedFile(FileDefinition file) {
        if (!PatchServerFascade.DownloadWadFromPatchServer(file.Filename, out var stream)) {
            return false;
        }

        // The old copy stays in the cache until the new one is downloaded.
        stream.Seek(0, SeekOrigin.Begin);
        var wad = ArchiveParser.Parse(stream);
        DeleteWad(file.Filename);
        CacheWad(SanitizeWadName(file.Filename), wad);

        return true;
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
