/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
