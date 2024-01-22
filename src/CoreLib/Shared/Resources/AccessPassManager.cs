/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Resources;

public class AccessPassManager : RootSingleResourceSingleton<AccessPassManager>, IMemoryStreamDisposable {
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

        s_zones = zoneList.ToArray();
        ((IMemoryStreamDisposable)this).DisposeStream();
    }

    public static bool DoesZoneExist(string zoneName)
        => s_zones.Any(zone => zone.ToLower() == zoneName?.ToLower());

    public static string GetContainedZoneName(string partialZoneName)
        => s_zones.FirstOrDefault(zone => zone.ToLower().Contains(partialZoneName.ToLower()));

    void IMemoryStreamDisposable.DisposeStream() => base.Stream?.Dispose();
}
