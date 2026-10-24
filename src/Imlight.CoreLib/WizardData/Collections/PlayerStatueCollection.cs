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
 * PLAYER STATUE COLLECTION
 * ========================================================================
 *
 * PURPOSE:
 * Static access to the player statues SpiralDB loaded.
 *
 * USAGE EXAMPLE:
 * PlayerStatueCollection.GetInZone(zonePath);
 *
 * NOTE:
 * 
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Collections.Generic;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class PlayerStatueCollection {

    /// <summary>
    /// Attempts to retrieve a statue by its ID.
    /// </summary>
    /// <param name="id">The statue ID.</param>
    /// <param name="statue">The statue.</param>
    /// <returns>True if the statue was found, false otherwise.</returns>
    public static bool TryGet(string id, out PlayerStatue statue)
        => SpiralDB.TryGetPlayerStatue(id, out statue);

    /// <summary>
    /// Returns the enabled statues that stand in a zone.
    /// </summary>
    /// <param name="zonePath">The zone path.</param>
    public static IEnumerable<PlayerStatue> GetInZone(string zonePath)
        => SpiralDB.GetPlayerStatuesInZone(zonePath);

}
