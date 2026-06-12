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
