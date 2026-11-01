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
 * HOUSING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Reads what a house template says about itself: its lot zone, interior structure, preview zone, initial
 * layout and item limits. A house template is any item template with a DeedBehaviorTemplate.
 * 
 * USAGE EXAMPLE:
 * if (HouseTemplates.TryGet(item.m_templateID, out var info)) {
 *     var zone = info.LotZone;
 * }
 * 
 * NOTE:
 * - Templates are resolved by id when asked, never by scanning the catalog or by file name.
 * - A deed with a structure has two lots: the deed's zone is the exterior and the structure's zone the interior.
 *   A deed without one is a house of one zone. Whether a template is a deed or a structure comes from its object type.
 * - The starter templates are the entries of Housing.StarterDeeds that are house templates. Boot checks the list.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// What a house template defines.
/// </summary>
/// <param name="TemplateId">The template id of the deed or structure item.</param>
/// <param name="LotZone">The zone of the house's lot.</param>
/// <param name="StructureTemplateId">The interior structure's template id, or 0 for none. Only a deed's value means anything: 
///     a structure names itself, nothing, or by a data mistake another structure.</param>
/// <param name="LayoutFile">The initial layout in the client's pipe-delimited asset form, or empty.</param>
/// <param name="ItemLimit">The template's item limit, before any bonus slots.</param>
/// <param name="IsStructure">Whether the template is a structure, the interior side of a residence, rather than a deed.</param>
internal readonly record struct HouseTemplateInfo(
    uint TemplateId,
    string LotZone,
    uint StructureTemplateId,
    string LayoutFile,
    int ItemLimit,
    bool IsStructure = false) {

    /// <summary>
    /// Whether the house is a deed with a structure of its own besides the lot.
    /// </summary>
    internal bool HasStructure => !IsStructure && StructureTemplateId != 0 && StructureTemplateId != TemplateId;

}

/// <summary>
/// Finds the house definition of a template.
/// </summary>
internal static class HouseTemplates {

    private static readonly ConcurrentDictionary<uint, HouseTemplateInfo> s_known = new();

    /// <summary>
    /// Gets the house definition of a template.
    /// </summary>
    /// <param name="templateId">The template id of an item.</param>
    /// <param name="info">The definition when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the template does not exist or is not a house.</returns>
    internal static bool TryGet(uint templateId, out HouseTemplateInfo info) {
        if (s_known.TryGetValue(templateId, out info)) {
            return true;
        }

        var itemTemplate = CoreObjectFactory.GetCoreTemplate(templateId);
        var deed = itemTemplate?.m_behaviors?.OfType<DeedBehaviorTemplate>().FirstOrDefault();
        var lotZone = deed?.m_lotWorldZone.ToString();
        if (string.IsNullOrEmpty(lotZone)) {
            info = default;

            return false;
        }

        info = s_known.GetOrAdd(templateId, new HouseTemplateInfo(
            templateId,
            lotZone,
            deed.m_structureTemplateID,
            deed.m_initialItemLayoutFile.ToString(),
            deed.m_itemLimit,
            itemTemplate is WizItemTemplate { m_nObjectType: ObjectType.OT_STRUCTURE }));

        return true;
    }

    /// <summary>
    /// Gets the starter houses: the entries of <c>Housing.StarterDeeds</c> that are house templates, in order.
    /// </summary>
    /// <returns>The definitions of the starter templates.</returns>
    internal static IReadOnlyList<HouseTemplateInfo> Starters() {
        var starters = new List<HouseTemplateInfo>();
        foreach (var templateId in HousingSettings.StarterDeeds) {
            if (TryGet(templateId, out var info)) {
                starters.Add(info);
            }
        }

        return starters;
    }

    /// <summary>
    /// Checks at boot that every entry of <c>Housing.StarterDeeds</c> is a house template, and logs the ones that are not.
    /// </summary>
    /// <returns><c>true</c> when the list names at least one house and no entry that is not one.</returns>
    internal static bool ValidateStarters() {
        var valid = true;
        var starters = HousingSettings.StarterDeeds;
        foreach (var templateId in starters) {
            if (!TryGet(templateId, out _)) {
                Logger.Error("Housing.StarterDeeds names template {TemplateId}, which is not a house template.", Logger.Args(templateId));
                valid = false;
            }
        }

        if (starters.Count == 0) {
            Logger.Warning("Housing.StarterDeeds is empty, so no character has a home to go to.");
            valid = false;
        }

        return valid;
    }

}
