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

        Logger.Information("Loaded {0} world hub zones", Logger.Args(s_worldHubZoneMap.m_hubZoneMapping.Count));
    }

    internal static HubZoneMapping GetHubZoneMapping(string worldName) {
        // Get the world name, which should be the first element if we split the zone name by '/'.
        var worldNameSplits = worldName.Split('/');
        if (worldNameSplits.Length > 1) {
            worldName = worldNameSplits[0];
        }

        return s_worldHubZoneMap.m_hubZoneMapping.FirstOrDefault(hubMap => hubMap.m_world == worldName);
    }

    public void DisposeStream() => Stream.Dispose();
}
