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
 * WORLD VENDOR LOCATIONS MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and validation of vendor location data
 * as it is defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var isVendor = WorldVendorLocations.IsVendor(vendorTemplateId);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Joji, Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Manages the loading and validation of vendor location data.
/// </summary>
internal class WorldVendorLocations : RootSingleResourceSingleton<WorldVendorLocations>, IMemoryStreamDisposable {

    protected override string ResourceName => "VendorLocationData.xml";

    private static ObjectLocationList s_vendorLocationMap;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize(base.Stream.ToArray(), 1, out s_vendorLocationMap)) {
            Logger.Error("Failed to deserialize vendor locations");

            return;
        }

        Logger.Information("Loaded {0} vendor locations", 
            Logger.Args(s_vendorLocationMap.m_objectList.Count));
    }

    /// <summary>
    /// Check if the template id is a vendor
    /// </summary>
    /// <param name="templateId">The template id to check</param>
    /// <returns>True if the template id is a vendor, false otherwise</returns>
    internal static bool IsVendor(uint templateId) 
        => s_vendorLocationMap.m_objectList.Any(x => x.m_templateID == templateId);

    public void DisposeStream() => Stream.Dispose();

}
