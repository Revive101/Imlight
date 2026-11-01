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
 * HATCHING RULES
 * ========================================================================
 *
 * PURPOSE:
 * Holds the hatching settings of the [Hatching] section of Imlight.ini: hatch time, gold cost,
 * youngest parent, lends per day and the time a wizard can stay in the hatching room.
 *
 * USAGE EXAMPLE:
 * var lendsLeft = wizard.PetOwnerBehavior.GetLendsLeft(HatchingRules.LendsPerDay);
 *
 * NOTE:
 * A negative hatch time or gold cost means the game's own value, read from the client files.
 *
 * TODO:
 * - The game's lends per day and hatching room time are not in the client files; the defaults are guesses.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The hatching settings, with their defaults.
/// </summary>
internal static class HatchingRules {

    private const int USE_GAME_VALUE = -1;
    private const int YOUNGEST_PET_LEVEL = 1;
    private const int OLDEST_PET_LEVEL = 7;
    private const int ADULT_PET_LEVEL = 3;
    private const int MOST_LENDS_THE_WINDOW_SHOWS = byte.MaxValue;
    private const int DEFAULT_LENDS_PER_DAY = 5;
    private const int DEFAULT_ROOM_MINUTES = 5;

    /// <summary>
    /// The minutes until an egg hatches, or null to use the egg's own hatch time.
    /// </summary>
    internal static int? HatchMinutes { get; } = NullForGameValue(ConfigurationManager.GetValue("Hatching.HatchMinutes", USE_GAME_VALUE));

    /// <summary>
    /// The gold each hatching wizard pays per egg, or null to use the game's in-person cost.
    /// </summary>
    internal static int? GoldCost { get; } = NullForGameValue(ConfigurationManager.GetValue("Hatching.GoldCost", USE_GAME_VALUE));

    /// <summary>
    /// The youngest pet level that can be a parent.
    /// </summary>
    internal static byte MinParentLevel { get; } = (byte) Math.Clamp(ConfigurationManager.GetValue("Hatching.MinParentLevel", ADULT_PET_LEVEL),
                                                                     YOUNGEST_PET_LEVEL,
                                                                     OLDEST_PET_LEVEL);

    /// <summary>
    /// The pets a wizard can lend each day. Zero turns lending off.
    /// </summary>
    internal static byte LendsPerDay { get; } = (byte) Math.Clamp(ConfigurationManager.GetValue("Hatching.LendsPerDay", DEFAULT_LENDS_PER_DAY),
                                                                  0,
                                                                  MOST_LENDS_THE_WINDOW_SHOWS);

    /// <summary>
    /// How long a wizard can stay in the hatching room.
    /// </summary>
    internal static TimeSpan RoomTime { get; } = TimeSpan.FromMinutes(Math.Max(1, ConfigurationManager.GetValue("Hatching.MorphZoneMinutes", DEFAULT_ROOM_MINUTES)));

    private static int? NullForGameValue(int value)
        => value < 0 ? null : value;

}
