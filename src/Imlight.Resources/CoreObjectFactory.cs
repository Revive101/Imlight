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
        private const string TEMPLATE_MANIFEST_NAME = "TemplateManifest.xml";
        
        private static TemplateManifest _templateManifest;
        private static readonly Dictionary<ulong, ByteString> _coreTemplates = new();

        public static bool Load(Wad rootWad)
        {
            var loadResult = ResourceManager.LoadFile(rootWad, TEMPLATE_MANIFEST_NAME, out _templateManifest);

            if (loadResult)
            {
                foreach (var templateLocation in _templateManifest.m_serializedTemplates)
                {
                    var id = templateLocation.m_id;
                    var loc = templateLocation.m_filename;

                    // Drop the MSB from the id.
                    id &= 0xFFFFFFFF;
                    _coreTemplates.Add(id, loc);
                }
            }

            return loadResult;
        }

        /// <summary>
        /// Initializes a CoreObject by allocating its behaviors from a given Template ID.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="coreObject"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static T InitializeCoreObjectBehaviors<T>(T coreObject, ulong id) 
            where T : CoreObject, new()
        {
            var template = GetCoreTemplate(id);
            if (template is null)
            {
                Log.Logger.Error($"Could not initialize CoreObject from TemplateID [{coreObject.m_templateID}]");
                return coreObject;
            }

            // The CoreTemplate contains a list of behavior templates. Using the name of the template,
            // we can find the instance of the behavior and add it to the CoreObject.
            coreObject.m_inactiveBehaviors = new List<BehaviorInstance>(template.m_behaviors.Count);
            foreach (var behaviorTemplate in template.m_behaviors)
            {
                if (behaviorTemplate is null)
                {
                    coreObject.m_inactiveBehaviors.Add(null);
                    continue;
                }

                // Hash the name and see if we can dispatch the behavior instance from that hash.
                var behaviorHash = Crypto.HashString(behaviorTemplate.m_behaviorName);
                var behaviorInstance = BehaviorCache.AllocateBehavior(behaviorHash);

                if (behaviorInstance is null)
                {
                    //Log.Logger.Warning($"Could not find behavior instance [{behavior.m_behaviorName}] for CoreObject [{typeof(T)}]");
                    coreObject.m_inactiveBehaviors.Add(null);
                    continue;
                }

                // If we did find the instance, set it's name to be proper and add it to the CoreObject behaviors.
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

        public static CoreObject CreateObjectFromInfo(CoreObjectInfo objInfo)
        {
            if (objInfo.m_templateID == 0)
            {
                Log.Logger.Error($"Object Info template ID was 0.");
                return null;
            };
            
            var template = GetCoreTemplate(objInfo.m_templateID);
            if (template is null)
            {
                Log.Logger.Error($"Could not find template for object info [{objInfo.m_templateID}]");
                return null;
            }
            
            // Generate a GUID ahead of time.
            var guid = RandomGen.GenerateGUID();
            CoreObject obj;
            switch (template)
            {
                case ReagentItemTemplate:
                    obj = new ClientReagentItem();
                    break;
                case ItemTemplate:
                    obj = new WizClientObjectItem();
                    break;
                case WizGameObjectTemplate:
                    obj = new WizClientObject();
                    // Below is probably not needed.
                    ((WizClientObject)obj).m_characterId = guid;
                    ((WizClientObject)obj).m_gameStats = new WizGameStats();
                    break;
                default:
                    obj = new ClientObject();
                    break;
            }

            // Initialize the object behaviors.
            //obj = InitializeCoreObjectBehaviors(obj, objInfo.m_templateID);
            
            // Set the object properties.
            obj.m_templateID = objInfo.m_templateID;
            obj.m_location = objInfo.m_location;
            obj.m_orientation = objInfo.m_orientation;
            obj.m_fScale = objInfo.m_fScale;
            obj.m_globalID = guid;
            obj.m_permID = RandomGen.GenerateHash($"{obj.m_zoneTagID}{obj.m_templateID}{obj.m_location.X}");
            obj.m_zoneTagID = Crypto.HashString(objInfo.m_zoneTag);
            obj.m_debugName = objInfo.m_zoneTag;

            return obj;
        }
        
        private static CoreTemplate GetCoreTemplate(ulong id)
        {
            if (!_coreTemplates.TryGetValue(id, out var loc)) return null;
            
            ResourceManager.LoadRootFile(loc, out CoreTemplate template);
            return template ?? null;
        }
    }
}
