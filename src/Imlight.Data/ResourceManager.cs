using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Imlight.Common;
using Imlight.Data;
using WizUnraveler;
using WizUnraveler.Data;
using WizUnraveler.ObjectProperty;

namespace Imlight.Data
{
    public static class ResourceManager
    {
        private static Wad _rootWad;
        private static readonly string GameDataPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) 
            ?? string.Empty, $@"gamedata");

        public static bool Initialize()
        {
            // The Root.wad is integral to the server. If we do not have one, or cannot install one
            // the server cannot run properly.
            if (!Directory.Exists(GameDataPath))
            {
                Log.Logger.Fatal($"ResourceManager GameData directory not found: {GameDataPath}");
                return false;
            }
            
            // Load the Root.wad file.
            var rootWadPath = Path.Combine(GameDataPath, "Root.wad");
            _rootWad = new Wad(rootWadPath);
            if (_rootWad is null)
            {
                Log.Logger.Fatal($"ResourceManager Root.wad not found: {rootWadPath}");
                return false;
            }
            
            // Load submodules.
            var subModuleCoreObjectFactory = LoadSubCoreObjectFactory(_rootWad);
            var subModuleAccessPass = LoadSubAccessPassManager(_rootWad);

            return subModuleCoreObjectFactory && subModuleAccessPass;
        }
        
        public static bool LoadWad(string wadName, out Wad wad)
        {
            wad = default;

            var redoWadName = wadName.Replace('/', '-');
            var wadPath = Path.Combine(GameDataPath, redoWadName);
            wad = new Wad($"{wadPath}.wad");

            return wad != null;
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

        private static bool GetRootWad()
        {
            byte[] file;
            try 
            {
                file = LocalCache.GetCachedFile("Root.wad");
            }
            catch (Exception ex)
            {
                Log.Logger.Warning("Root.wad not found in local cache. Downloading..");
            }

            return false;
        }
    }
}
