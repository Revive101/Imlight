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
using System.Text.RegularExpressions;

internal static class CommandUtilities {

    internal static bool TryParseDuration(string durationString, out TimeSpan result) {
        result = TimeSpan.Zero;

        // Use regular expression to match and extract components.
        // Anchor with ^...$ so the whole string must be a valid duration (no "abc123d" partials).
        var match = Regex.Match(durationString, @"^(\d+d)?(\d+h)?(\d+m)?(\d+s)?$");

        if (!match.Success) {
            return false;
        }

        var parsedAny = false;

        if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value.TrimEnd('d'), out int days)) {
            result += TimeSpan.FromDays(days);
            parsedAny = true;
        }

        if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value.TrimEnd('h'), out int hours)) {
            result += TimeSpan.FromHours(hours);
            parsedAny = true;
        }

        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value.TrimEnd('m'), out int minutes)) {
            result += TimeSpan.FromMinutes(minutes);
            parsedAny = true;
        }

        if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value.TrimEnd('s'), out int seconds)) {
            result += TimeSpan.FromSeconds(seconds);
            parsedAny = true;
        }

        // All groups are optional in the regex, so match.Success alone isn't enough —
        // we must have parsed at least one component. Otherwise overflow values like
        // "10000000000000m" would silently produce TimeSpan.Zero and return true.
        return parsedAny;
    }
    
}
