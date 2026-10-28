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
 * The house zones the client's own list names (HousingNames.xml), so the server can tell a house zone
 * from an ordinary one without looking at names or paths.
 * 
 * USAGE EXAMPLE:
 * HouseZoneIndex.Preload();
 * var isHouse = HouseZoneIndex.IsHouseZone(zoneName);
 * 
 * NOTE:
 * - The list holds the lot zone of every deed and structure template, nothing else. A zone name prefix
 *   is not a test: some dungeon zones whose names start the same way are ordinary zones.
 * - Every house zone is registered as an always-private attach target.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Housing.Houses;

/// <summary>
/// The zones the game data lists as houses.
/// </summary>
internal sealed class HouseZoneIndex : RootSingleResourceSingleton<HouseZoneIndex>, IMemoryStreamDisposable {

    private static int s_privateZoneTestRegistered;

    private HashSet<string> _zones;

    protected override string ResourceName => "HousingNames.xml";

    protected override void AfterLoad() {
        if (!new BindSerializer().Deserialize(Stream.ToArray(), 1, out HouseNameList names)) {
            throw new InvalidOperationException("Could not deserialize HousingNames.xml.");
        }

        _zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in names.m_houseZoneList ?? []) {
            _zones.Add(zone.m_zoneName.ToString());
        }
    }

    /// <summary>
    /// Loads the zone list and registers house zones as always-private attach targets.
    /// Throws when the list is missing from the game data.
    /// </summary>
    internal static void Preload() {
        _ = Instance;

        if (Interlocked.Exchange(ref s_privateZoneTestRegistered, 1) == 0) {
            ZoneInstanceOwners.RegisterPrivateZoneTest(IsHouseZone);
            Logger.Information("Indexed {Count} house zones.", Logger.Args(Count));
        }
    }

    /// <summary>
    /// Checks whether a zone is the lot of a house.
    /// </summary>
    /// <param name="zoneName">The zone name, compared without regard to case.</param>
    /// <returns><c>true</c> for a house zone.</returns>
    internal static bool IsHouseZone(string zoneName)
        => !string.IsNullOrEmpty(zoneName) && Instance._zones.Contains(zoneName);

    /// <summary>
    /// Gets the number of house zones.
    /// </summary>
    internal static int Count => Instance._zones.Count;

    public void DisposeStream() => Stream.Dispose();

}
