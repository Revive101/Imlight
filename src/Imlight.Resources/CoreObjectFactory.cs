using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.Cache;
using WizUnraveler.Data;
using Imlight.Common;
using static WizUnraveler.Cache.TypeCache;
using WizUnraveler;

namespace Imlight.Resources
{
    public static class CoreObjectFactory
    {
        private static Wad _rootWad;
        private static TemplateManifest _templateManifest;

        public static void SetRoot(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"Root.wad not found at [{path}]");

            _rootWad = new Wad(path);

            Log.Logger.Information("CoreObjectFactory [Root.wad] loaded.");

            if (_templateManifest is null)
            {
                Log.Logger.Information("CoreObjectFactory loading [TemplateManifest.xml]..");

                var serializer = new FileSerializer();
                _templateManifest = serializer.OpenClass<TemplateManifest>(_rootWad, "TemplateManifest.xml");

                Log.Logger.Information("CoreObjectFactory [TemplateManifest.xml] loaded.");
            }
        }

        /// <summary>
        /// Initializes a CoreObject by allocating its behaviors from a given Template ID.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="coreObject"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static T InitializeCoreObject<T>(T coreObject, uint id) 
            where T : CoreObject, new()
        {
            // The CoreTemplate contains a list of behavior names.
            var template = GetCoreTemplate(id);
            if (template is null)
            {
                Log.Logger.Error($"Could not initialize CoreObject from TemplateID [{coreObject.m_templateID}]");
                return coreObject;
            }

            // Using this list of behavior names, we can find the actual class of each of them
            // by hashing their names and searching through our BehaviorCache.
            coreObject.m_inactiveBehaviors = new List<BehaviorInstance>();
            foreach (BehaviorTemplate behavior in template.m_behaviors)
            {
                if (behavior is null)
                {
                    Log.Logger.Warning($"CoreObject contained null behavior!");
                    coreObject.m_inactiveBehaviors.Add(null);

                    continue;
                }

                // Hash the name and see if we can dispatch the class from that hash.
                var behaviorHash = Crypto.HashString(behavior.m_behaviorName);
                var behaviorInstance = (BehaviorInstance)BehaviorCache.AllocateBehavior(behaviorHash);

                if (behaviorInstance is null)
                {
                    Log.Logger.Warning($"Could not find behavior instance [{behavior.m_behaviorName}] for CoreObject [{typeof(T)}]");

                    coreObject.m_inactiveBehaviors.Add(null);

                    continue;
                }

                // If we did find the hash, set it's name to be proper and add it to the 
                // CoreObject behaviors.
                behaviorInstance.m_behaviorTemplateNameID = behaviorHash;
                coreObject.m_inactiveBehaviors.Add(behaviorInstance);
            }

            return coreObject;
        }

        public static bool FindBehaviorInstance<T>(CoreObject coreObj, out T behaviorInstance)
            where T : BehaviorInstance
        {
            foreach (var behavior in coreObj.m_inactiveBehaviors)
            {
                if (behavior is T)
                {
                    behaviorInstance = (T)behavior;
                    return true;
                }
            }

            behaviorInstance = default;
            return false;
        }

        private static CoreTemplate GetCoreTemplate(ulong id)
        {
            if (_templateManifest is null || _rootWad is null)
                return null;

            var loc = _templateManifest.GetLocation((uint)(id & 0xFFFFFFFF));
            
            if (loc is null)
            {
                return null;
            }

            var fileName = loc.m_filename;
            var serializer = new FileSerializer();
            var coreTemplate = serializer.OpenClass<CoreTemplate>(_rootWad, $"{fileName}");

            if (coreTemplate is null)
                return null;

            return coreTemplate;
        }
    }
}
