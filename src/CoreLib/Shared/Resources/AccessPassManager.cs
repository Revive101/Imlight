/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Resources;

public static class AccessPassManager {
    private const string RootWadName = "Root.wad";
    private const string AccessPassName = "AccessPass.xml";

    private static string[] s_zones;

    public static bool Load() {
        if (!ResourceManager.TryLoadFile(RootWadName, AccessPassName, out var stream)) {
            return false;
        }

        // Use the XmlReader to read the file. Only the zone names are needed,
        // so we find the <Zone> tags and read the name attribute.
        var zoneList = new HashSet<string>();
        var doc = new XmlDocument();
        doc.Load(stream);

        foreach (XmlNode zoneNode in doc.GetElementsByTagName("Zone")) {
            var zoneName = zoneNode.InnerText;
            zoneList.Add(zoneName);
        }

        // Log
        Logger.Information("AccessPassManager loaded {Count} zones.", Logger.Args(zoneList.Count));

        s_zones = zoneList.ToArray();

        return true;
    }

    public static bool DoesZoneExist(string zoneName) {
        return s_zones.Any(zone => zone == zoneName);
    }
}
