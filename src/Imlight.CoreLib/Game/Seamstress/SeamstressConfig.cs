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
 * SEAMSTRESS CONFIG
 * ========================================================================
 *
 * PURPOSE:
 * Reads the [Seamstress] section of Imlight.ini: the Crowns prices and the shop title key.
 *
 * USAGE EXAMPLE:
 * if (SeamstressConfig.TryGetPrices(out var stitchCrowns, out var unstitchCrowns)) { ... }
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System.Threading;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Seamstress;

/// <summary>
/// Settings of the seamstress NPC, read from the [Seamstress] section of Imlight.ini.
/// </summary>
internal static class SeamstressConfig {

    private const string STITCH_CROWNS_KEY = "Seamstress.StitchCrowns";
    private const string UNSTITCH_CROWNS_KEY = "Seamstress.UnstitchCrowns";

    private static int s_warnedUnsetPrices;

    /// <summary>
    /// Reads the Crowns prices of one stitch and one unstitch.
    /// </summary>
    /// <param name="stitchCrowns">The price of a stitch.</param>
    /// <param name="unstitchCrowns">The price of an unstitch.</param>
    /// <returns>False when either price is missing or negative. A price of 0 is allowed.</returns>
    internal static bool TryGetPrices(out int stitchCrowns, out int unstitchCrowns) {
        stitchCrowns = ConfigurationManager.GetValue(STITCH_CROWNS_KEY, -1);
        unstitchCrowns = ConfigurationManager.GetValue(UNSTITCH_CROWNS_KEY, -1);
        if (stitchCrowns >= 0 && unstitchCrowns >= 0) {
            return true;
        }

        if (Interlocked.Exchange(ref s_warnedUnsetPrices, 1) == 0) {
            Logger.Warning("[Seamstress] StitchCrowns and UnstitchCrowns must both be set to 0 or more, " +
            "so the seamstress options are not offered. StitchCrowns={Stitch}, UnstitchCrowns={Unstitch}.",
                Logger.Args(stitchCrowns, unstitchCrowns));
        }

        return false;
    }

}
