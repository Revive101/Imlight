/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Reagents;

public class ReagentFactory : RootDirectoryResourceSingleton<ReagentFactory>, IMemoryStreamDisposable {
    protected override string DirectoryName => "ObjectData/Reagents/";

    private static readonly Dictionary<string, ReagentItemTemplate> s_reagentTemplates = [];

    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            var reagentTemplate = serializer.OpenClass<ReagentItemTemplate>(fileStream);
            if (reagentTemplate is null) {
                Logger.Error("Could not deserialize {0} as {1}", Logger.Args(fileRecord.FileName, nameof(SpellTemplate)));
                continue;
            }

            var name = reagentTemplate.m_objectName
                .ToString()
                .ToLower();

            s_reagentTemplates.Add(name, reagentTemplate);

            counter++;
        }

        Logger.Information("Loaded {0} reagent templates.", Logger.Args(counter));
    }

    /// <summary>
    /// Retrieves a reagent by its name.
    /// </summary>
    /// <param name="reagentName">The name of the reagent.</param>
    /// <returns>The reagent with the specified name, or null if the reagent template is not found.</returns>
    public static ClientReagentItem GetReagent(string reagentName) {
        reagentName = reagentName.ToLower();    
        foreach (var kp in s_reagentTemplates) {
            if (kp.Key.Contains(reagentName)) {
                return GetReagent(kp.Value.m_templateID);
            }
        }

        Logger.Warning("Could not find reagent template by name {0}.", Logger.Args(reagentName));
        return null;
    }

    /// <summary>
    /// Creates a reagent from a template ID.
    /// </summary>
    /// <param name="templateId">The ID of the reagent template.</param>
    /// <returns>The created reagent object.</returns>
    public static ClientReagentItem GetReagent(uint templateId) {
        var templateFound = s_reagentTemplates.FirstOrDefault(x => x.Value.m_templateID == templateId).Value;

        if (templateFound is null) {
            Logger.Warning("Could not find reagent template ID {0}.", Logger.Args(templateId));
            return null;
        }

        if (templateFound is not ReagentItemTemplate) {
            Logger.Warning("Could not find reagent template ID {0}.", Logger.Args(templateId));
            return null;
        }

        return (ClientReagentItem) CoreObjectFactory.FinalizeCoreObject(templateId);
    }

    public void DisposeStream() {
        s_reagentTemplates.Clear();
    }
}
