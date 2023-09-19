/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using Akka.Actor;
using Imlight.Common.Formats;
using Imlight.Common.Serializable;
using Imlight.Common.Serializable.ObjectProperty;
using Imlight.Common.Utilities;
using Imlight.Server.Patch;
using Imlight.Server.Shared.Packets;
using Imlight.Server.Shared.WizardData.Implementations;

namespace Imlight.Server.Shared.Resources;

public static class ResourceManager
{
    private const uint PatchServerDownloadTimeoutSeconds = 360;
    private const string RootWadName = "Root.wad";
    private static Wad _rootWad;

    /// <summary>   
    /// Initializes the ResourceManager. It will instantiate
    /// <see cref="CoreObjectFactory"/> and
    /// <see cref="AccessPassManager"/>
    /// alongside itself. These are considered the most vital resources needed
    /// for the server to run.
    /// </summary>
    /// <returns></returns>
    public static bool Initialize()
    {
        Log.Information("Updating local cache..");
        if (PatchServer.EndpointReached)
        {
            UpdateCache();
            Log.Information("Local cache updated!");
        }
        else
        {
            Log.Information("The patch server endpoint could not be reached. " +
                            "The local cache will not be updated.");
        }

        // Load submodules.
        var subModuleCoreObjectFactory = LoadSubCoreObjectFactory();
        var subModuleAccessPass = LoadSubAccessPassManager();
        return subModuleCoreObjectFactory && subModuleAccessPass;
    }

    /// <summary>
    /// Gets a WAD file from storage. If it's not found in the local cache, it will instead
    /// download it from the available patch server endpoint.
    /// </summary>
    public static bool TryLoadFile(string wadName, out Wad wad)
    {
        wad = default;
        if (wadName == RootWadName)
        {
            // Root is always loaded into memory. If it's not, we'll load it. 
            if (_rootWad is null)
            {
                var rootCache = LoadWad(RootWadName);
                if (rootCache is null)
                {
                    Log.Error("Could not load vital {WadName} into memory!", Log.Args(RootWadName));
                    return false;
                }
                _rootWad = rootCache;
            }

            wad = _rootWad;
        }
        else
        {
            var cachedWad = LoadWad(wadName);
            if (cachedWad is null)
                return false;

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
    public static bool TryLoadFile(string wadName, string fileName, out MemoryStream fileStream)
    {
        fileStream = default;

        if (!TryLoadFile(wadName, out var wad)) 
            return false;
            
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
    public static T LoadDeserializedFile<T>(string wadName, string fileName)
        where T : PropertyClass
    {
        if (!TryLoadFile(wadName, out var wad))
            return null;
        var serializer = new FileSerializer();
        return serializer.OpenClass<T>(wad, fileName);
    }

    /// <summary>
    /// Loads a wad from the cache, or downloads it from the patch server as needed.
    /// </summary>
    /// <param name="wadName"></param>
    /// <returns></returns>
    private static Wad LoadWad(string wadName)
    {
        var betterWadName = SanitizeWadName(wadName);
            
        // Check if the file is already cached. If it is, just return that.
        var cachedWad = KiWadCache.GetCachedWad(betterWadName);
        if (cachedWad is not null)
            return cachedWad;

        // Otherwise, download it from the patch server.
        // If Imlight is running without the patch server, we'll just return null.
        if (!PatchServer.EndpointReached)
        {
            Log.Warning($"Imlight tried to load an uncached KIWAD while the patch server was not available.");
            return null;
        }
            
        if (!DownloadWadFromPatchServer(betterWadName, out var stream))
        {
            Log.Error("Failed to download wad {WadName} from patch server", Log.Args(wadName));
            return null;
        }

        // If we successfully downloaded it, we'll also cache it so we don't have to do that again.
        stream.Seek(0, SeekOrigin.Begin);
        var wad = new Wad(stream);
        KiWadCache.CacheWad(betterWadName, wad);
            
        return wad;
    }

    /// <summary>
    /// Downloads a wad from the patch server.
    /// </summary>
    /// <param name="wadName"></param>
    /// <param name="fileStream"></param>
    /// <returns></returns>
    private static bool DownloadWadFromPatchServer(string wadName, out MemoryStream fileStream)
    {
        fileStream = default;
        try 
        {
            var patchServer = PatchServer.Instance;
            var askMsg = new PATCH_105_PROTCOL.MSG_DOWNLOAD_WAD_REQUEST { WadName = wadName };
            var timeout = TimeSpan.FromSeconds(PatchServerDownloadTimeoutSeconds);
            fileStream = patchServer.Ask<PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_RESULT>(askMsg, timeout)
                .Result
                .FileStream;

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not download wad {WadName}. Exception: {Ex}", 
                Log.Args(wadName, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Loads the <see cref="CoreObjectFactory"/> submodule.
    /// </summary>
    /// <returns></returns>
    private static bool LoadSubCoreObjectFactory()
    {
        Log.Information("Start load of {Cof}", Log.Args(nameof(CoreObjectFactory)));
        if (!CoreObjectFactory.Load())
        {
            Log.Fatal("CoreObjectFactory could not be loaded.");
            return false;
        }

        Log.Information("Complete load of {Cof}", Log.Args(nameof(CoreObjectFactory)));

        return true;
    }

    /// <summary>
    /// Loads the <see cref="AccessPassManager"/> submodule.
    /// </summary>
    /// <returns></returns>
    private static bool LoadSubAccessPassManager()
    {
        Log.Information("Start load of {Apm}", Log.Args(nameof(AccessPassManager)));
        if (!AccessPassManager.Load())
        {
            Log.Fatal("AccessPassManager could not be loaded.");
            return false;
        }
        Log.Information("Complete load of {Apm}", Log.Args(nameof(AccessPassManager)));
            
        return true;
    }

    /// <summary>
    /// Sanitizes a wad name to match the patch server's naming convention.
    /// </summary>
    /// <param name="originalName"></param>
    /// <returns></returns>
    private static string SanitizeWadName(string originalName)
    {
        var betterWadName = originalName.Replace('/', '-');
        // Remove the `.wad` extension if one exists.
        if (originalName.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
            betterWadName = originalName[..^4];
        // Scope down to the wad name.
        betterWadName = betterWadName.Split('/')[^1];

        return betterWadName;
    }
        
    /// <summary>
    /// Updates the local cache by downloading any new files from the patch server.
    /// </summary>
    private static void UpdateCache()
    {
        // To check for new files, Imlight will always download the LatestFileList from the patch server. It will
        // use the file sizes to determine which cached files are outdated. 
        var latestFileList = GetLatestFileList();
            
        // Iterate through every cached file and check to see if its size matches the latest.
        var cachedFiles = KiWadCache.GetAllCachedFiles();
        foreach (var file in cachedFiles)
        {
            // Imlight's cache removes the '/' character to match zone transfer data. There's also a naming
            // inconsistency. Wizard101 uses a path while Imlight does not.
            var internalFileName = SanitizeWadName(file.Filename);     // Filename for Imlight.
            var documentFileName = $"Data/GameData/{internalFileName}.wad"; // Filename for Wizard101.
                
            // Search for this file in the LatestFileList.
            var latestFile = latestFileList.Files
                .Find(f => f.SourceFileName == documentFileName);
            if (latestFile is null)
            {
                Log.Warning("Cached file {FileName} does not exist in the LatestFileList!", 
                    Log.Args(internalFileName));
                continue;
            }
                
            // If the file size matches, we don't need to update it.
            if (latestFile.Size == file.Size)
            {
                Log.Debug("Cached file {FileName} did not require update", Log.Args(latestFile.SourceFileName));
                continue;
            }
                
            Log.Debug("Cached file {FileName} needed update", Log.Args(file.Filename));
            UpdateCachedFile(file);
        }
    }

    /// <summary>
    /// Gets the latest file list from the patch server.
    /// </summary>
    /// <returns></returns>
    private static LatestFileList GetLatestFileList()
    {
        var msg = new PATCH_105_PROTCOL.MSG_LATESTFILELIST();
        var rsp = PatchServer.Instance.Ask<PATCH_105_PROTCOL.MSG_LATESTFILELIST>(msg).Result;
        var latestFileList = rsp.LatestFileList;

        return latestFileList;
    }

    /// <summary>
    /// Updates a cached file by deleting it and downloading it again.
    /// </summary>
    /// <param name="file"></param>
    private static void UpdateCachedFile(FileDefinition file)
    {
        KiWadCache.DeleteWad(file.Filename);
            
        // If the file is Root.wad, we'll also clear the cached version in memory.
        if (file.Filename.Contains("Root"))
            _rootWad = null;
            
        if (TryLoadFile(file.Filename, out _))
        {
            Log.Debug("Cached file {FileName} was updated", Log.Args(file.Filename));
            return;
        }
                
        Log.Error("Could not upsert cached file {FileName}", Log.Args(file.Filename));
    }
}