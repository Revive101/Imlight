/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using SharpDX;

namespace Imlight.Server.Shared.Resources;

public static class Util
{
    public static string GetCompactStringFromVector(Vector4 vector)
        => $"{vector.X},{vector.Y},{vector.Z},{vector.W}";
        
    public static string GetCompactStringFromVector(Vector3 vector, Vector3 orientation)
        => $"{vector.X},{vector.Y},{vector.Z},{orientation.Z}";
        
    public static Vector4 GetVectorFromCompactString(string loc)
    {
        if (!loc.Contains(','))
            return Vector4.Zero;
            
        var components = loc.Split(",");
        var x = float.TryParse(components[0], out var xVal) ? xVal : 0;
        var y = float.TryParse(components[1], out var yVal) ? yVal : 0;
        var z = float.TryParse(components[2], out var zVal) ? zVal : 0;
        var d = float.TryParse(components[3], out var dVal) ? dVal : 0;

        return new Vector4(x, y, z, d);
    }
        
    public static bool IsDateTimeNowBetween(DateTime start, DateTime end)
    {
        // Get the current year from DateTime.Now
        var currentYear = DateTime.Now.Year;

        // Create new DateTime instances with the current year but the same day and month
        var startWithCurrentYear = new DateTime(currentYear, start.Month, start.Day);
        var endWithCurrentYear = new DateTime(currentYear, end.Month, end.Day);

        // Check if DateTime.Now is between startWithCurrentYear and endWithCurrentYear
        return DateTime.Now >= startWithCurrentYear && DateTime.Now <= endWithCurrentYear;
    }
}