using System;
using System.IO;
using System.Reflection;
using Akka.Actor;
using WizUnraveler;
using WizUnraveler.Data;
using WizUnraveler.ObjectProperty;
using Imlight.Common.Utilities;
using Imlight.Server.Patch;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Database
{
    public static class ResourceManager
    {
        private const string ROOT_WAD_NAME = "Root.wad";
        private const uint PATCH_SERVER_DOWNLOAD_TIMEOUT = 5;

        private static Wad _rootWad;

        public static bool Initialize()
        {
            // The Root.wad is integral to the server. If we do not have one, or cannot install one
            // the server cannot run properly.
            if (!TryLoadWad(ROOT_WAD_NAME, out _rootWad))
            {
                Log.Logger.Fatal($"Unable to download integral wad! {ROOT_WAD_NAME} was not found in the local cache nor able to download!");
                return false;
            }
            
            // Load submodules.
            var subModuleCoreObjectFactory = LoadSubCoreObjectFactory(_rootWad);
            var subModuleAccessPass = LoadSubAccessPassManager(_rootWad);

            return subModuleCoreObjectFactory && subModuleAccessPass;
        }

        /// <summary>
        /// Gets a WAD file from storage. If it's not found in the local cache, it will instead
        /// download it from the available patch server endpoint.
        /// </summary>
        public static bool TryLoadWad(string wadName, out Wad wad)
        {
            wad = default;

            // The root wad is cached in memory due to it's severe usages.
            if (wadName == ROOT_WAD_NAME && _rootWad is not null)
            {
                wad = _rootWad;
                return true;
            }

            // There is a name inconsistency for wad files.
            var betterWadName = wadName.Replace('/', '-');

            // First, check to see if we can get this file from our local cache.
            if (LocalCache.TryGetCachedFile(betterWadName, out var contentDataRaw))
            {
                // If the cached file is available, we can simply transmute the data into a Wad
                // type and return.
                wad = new Wad(contentDataRaw);
                return true;
            }

            // It's not in the local cache. Instead, download it from the patch server endpoint.
            try 
            {
                var patchServer = PatchServer.Instance;
                var askMsg = new PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_REQUEST();
                askMsg.FileName = betterWadName;
                var timeout = TimeSpan.FromSeconds(PATCH_SERVER_DOWNLOAD_TIMEOUT);
                var data = patchServer.Ask<PATCH_105_PROTCOL.MSG_DOWNLOAD_FILE_TASK>(askMsg, timeout)
                        .Result
                        .DownloadTask
                        .Result;

                // The file is now downloaded. Upload it to the cache.
                var ms = new MemoryStream(data);
                LocalCache.CacheFile(wadName, ms);

                Log.Logger.Information($"Wad file {wadName} was put into the local cache. Content size: {data.Length}");

                wad = new Wad(data);
                return true;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"Could not download wad \"{wadName}\". Exception: {ex.Message}");
                return false;
            }
        }

        public static bool LoadFile<T>(Wad wad, string path, out T obj)
            where T : PropertyClass
        {
            obj = default;

            var serializer = new FileSerializer();
            obj = serializer.OpenClass<T>(wad, path);

            return true;
        } 

        public static bool LoadFileStream(Wad wad, string path, out Stream fileStream)
        {
            fileStream = default;
            
            if (wad is null)
            {
                Log.Logger.Fatal("ResourceManager Root.wad not loaded.");
                return false;
            }

            fileStream = wad.OpenFile(path);
            return true;
        }
        
        public static bool LoadRootFile<T>(string path, out T obj)
            where T : PropertyClass
        {
            obj = default;
            
            if (_rootWad is null)
            {
                Log.Logger.Fatal("ResourceManager Root.wad not loaded.");
                return false;
            }

            try
            {
                var serializer = new FileSerializer();
                obj = serializer.OpenClass<T>(_rootWad, path);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"ResourceManager could not load file in wad [{_rootWad.Name}]: {ex.Message}");
                return false;
            }

            return true;
        } 

        private static bool LoadSubCoreObjectFactory(Wad rootWad)
        {
            Log.Logger.Information("CoreObjectFactory loading [TemplateManifest.xml]..");
            if (!CoreObjectFactory.Load(rootWad))
            {
                Log.Logger.Fatal("CoreObjectFactory could not be loaded.");
                return false;
            }
            Log.Logger.Information("CoreObjectFactory [TemplateManifest.xml] loaded.");

            return true;
        }

        private static bool LoadSubAccessPassManager(Wad rootWad)
        {
            Log.Logger.Information("AccessPassManager loading [AccessPass.xml]..");
            if (!AccessPassManager.Load(rootWad))
            {
                Log.Logger.Fatal("AccessPassManager could not be loaded.");
                return false;
            }
            Log.Logger.Information("AccessPassManager [AccessPass.xml] loaded.");
            
            return true;
        }
    }
}
