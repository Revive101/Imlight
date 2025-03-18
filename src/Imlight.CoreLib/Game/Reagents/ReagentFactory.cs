/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Reagents;

public class ReagentFactory : RootDirectoryResourceSingleton<ReagentFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "ObjectData/Reagents/";

    private static readonly Dictionary<string, ReagentItemTemplate> s_reagentTemplates = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<ReagentItemTemplate>(fileStream?.ToArray(), out var reagentTemplate)) {
                Logger.Error("Could not deserialize {0} as {1}", Logger.Args(fileRecord.FileName, nameof(ReagentItemTemplate)));

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

        if (reagentName.Contains("flax")) { // KI naming inconsistency
            reagentName = "flax-01"; 
        }

        if (reagentName.Contains("lron")) { // KI can't spell
            reagentName = "scrapiron";
        }

        if (reagentName.Contains(' ')) {
            reagentName = reagentName.Replace(" ", "");
        }

        var allReagents = s_reagentTemplates.Keys.Where(x => x.Contains(reagentName)).ToList();

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
            Logger.Warning("Could not find reagent template ID {0}.", 
                Logger.Args(templateId));

            return null;
        }

        if (templateFound is not ReagentItemTemplate) {
            Logger.Warning("Could not find reagent template ID {0}.", 
                Logger.Args(templateId));

            return null;
        }

        var obj = (ClientReagentItem) CoreObjectFactory.FinalizeCoreObject(templateId);
        obj.m_displayKey = templateFound.m_displayName;

        return (ClientReagentItem) CoreObjectFactory.FinalizeCoreObject(templateId);
    }

    /// <summary>
    /// Retrieves a harvestable reagent by its name.
    /// </summary>
    /// <param name="reagentName">The name of the reagent.</param>
    /// <returns>The reagent with the specified name, or null if the reagent template is not found.</returns>
    public static ClientReagentItem GetHarvestable(string reagentName) {
        // Example reagent     : Harvest-Mushroom-01
        // Attach "harvest" prefix if not already present.
        if (!reagentName.Contains("harvest")) {
            reagentName = "harvest-" + reagentName;
        }

        // Attach "-01" suffix if not already present.
        if (!reagentName.Contains("-01")) {
            reagentName += "-01";
        }

        return GetReagent(reagentName);
    }

    /// <summary>
    /// Retrieves a rare variant of a harvestable reagent by its non-rare counterpart's name.
    /// </summary>
    /// <param name="reagentName">The name of the reagent.</param>
    /// <returns>The rare variant of the reagent with the specified name, or null if the reagent template is not found.</returns>
    public static ClientReagentItem GetHarvestableRareVariant(string reagentName) {
        // Example reagent     : Harvest-Mushroom-01
        // Example rare variant: Harvest-Mushroom-Nightshade-01
        // All rare harvestable variants follow the schema of Harvest-<reagentName>-<rareVariantName>-<variantNumber>
        reagentName = reagentName.ToLower();

        // Edge case: Flax is the only reagent that doesn't follow the pattern.
        if (reagentName.Contains("flax")) {
            reagentName = "flax";
        }

        // Put the harvest prefix if it doesn't exist.
        if (!reagentName.Contains("harvest")) {
            reagentName = "harvest-" + reagentName;
        }

        // Remove any spaces in the reagent name.
        reagentName = reagentName.Replace(" ", "");

        if (reagentName.Contains("lron")) { // KI can't spell
            reagentName = "scrapiron";
        }

        // Find the reagent template that contains the reagent name, but is not the exact
        // reagent name. 
        var allReagents = s_reagentTemplates.Keys.Where(x => x.Contains(reagentName)).ToList();
        foreach (var kp in s_reagentTemplates) {
            var suffixAttachedReagentName = reagentName + "-01";

            if (kp.Key.Contains(reagentName) && !kp.Key.Equals(suffixAttachedReagentName)) {
                return GetReagent(kp.Value.m_templateID);
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves a reagent template by its name.
    /// </summary>
    /// <param name="reagentName">The name of the reagent.</param>
    /// <returns>The reagent template with the specified name, or null if the reagent template is not found.</returns>
    public static ReagentItemTemplate GetReagentTemplate(string reagentName) {
        reagentName = reagentName.ToLower();

        if (reagentName.Contains("flax")) { // KI naming inconsistency
            reagentName = "flax-01"; 
        }

        if (reagentName.Contains("lron")) { // KI can't spell
            reagentName = "scrapiron";
        }

        if (reagentName.Contains(' ')) {
            reagentName = reagentName.Replace(" ", "");
        }

         foreach (var kp in s_reagentTemplates) {
            if (kp.Key.Contains(reagentName)) {
                return kp.Value;
            }
        }

        Logger.Warning("Could not find reagent template by name {0}.", Logger.Args(reagentName));

        return null;
    }

    public void DisposeStream() {
        s_reagentTemplates.Clear();
    }

}
