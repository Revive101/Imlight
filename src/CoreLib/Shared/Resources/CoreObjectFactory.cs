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

public class CoreObjectFactory : RootSingleResourceSingleton<CoreObjectFactory>, IMemoryStreamDisposable {
    protected override string ResourceName { get; } = "TemplateManifest.xml";

    public static TemplateManifest TemplateManifest;

    protected override void AfterLoad() {
        var fileSerializer = new FileSerializer();
        TemplateManifest = fileSerializer.OpenClass<TemplateManifest>(Stream)
            ?? throw new Exception("Could not deserialize TemplateManifest.xml");

        Logger.Information("Loaded {TCount} CoreTemplates.", Logger.Args(TemplateManifest.m_serializedTemplates.Count));

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
        if (template is null) {
            return coreObject;
        }

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
        var template = TemplateManifest.m_serializedTemplates.FirstOrDefault(x => x.m_id == id);
        if (template is null) {
            Logger.Error("Could not find CoreTemplate by ID {Tid}", Logger.Args(id));
            return null;
        }

        var templateObj = RootArchiveLoader.GetFile<CoreTemplate>(template.m_filename);
        if (template is null) {
            Logger.Error("Could not load CoreTemplate from {Loc}", Logger.Args(template.m_filename));
        }
        return templateObj ?? null;
    }

    /// <summary>
    /// Retrieves the ID of a core template based on its name.
    /// </summary>
    /// <param name="templateName">The name of the template.</param>
    /// <returns>The ID of the core template.</returns>
    public static uint GetCoreTemplateID(string templateName) {
        var template = TemplateManifest.m_serializedTemplates.FirstOrDefault(x => x.m_filename == templateName);
        if (template is null) {
            Logger.Error("Could not find CoreTemplate by name {TName}", Logger.Args(templateName));
            return 0;
        }

        return (uint)template.m_id;
    }

    /// <summary>
    /// Finalizes a CoreObject based on the provided template ID.
    /// </summary>
    /// <param name="templateId">The ID of the template associated with the core object.</param>
    /// <returns>The finalized core object.</returns>
    public static CoreObject FinalizeCoreObject(ulong templateId) {
        var template = GetCoreTemplate(templateId);

        // Create a blank CoreObjectInfo.
        var objInfo = new CoreObjectInfo {
            m_templateID = templateId,
            m_fScale = 1.0f,
        };

        return FinalizeCoreObject(objInfo, template);
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
        if (template is null) {
            Logger.Error("Could not finalize CoreObject from TemplateID {Tid}", Logger.Args(objInfo.m_templateID));
            return null;
        }

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

        // Check to see if the template has a field called "m_displayName."
        // If it does, set the debug name to the English name of the display name.
        if (template.GetType().GetField("m_displayName") is not null) {
            var displayNameValue = ((ByteString)template.GetType().GetField("m_displayName").GetValue(template)).ToString();
            var englishName = Locale.GetEnglishName(displayNameValue);
            obj.m_debugName = englishName;
        }

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
        Stream?.Dispose();
    }
}
