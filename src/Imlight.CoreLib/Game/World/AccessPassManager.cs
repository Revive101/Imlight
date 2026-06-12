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
 * ACCESS PASS 
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and validation of zone access passes
 * as they appear in the ROot.wad
 * 
 * USAGE EXAMPLE:
 * var zoneExists = AccessPassManager.DoesZoneExist("WizardCity/WC_Hub");
 * var matchedZone = AccessPassManager.GetContainedZoneName("WC_Hub");
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Manages the loading and validation of zone access passes.
/// </summary>
internal class AccessPassManager : RootSingleResourceSingleton<AccessPassManager>, IMemoryStreamDisposable {

    protected override string ResourceName { get; } = "AccessPass.xml";

    private static string[] s_zones;

    protected override void AfterLoad() {
        // Use the XmlReader to read the file. Only the zone names are needed,
        // so we find the <Zone> tags and read the name attribute.
        var zoneList = new HashSet<string>();
        var doc = new XmlDocument();
        doc.Load(Stream);

        foreach (XmlNode zoneNode in doc.GetElementsByTagName("Zone")) {
            var zoneName = zoneNode.InnerText;
            zoneList.Add(zoneName);
        }

        // Log
        Logger.Information("Loaded {Count} zones.", Logger.Args(zoneList.Count));

        s_zones = [.. zoneList];
        ((IMemoryStreamDisposable) this).DisposeStream();
    }

    /// <summary>
    /// Checks if a zone exists in the access pass list.
    /// </summary>
    /// <param name="zoneName">The name of the zone to check.</param>
    /// <returns>True if the zone exists, false otherwise.</returns>
    internal static bool DoesZoneExist(string zoneName)
        => s_zones.Any(zone => zone.Equals(zoneName?.ToLower(), System.StringComparison.CurrentCultureIgnoreCase));

    /// <summary>
    /// Checks if a zone name contains a partial zone name.
    /// </summary>
    /// <param name="partialZoneName">The partial zone name to check.</param>
    /// <returns>True if the zone name contains the partial zone name, false otherwise.</returns>
    internal static string GetContainedZoneName(string partialZoneName)
        => s_zones.FirstOrDefault(zone => zone.Contains(partialZoneName, System.StringComparison.CurrentCultureIgnoreCase));

    void IMemoryStreamDisposable.DisposeStream() => base.Stream?.Dispose();

}
