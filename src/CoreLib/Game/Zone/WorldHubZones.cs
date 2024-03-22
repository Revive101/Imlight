/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

public class WorldHubZones : RootSingleResourceSingleton<WorldHubZones>, IMemoryStreamDisposable {

    protected override string ResourceName => "WorldHubZones.xml";

    private static WorldHubZoneMapper s_worldHubZoneMap;
    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        var propClass = serializer.OpenClass<WorldHubZoneMapper>(Stream);

        s_worldHubZoneMap = propClass;

        Logger.Information("Loaded {0} World Hub Zones", Logger.Args(s_worldHubZoneMap.m_hubZoneMapping.Count()));
    }

    internal static string GetWorldHubLocation(string worldName) {
        HubZoneMapping hub = s_worldHubZoneMap.m_hubZoneMapping.FirstOrDefault(x => x.m_world == worldName);
        return hub.m_universeTPZone;
    }

    public void DisposeStream() => Stream.Dispose();
}
