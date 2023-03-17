using System;
using System.Collections.Generic;
using System.IO;
using Imlight.Common;
using WizUnraveler;
using WizUnraveler.Data;
using WizUnraveler.ObjectProperty;

namespace Imlight.Resources
{
    public static class ResourceManager
    {
        private static Wad _rootWad;
        
        /// <summary>
        /// Loads all of the needed resources from a Root.wad file.
        /// </summary>
        /// <param name="path">The absolute path of the Root.wad</param>
        /// <returns>True, on successful load; otherwise, false.</returns>
        public static bool Load(string path)
        {
            if (!File.Exists(path))
            {
                Log.Logger.Fatal("ResourceManager Root.wad not found.");
                return false;
            }

            _rootWad = new Wad(path);

            Log.Logger.Information("ResourceManager Root.wad loaded.");
            
            // Load submodules.
            var subModuleCoreObjectFactory = LoadSubCoreObjectFactory();
            var subModuleAccessPass = LoadSubAccessPassManager();

            return subModuleCoreObjectFactory && subModuleAccessPass;
        }
        
        public static bool LoadFile<T>(string path, out T obj)
            where T : PropertyClass
        {
            obj = default;
            
            if (_rootWad is null)
            {
                Log.Logger.Fatal("ResourceManager Root.wad not loaded.");
                return false;
            }

            var serializer = new FileSerializer();
            obj = serializer.OpenClass<T>(_rootWad, path);

            return true;
        }

        public static bool LoadFileStream(string path, out Stream fileStream)
        {
            fileStream = default;
            
            if (_rootWad is null)
            {
                Log.Logger.Fatal("ResourceManager Root.wad not loaded.");
                return false;
            }

            fileStream = _rootWad.OpenFile(path);
            return true;
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