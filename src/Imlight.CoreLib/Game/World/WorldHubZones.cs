/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * WORLD HUB ZONES
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and retrieval of world hub zone mappings
 * as they are defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var hubZone = WorldHubZones.GetHubForZone("WizardCity/Streets/WC_Unicorn");
 * 
 * NOTE:
 * Hubs are usually the player gathering areas in the game, such as Wizard City Commons.
 * They are safe spaces.
 * 
 * TODO:
 * 
 * Created by: Jeff
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
/// Manages the loading and retrieval of world hub zone mappings.
/// </summary>
internal class WorldHubZones : RootSingleResourceSingleton<WorldHubZones>, IMemoryStreamDisposable {

    protected override string ResourceName => "WorldHubZones.xml";

    private static WorldHubZoneMapper s_worldHubZoneMap;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize(base.Stream.ToArray(), 1, out s_worldHubZoneMap)) {
            Logger.Error("Failed to deserialize WorldHubZones.xml");

            return;
        }

        Logger.Information("Loaded {0} world hub zones", 
            Logger.Args(s_worldHubZoneMap.m_hubZoneMapping.Count));
    }

    /// <summary>
    /// Get the hub zone mapping for a given world name. Assumes the world name
    /// is the first part of the zone name when split by '/'.
    /// </summary>
    /// <param name="worldName">The world name to search for.</param>
    /// <returns>The hub zone mapping for the given world name, or null if not found.</returns>
    internal static HubZoneMapping GetHubForZone(string worldName) {
        // Get the world name, which should be the first element if we split the zone name by '/'.
        var worldNameSplits = worldName.Split('/');
        if (worldNameSplits.Length > 1) {
            worldName = worldNameSplits[0];
        }

        return s_worldHubZoneMap.m_hubZoneMapping.FirstOrDefault(hubMap => hubMap.m_world == worldName);
    }

    public void DisposeStream() => Stream.Dispose();

}
