/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text.RegularExpressions;

internal static class CommandUtilities {

    internal static bool TryParseDuration(string durationString, out TimeSpan result) {
        result = TimeSpan.Zero;

        // Use regular expression to match and extract components
        var match = Regex.Match(durationString, @"(\d+d)?(\d+h)?(\d+m)?(\d+s)?");

        if (match.Success) {
            // Try to extract and convert each component
            if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value.TrimEnd('d'), out int days)) {
                result += TimeSpan.FromDays(days);
            }

            if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value.TrimEnd('h'), out int hours)) {
                result += TimeSpan.FromHours(hours);
            }

            if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value.TrimEnd('m'), out int minutes)) {
                result += TimeSpan.FromMinutes(minutes);
            }

            if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value.TrimEnd('s'), out int seconds)) {
                result += TimeSpan.FromSeconds(seconds);
            }

            return true;
        }

        return false;
    }
    
}
