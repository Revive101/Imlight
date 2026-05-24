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
*/

using System;
using System.Linq;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class ZoneDataCollection {

    /// <summary>
    /// Retrieves zone data by zone name.
    /// </summary>
    /// <param name="zoneName">The name of the zone.</param>
    /// <returns>The zone data, or null if not found.</returns>
    public static WizardZoneData GetZoneData(string zoneName) 
        => SpiralDB.GetZoneData(zoneName);

    /// <summary>
    /// Retrieves a random zone data entry.
    /// </summary>
    /// <returns> A random zone data. </returns>
    /// <remarks> This method is used for the April Fools event. </remarks>
    public static WizardZoneData GetAprilFoolsRandomZoneData() {
        var allZones = SpiralDB.GetAllZoneData();
        if (allZones.Count == 0) {
            return null;
        }

        var random = new Random();
        var index = random.Next(0, allZones.Count);

        return allZones.ElementAt(index);
    }

}
