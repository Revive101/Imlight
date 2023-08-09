/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Imlight.Common.Cryptography;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Secrets;
using WizUnraveler.Common;
using WizUnraveler.IO;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Shared.Resources
{
    public static class CoreObjectFactory
    {
        private const string ROOT_WAD_NAME = "Root.wad";
        private const string TEMPLATE_MANIFEST_NAME = "TemplateManifest.xml";
        
        private static readonly Dictionary<ulong, ByteString> _coreTemplates = new();

        public static bool Load()
        {
            // Load the TemplateManifest.xml and record the amount of time it takes.
            var timer = new Stopwatch();
            timer.Start();
            var manifest = ResourceManager.LoadDeserializedFile<TemplateManifest>(ROOT_WAD_NAME, TEMPLATE_MANIFEST_NAME);
            if (manifest is null)
                return false;
            timer.Stop();
            Log.Logger.Information("TemplateManifest deserialize took {Em}ms.", timer.ElapsedMilliseconds);
            
            foreach (var templateLocation in manifest.m_serializedTemplates)
            {
                if (templateLocation is null) continue;

                var id = templateLocation.m_id;
                var loc = templateLocation.m_filename;

                // Drop the MSB from the id.
                id &= 0xFFFFFFFF;
                _coreTemplates.Add(id, loc);
            }

            return true;
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
                Log.Logger.Error("Could not initialize CoreObject from TemplateID {Tid}", 
                    coreObject.m_templateID);
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
                var behaviorHash = StringHash.Compute(behaviorTemplate.m_behaviorName);
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
            foreach (var behavior in coreObj.m_inactiveBehaviors.OfType<T>())
            {
                behaviorInstance = behavior;
                return true;
            }

            behaviorInstance = default;
            return false;
        }

        public static CoreObject CreateObjectFromTemplate(CoreObjectInfo objInfo, CoreTemplate template, ulong templateId)
        {
            var obj = CreateCoreObjectFromTemplate(template);
            obj.m_templateID = templateId;
            SetCoreObjectStatsFromInfo(obj, ref objInfo);

            return obj;
        }

        public static CoreObject CreateObjectFromInfo(CoreObjectInfo objInfo, ulong templateId = 0)
        {
            // If the template ID is 0, use the one from the object info.
            if (templateId == 0)
                templateId = objInfo.m_templateID;
            
            var template = GetCoreTemplate(templateId);

            var obj = CreateObjectFromTemplate(objInfo, template, templateId);
            return obj;
        }

        public static CoreTemplate GetCoreTemplate(ulong id)
        {
            if (!_coreTemplates.TryGetValue(id, out var loc)) return null;

            var template = ResourceManager.LoadDeserializedFile<CoreTemplate>("Root.wad", loc);
            if (template is null)
                throw new NullReferenceException($"Template by ID {id} was not found!");

            return template ?? null;
        }

        private static CoreObject CreateCoreObjectFromTemplate(CoreTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            return template switch
            {
                ReagentItemTemplate => new ClientReagentItem(),
                ItemTemplate => new WizClientObjectItem(),
                WizGameObjectTemplate => new WizClientObject(),
                _ => new ClientObject()
            };
        }

        private static void SetCoreObjectStatsFromInfo(CoreObject obj, ref CoreObjectInfo info)
        {
            // Generate a GUID ahead of time.
            var guid = RandomGen.GenerateGUID();

            // Set the object properties.
            obj.m_location = info.m_location;
            obj.m_orientation = info.m_orientation;
            obj.m_fScale = info.m_fScale;
            obj.m_globalID = guid;
            obj.m_permID = RandomGen.GenerateHash($"{obj.m_zoneTagID}{obj.m_templateID}{obj.m_location.X}");
            obj.m_zoneTagID = Crypto.HashString(info.m_zoneTag);
            obj.m_debugName = info.m_zoneTag;
        }
    }
}
