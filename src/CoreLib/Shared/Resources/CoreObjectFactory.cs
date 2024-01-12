/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty;
using Imlight.Common.Utilities;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Resources;

public class CoreObjectFactory : RootResourceSingleton<CoreObjectFactory>, IMemoryStreamDisposable {
    protected override string ResourceName { get; } = "TemplateManifest.xml";

    private static readonly Dictionary<ulong, ByteString> s_coreTemplates = new();
    private static bool s_hasLoaded = false;

    protected override void AfterLoad() {
        // Load the TemplateManifest.xml and record the amount of time it takes.
        var timer = new Stopwatch();
        timer.Start();

        var fileSerializer = new FileSerializer();
        var manifest = fileSerializer.OpenClass<TemplateManifest>(Stream);

        if (manifest is null) {
            throw new Exception("Could not deserialize TemplateManifest.xml");
        }

        var templateCount = manifest.m_serializedTemplates.Count;
        Parallel.ForEach(manifest.m_serializedTemplates, templateLocation => {
            if (templateLocation is null) {
                return;
            }

            var id = templateLocation.m_id;
            var loc = templateLocation.m_filename;

            // Drop the MSB from the id.
            id &= 0xFFFFFFFF;

            lock (s_coreTemplates) {
                s_coreTemplates.Add(id, loc);
            }
        });

        Logger.Information("Loaded {TCount} CoreTemplates.", Logger.Args(templateCount));

        timer.Stop();
        Logger.Debug("{0} load took {Em}ms.", Logger.Args(ResourceName, timer.ElapsedMilliseconds));

        this.DisposeStream();
    }

    /// <summary>
    /// Initializes the behaviors of a core object with the specified ID.
    /// </summary>
    /// <typeparam name="T">The type of the core object.</typeparam>
    /// <param name="coreObject">The core object to initialize.</param>
    /// <param name="id">The ID of the core object.</param>
    /// <returns>The initialized core object.</returns>
    public static T InitializeCoreObjectBehaviors<T>(T coreObject, ulong id) where T : CoreObject, new() {
        var template = GetCoreTemplate(id);
        if (template is null) {
            Logger.Error("Could not initialize CoreObject from TemplateID {Tid}", Logger.Args(coreObject.m_templateID));
            return coreObject;
        }

        return InitializeCoreObjectBehaviors(coreObject, template);
    }

    /// <summary>
    /// Initializes the core object behaviors with the specified template.
    /// </summary>
    /// <typeparam name="T">The type of the core object.</typeparam>
    /// <param name="coreObject">The core object to initialize.</param>
    /// <param name="template">The core template containing behavior templates.</param>
    /// <returns>The initialized core object.</returns>
    public static T InitializeCoreObjectBehaviors<T>(T coreObject, CoreTemplate template) where T : CoreObject, new() {
        // The CoreTemplate contains a list of behavior templates. Using the name of the template,
        // we can find the instance of the behavior and add it to the CoreObject.
        coreObject.m_inactiveBehaviors = new List<BehaviorInstance>(template.m_behaviors.Count);
        foreach (var behaviorTemplate in template.m_behaviors) {
            if (behaviorTemplate is null) {
                coreObject.m_inactiveBehaviors.Add(null);
                continue;
            }

            // Hash the name and see if we can dispatch the behavior instance from that hash.
            var behaviorHash = StringHash.Compute(behaviorTemplate.m_behaviorName);
            var behaviorInstance = BehaviorCache.AllocateBehavior(behaviorHash);

            if (behaviorInstance is null) {
                coreObject.m_inactiveBehaviors.Add(null);
                continue;
            }

            // If we did find the instance, set it's name to be proper and add it to the CoreObject behaviors.
            behaviorInstance.m_behaviorTemplateNameID = behaviorHash;
            coreObject.m_inactiveBehaviors.Add(behaviorInstance);
        }

        return coreObject;
    }

    /// <summary>
    /// Finds an instance of a behavior in a CoreObject.
    /// </summary>
    /// <typeparam name="T">The type of behavior instance to find.</typeparam>
    /// <param name="coreObj">The CoreObject to search in.</param>
    /// <param name="behaviorInstance">The found behavior instance, if any.</param>
    /// <returns><c>true</c> if a behavior instance is found; otherwise, <c>false</c>.</returns>
    public static bool FindBehaviorInstance<T>(CoreObject coreObj, out T behaviorInstance) where T : BehaviorInstance {
        foreach (var behavior in coreObj.m_inactiveBehaviors.OfType<T>()) {
            behaviorInstance = behavior;
            return true;
        }

        behaviorInstance = default;
        return false;
    }

    /// <summary>
    /// Gets the CoreTemplate object with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the CoreTemplate.</param>
    /// <returns>The CoreTemplate object if found; otherwise, null.</returns>
    public static CoreTemplate GetCoreTemplate(ulong id) {
        if (!s_coreTemplates.TryGetValue(id, out var loc)) {
            Logger.Error("Could not find CoreTemplate by ID {Tid}", Logger.Args(id));
            return null;
        }

        var template = ResourceManager.LoadDeserializedFile<CoreTemplate>("Root.wad", loc);
        if (template is null) {
            Logger.Error("Could not load CoreTemplate from {Loc}", Logger.Args(loc));
        }
        return template ?? null;
    }

    /// <summary>
    /// Finalizes a CoreObject based on the provided CoreObjectInfo.
    /// </summary>
    /// <param name="objInfo">The CoreObjectInfo containing the necessary information for finalizing the CoreObject.</param>
    /// <returns>The finalized CoreObject.</returns>
    public static CoreObject FinalizeCoreObject(CoreObjectInfo objInfo) {
        var templateId = objInfo.m_templateID;
        var template = GetCoreTemplate(templateId);

        return FinalizeCoreObject(objInfo, template);
    }

    /// <summary>
    /// Finalizes a core object using the specified object information and template ID.
    /// </summary>
    /// <param name="objInfo">The object information.</param>
    /// <param name="templateId">The template ID.</param>
    /// <returns>The finalized core object.</returns>
    public static CoreObject FinalizeCoreObject(CoreObjectInfo objInfo, ulong templateId) {
        var template = GetCoreTemplate(templateId);

        return FinalizeCoreObject(objInfo, template);
    }

    /// <summary>
    /// Finalizes a CoreObject based on the provided CoreObjectInfo.
    /// </summary>
    /// <param name="objInfo">The CoreObjectInfo containing the necessary information for finalizing the CoreObject.</param>
    /// <param name="template">The CoreTemplate containing the behavior templates.</param>
    /// <returns>The finalized CoreObject.</returns>
    public static CoreObject FinalizeCoreObject(CoreObjectInfo objInfo, CoreTemplate template) {
        var obj = CreateCoreObjectFromTemplate(template);
        obj.m_templateID = objInfo.m_templateID;

        // Set the object properties.
        obj.m_location = objInfo.m_location;
        obj.m_orientation = objInfo.m_orientation;
        obj.m_fScale = objInfo.m_fScale;
        obj.m_globalID = RandomGen.GenerateGUID();
        obj.m_permID = RandomGen.GenerateHash($"{obj.m_zoneTagID}{obj.m_templateID}{obj.m_location.X}");
        obj.m_zoneTagID = StringHash.Compute(objInfo.m_zoneTag);
        obj.m_debugName = objInfo.m_zoneTag;

        // todo: set a property here for the english name of this object

        return obj;
    }

    private static CoreObject CreateCoreObjectFromTemplate(CoreTemplate template) {
        return template switch {
            ReagentItemTemplate => new ClientReagentItem(),
            ItemTemplate => new WizClientObjectItem(),
            WizGameObjectTemplate => new WizClientObject(),
            _ => new ClientObject()
        };
    }

    public void DisposeStream() {
        s_coreTemplates.Clear();
        Stream?.Dispose();
    }
}
