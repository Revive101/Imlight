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

using Imcodec.Math;
using System;

namespace Imlight.CoreLib.Shared.Resources;

internal static class Util {

    public static string GetCompactStringFromVector(Vector4 vector)
        => $"{vector.X},{vector.Y},{vector.Z},{vector.W}";

    public static string GetCompactStringFromVector(Vector3 vector, Vector3 orientation)
        => $"{vector.X},{vector.Y},{vector.Z},{orientation.Z}";

    public static Vector4 GetVectorFromCompactString(string loc) {
        if (loc.Split(',').Length != 4) {
            return Vector4.Zero;
        }

        var components = loc.Split(",");
        var x = float.TryParse(components[0], out var xVal) ? xVal : 0;
        var y = float.TryParse(components[1], out var yVal) ? yVal : 0;
        var z = float.TryParse(components[2], out var zVal) ? zVal : 0;
        var d = float.TryParse(components[3], out var dVal) ? dVal : 0;

        return new Vector4(x, y, z, d);
    }

    public static bool IsDateTimeNowBetween(DateTime start, DateTime end) {
        // Get the current year from DateTime.Now
        var currentYear = DateTime.Now.Year;

        // Create new DateTime instances with the current year but the same day and month
        var startWithCurrentYear = new DateTime(currentYear, start.Month, start.Day);
        var endWithCurrentYear = new DateTime(currentYear, end.Month, end.Day);

        // Check if DateTime.Now is between startWithCurrentYear and endWithCurrentYear
      
        return DateTime.Now >= startWithCurrentYear && DateTime.Now <= endWithCurrentYear;
    }
}
