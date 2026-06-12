/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * REAGENT FACTORY MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading, management, and retrieval of reagent templates
 * from resource files as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var reagent = ReagentFactory.GetReagent("Mushroom");
 * var harvestableReagent = ReagentFactory.GetHarvestable("Mushroom");
 * 
 * NOTE:
 * Utilizes singleton pattern for resource management.
 * Implements complex name normalization and resolution strategies.
 * 
 * TODO:
 * 
 * Created by: Joji, Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Reagents;

/// <summary>
/// Manages the loading, caching, and retrieval of reagent templates from resource files.
/// </summary>
/// <remarks>
/// Provides multiple methods for accessing reagent templates with flexible name resolution.
/// Handles various edge cases in reagent naming and template retrieval.
/// Supports harvestable and rare variant reagent lookups.
/// </remarks>
internal class ReagentFactory : RootDirectoryResourceSingleton<ReagentFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "ObjectData/Reagents/";

    private static readonly Dictionary<string, ReagentItemTemplate> s_reagentTemplates = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var count = 0;

        foreach (var (fileRecord, fileStream) in base.Files) {
            if (!serializer.Deserialize<ReagentItemTemplate>(fileStream?.ToArray(), out var template)) {
                Logger.Error("Could not deserialize {0} as {1}", Logger.Args(fileRecord.FileName, nameof(ReagentItemTemplate)));
                continue;
            }

            var key = template.m_objectName.ToString().ToLower();
            s_reagentTemplates[key] = template;
            count++;
        }

        Logger.Information("Loaded {0} reagent templates.", 
            Logger.Args(count));
    }

    /// <summary>
    /// Retrieves a reagent instance by name, applying normalization and searching loaded templates.
    /// </summary>
    /// <param name="reagentName">The name of the reagent.</param>
    /// <returns>A <see cref="ClientReagentItem"/> instance if found; otherwise, null.</returns>
    internal static ClientReagentItem GetReagent(string reagentName) {
        var template = FindTemplateByName(reagentName);
        if (template is null) {
            Logger.Warning("Could not find reagent template by name {0}.", 
                Logger.Args(reagentName));

            return null;
        }

        return CreateReagent(template.m_templateID, template.m_displayName);
    }

    /// <summary>
    /// Retrieves a harvestable variant of a reagent by its base name.
    /// </summary>
    /// <param name="reagentName">The base name of the reagent.</param>
    /// <returns>A <see cref="ClientReagentItem"/> instance if found; otherwise, null.</returns>
    internal static ClientReagentItem GetHarvestable(string reagentName) {
        var normalized = reagentName.ToLower();
        if (!normalized.Contains("harvest")) {
            normalized = "harvest-" + normalized;
        }

        if (!normalized.Contains("-01")) {
            normalized += "-01";
        }

        return GetReagent(normalized);
    }

    /// <summary>
    /// Retrieves a rare variant of a harvestable reagent based on its base name.
    /// </summary>
    /// <param name="reagentName">The base name of the reagent.</param>
    /// <returns>A <see cref="ClientReagentItem"/> instance for the rare variant if found; otherwise, null.</returns>
    internal static ClientReagentItem GetHarvestableRareVariant(string reagentName) {
        var baseName = NormalizeName(reagentName);

        if (!baseName.Contains("harvest")) {
            baseName = "harvest-" + baseName;
        }

        var baseVariantName = baseName + "-01";

        var rareVariant = s_reagentTemplates
            .Where(kvp => kvp.Key.Contains(baseName) && kvp.Key != baseVariantName)
            .Select(kvp => kvp.Value)
            .FirstOrDefault();

        return rareVariant is null
            ? null
            : CreateReagent(rareVariant.m_templateID, rareVariant.m_displayName);
    }

    /// <summary>
    /// Retrieves a reagent template by its name using multiple resolution strategies.
    /// </summary>
    /// <param name="reagentName">The name of the reagent.</param>
    /// <returns>A <see cref="ReagentItemTemplate"/> if found; otherwise, null.</returns>
    internal static ReagentItemTemplate GetReagentTemplate(string reagentName) {
        var normalized = NormalizeName(reagentName);

        if (s_reagentTemplates.TryGetValue(normalized, out var exact)) {
            return exact;
        }

        var prefixMatch = s_reagentTemplates
            .FirstOrDefault(kp => kp.Key.StartsWith(normalized));
        if (prefixMatch.Key is not null) {
            return prefixMatch.Value;
        }

        var boundaryMatch = s_reagentTemplates
            .FirstOrDefault(kp => Regex.IsMatch(kp.Key, $@"(^|[-_]){Regex.Escape(normalized)}($|[-_])"));
        if (boundaryMatch.Key is not null) {
            return boundaryMatch.Value;
        }

        var substringMatch = s_reagentTemplates
            .FirstOrDefault(kp => kp.Key.Contains(normalized));
        if (substringMatch.Key is not null) {
            return substringMatch.Value;
        }

        Logger.Warning("Could not find reagent template by name {0}.", 
            Logger.Args(reagentName));

        return null;
    }

    private static string NormalizeName(string name) {
        var normalized = name.ToLower();

        if (normalized.Contains("flax")) {
            return "flax-01";
        }

        if (normalized.Contains("lron")) {
            normalized = normalized.Replace("lron", "iron");
        }

        return normalized.Replace(" ", "");
    }

    private static ReagentItemTemplate FindTemplateByName(string reagentName) {
        var normalized = NormalizeName(reagentName);

        return s_reagentTemplates
            .FirstOrDefault(kp => kp.Key.Contains(normalized))
            .Value;
    }

    private static ClientReagentItem CreateReagent(uint templateId, string displayName) {
        if (CoreObjectFactory.FinalizeCoreObject(templateId) is not ClientReagentItem obj) {
            Logger.Warning("Could not create reagent object for template ID {0}.", 
                Logger.Args(templateId));

            return null;
        }

        obj.m_displayKey = displayName;

        return obj;
    }

    public void DisposeStream() 
        => s_reagentTemplates.Clear();

}
