using System;
using System.IO;
using System.Reflection;
using Akka.Actor;
using WizUnraveler;
using WizUnraveler.ObjectProperty;
using Imlight.Common.Utilities;
using Imlight.Server.Patch;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Cache;
using WizUnraveler.Formats;
using WizUnraveler.IO;

namespace Imlight.Server.Database
{
    public static class ResourceManager
    {
        const uint PATCH_SERVER_DOWNLOAD_TIMEOUT_SECONDS = 360;
        private const string ROOT_WAD_NAME = "Root.wad";
        private static Wad _rootWad;

        /// <summary>   
        /// Initializes the ResourceManager. It will instantiate
        /// <see cref="CoreObjectFactory"/> and
        /// <see cref="AccessPassManager"/>
        /// alongside itself. These are considered the most vital resouces needed
        /// for the server to run.
        /// </summary>
        /// <returns></returns>
        public static bool Initialize()
        {
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

            // The Root.wad is accessed a LOT, so it's cached in memory.
            if (wadName == ROOT_WAD_NAME)
            {
                if (_rootWad is null)
                {
                    var rootCache = LoadWadFromCacheOrDownload(ROOT_WAD_NAME);
                    if (rootCache is null)
                    {
                        Log.Logger.Error($"Could not load vital {ROOT_WAD_NAME} into memory!");
                        return false;
                    }
                    _rootWad = rootCache;
                }

                wad = _rootWad;
                return true;
            }

            var betterWadName = wadName.Replace('/', '-');
            var cachedWad = LoadWadFromCacheOrDownload(betterWadName);
            if (cachedWad is null)
                return false;

            wad = cachedWad;
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
        /// Loads a file from the cache, or downloads it from the patch server as needed. Deserializes the file.
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

        private static Wad LoadWadFromCacheOrDownload(string wadName)
        {
            var cachedWad = LocalCache.GetCachedWad(wadName);
            if (cachedWad is not null)
                return cachedWad;

            if (!DownloadFromPatchServer(wadName, out var stream))
                return null;

            LocalCache.CacheWad(wadName, stream);
            return new Wad(stream);
        }

        private static bool DownloadFromPatchServer(string wadName, out Stream fileStream)
        {
            fileStream = default;
            try 
            {
                var patchServer = PatchServer.Instance;
                var askMsg = new PATCH_105_PROTCOL.MSG_DOWNLOAD_WAD_REQUEST
                {
                    WadName = wadName
                };
                var timeout = TimeSpan.FromSeconds(PATCH_SERVER_DOWNLOAD_TIMEOUT_SECONDS);
                fileStream = patchServer.Ask<PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_RESULT>(askMsg, timeout)
                    .Result
                    .FileStream;

                return true;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Could not download wad \"{wadName}\". Exception: {ex.Message}");
                return false;
            }
        }

        private static bool LoadSubCoreObjectFactory()
        {
            Log.Logger.Information("CoreObjectFactory loading [TemplateManifest.xml]..");
            if (!CoreObjectFactory.Load())
            {
                Log.Logger.Fatal("CoreObjectFactory could not be loaded.");
                return false;
            }
            Log.Logger.Information("CoreObjectFactory [TemplateManifest.xml] loaded.");

            return true;
        }

        private static bool LoadSubAccessPassManager()
        {
            Log.Logger.Information("AccessPassManager loading [AccessPass.xml]..");
            if (!AccessPassManager.Load())
            {
                Log.Logger.Fatal("AccessPassManager could not be loaded.");
                return false;
            }
            Log.Logger.Information("AccessPassManager [AccessPass.xml] loaded.");
            
            return true;
        }
    }
}
