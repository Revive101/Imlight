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
 * Finds the items a new house starts with: the initial layout its template names, or the layout file
 * its lot zone archive ships.
 * 
 * USAGE EXAMPLE:
 * var items = HouseLayouts.Resolve(template, template.LotZone);
 * 
 * NOTE:
 * - A template names its layout in the client's pipe-delimited asset form: archive, folder and file. The archive
 *   is the first two parts joined by '-'.
 * - A template that names none uses the first file in the root of the lot zone's own archive that reads as a
 *   HousingLayout. A zone with no such file starts empty.
 * - A layout is static game data, so the result is kept for the life of the server.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Houses;

internal static class HouseLayouts {

    private const char ASSET_SEPARATOR = '|';
    private const int ASSET_PARTS = 3;

    private static readonly ConcurrentDictionary<(string LayoutFile, string ZoneName), IReadOnlyList<PlacedItem>> s_resolved = new();

    /// <summary>
    /// Gets the items a new house of a template starts with.
    /// </summary>
    /// <param name="template">The house template.</param>
    /// <param name="zoneName">The lot zone whose archive may ship the layout.</param>
    /// <returns>
    /// The items without ids, in layout order; empty when the house starts empty; <c>null</c> when an archive or the
    /// named layout file cannot be read, so the house must not be created yet.
    /// </returns>
    internal static IReadOnlyList<PlacedItem> Resolve(HouseTemplateInfo template, string zoneName) {
        var key = (template.LayoutFile ?? string.Empty, zoneName);
        if (s_resolved.TryGetValue(key, out var known)) {
            return known;
        }

        var layout = string.IsNullOrEmpty(template.LayoutFile)
            ? FindInZoneArchive(zoneName)
            : ReadNamed(template.LayoutFile);
        if (layout is null) {
            return null;
        }

        var items = new List<PlacedItem>();
        foreach (var layoutObject in layout.m_housingLayoutObjectList ?? []) {
            var item = PlacedItem.FromLayout(layoutObject);
            if (item is null) {
                Logger.Error("A housing layout object of template {TemplateId} cannot be stored: {ObjectTemplate}",
                    Logger.Args(template.TemplateId, layoutObject?.m_templateID ?? 0));

                continue;
            }

            items.Add(item);
        }

        return s_resolved.GetOrAdd(key, items);
    }

    private static HousingLayout ReadNamed(string layoutFile) {
        var parts = layoutFile.Split(ASSET_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != ASSET_PARTS) {
            Logger.Error("The layout file {LayoutFile} is not an archive, a folder and a file.", 
                Logger.Args(layoutFile));

            return null;
        }

        var archiveName = $"{parts[0]}-{parts[1]}";
        if (!ResourceManager.TryLoadArchive(archiveName, out var wad)) {
            Logger.Error("Could not load the archive {ArchiveName} of the layout {LayoutFile}.", 
                Logger.Args(archiveName, layoutFile));

            return null;
        }

        var bytes = wad.OpenFile(parts[2]);
        if (!bytes.HasValue || !TryDeserialize(bytes.Value.ToArray(), out var layout)) {
            Logger.Error("Could not read the layout {LayoutFile}.", 
                Logger.Args(layoutFile));

            return null;
        }

        return layout;
    }

    private static HousingLayout FindInZoneArchive(string zoneName) {
        if (!ResourceManager.TryLoadArchive(zoneName, out var wad)) {
            Logger.Error("Could not load the archive of house zone {ZoneName}.", 
                Logger.Args(zoneName));

            return null;
        }

        foreach (var fileName in wad.Files.Keys.Where(name => !name.Contains('/')).Order(StringComparer.Ordinal)) {
            var bytes = wad.OpenFile(fileName);
            if (bytes.HasValue && TryDeserialize(bytes.Value.ToArray(), out var layout)) {
                return layout;
            }
        }

        return new HousingLayout { m_housingLayoutObjectList = [] };
    }

    private static bool TryDeserialize(byte[] bytes, out HousingLayout layout) {
        layout = null;
        try {
            if (new BindSerializer().Deserialize<PropertyClass>(bytes, 1, out var parsed) && parsed is HousingLayout found) {
                layout = found;

                return true;
            }
        } catch (Exception) {
            // A file that is not a layout can fail anywhere in the decode.
        }

        return false;
    }

}
