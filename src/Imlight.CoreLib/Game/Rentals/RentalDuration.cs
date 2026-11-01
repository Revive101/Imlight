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
 * RENTAL DURATION
 * ========================================================================
 *
 * PURPOSE:
 * Parses the duration string of a RentalBehaviorTemplate ("7d", "86400", "1d2h") into a TimeSpan.
 *
 * USAGE EXAMPLE:
 * if (RentalDuration.TryParse(rentalTemplate.m_expireTime, out var duration)) { expireTime = now + (long) duration.TotalSeconds; }
 *
 * NOTE:
 * Only ever fed the template string. A part without a unit counts seconds, as in the client's own tooltip parser.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Imlight.CoreLib.Game.Rentals;

internal static class RentalDuration {

    private static readonly Regex s_wholeString = new(@"^(?:[0-9]+[dhms])*[0-9]*\z", RegexOptions.CultureInvariant);
    private static readonly Regex s_part = new("([0-9]+)([dhms]?)", RegexOptions.CultureInvariant);

    internal static bool TryParse(string value, out TimeSpan duration) {
        duration = TimeSpan.Zero;

        if (string.IsNullOrEmpty(value) || !s_wholeString.IsMatch(value)) {
            return false;
        }

        ulong totalSeconds = 0;
        foreach (Match part in s_part.Matches(value)) {
            if (!ulong.TryParse(part.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)) {
                return false;
            }

            var unitSeconds = part.Groups[2].Value switch {
                "d" => 86400UL,
                "h" => 3600UL,
                "m" => 60UL,
                _ => 1UL
            };

            if (amount > uint.MaxValue / unitSeconds) {
                return false;
            }

            totalSeconds += amount * unitSeconds;
            if (totalSeconds > uint.MaxValue) {
                return false;
            }
        }

        if (totalSeconds == 0) {
            return false;
        }

        duration = TimeSpan.FromSeconds(totalSeconds);

        return true;
    }

}
