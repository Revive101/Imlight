/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Common.Utilities;

public static class Math
{
    /// <summary>
    ///     Determines if a 2D point is within a convex polygon.
    /// </summary>
    /// <param name="p">An array of 2D points to describe a polygon.</param>
    /// <param name="pos">The position to be checked.</param>
    /// <returns>If the position is inside of the polygon or not.</returns>
    public static bool InsideOfPolygon(SharpDX.Vector2[] p, SharpDX.Vector2 pos)
    {
        double angle = 0;
        SharpDX.Vector2 p1, p2;
        int n = p.Length;

        for (int i = 0; i < n; i++)
        {
            p1.X = p[i].X - pos.X;
            p1.Y = p[i].Y - pos.Y;
            p2.X = p[(i + 1) % n].X - pos.X;
            p2.Y = p[(i + 1) % n].Y - pos.Y;

            angle += Angle2D(p1.X, p1.Y, p2.X, p2.Y);
        }
        return (System.Math.Abs(System.Math.Abs(angle) - (System.Math.PI * 2)) < 0.01); //Some tolerance for rounding errors
    }

    private static double Angle2D(float x1, float y1, float x2, float y2)
    {
        double diff, theta1, theta2;

        theta1 = System.Math.Atan2(y1, x1);
        theta2 = System.Math.Atan2(y2, x2);
        diff = theta2 - theta1;
        while (diff > System.Math.PI)
            diff -= System.Math.PI * 2;
        while (diff < -System.Math.PI)
            diff += System.Math.PI * 2;

        return diff;
    }

    /// <summary>
    ///     Determines if a 2D point is within a circle around another 2D point.
    /// </summary>
    /// <param name="p">The origin of the circle.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <param name="pos">The position to be checked.</param>
    /// <returns>If the position is inside of the circle or not.</returns>
    public static bool InsideOfCircle(SharpDX.Vector2 p, float radius, SharpDX.Vector2 pos)
    {
        return System.Math.Abs(SharpDX.Vector2.Distance(p, pos)) < radius;
    }

    /// <summary>
    ///     Determines if a 3D point is within a 6-sided prism.
    ///     Assumes that the first 4 points are on the "bottom", last 4 are the "top", and are iterated clockwise.
    ///     Prisms do not have to be perfect rectangle, but must be defined by 8 points.
    /// </summary>
    /// <param name="p">An array of 3D points to describe a prism.</param>
    /// <param name="pos">The position to be checked.</param>
    /// <returns>If the position is inside of the prism or not.</returns>
    public static bool InsideOfPrism(SharpDX.Vector3[] p, SharpDX.Vector3 pos)
    {
        var i = p[1] - p[0];
        var j = p[3] - p[0];
        var k = p[4] - p[0];
        var v = pos - p[0];

        var idot = SharpDX.Vector3.Dot(i, i);
        var jdot = SharpDX.Vector3.Dot(j, j);
        var kdot = SharpDX.Vector3.Dot(k, k);
            
        var vidot = SharpDX.Vector3.Dot(v, i);
        var vjdot = SharpDX.Vector3.Dot(v, j);
        var vkdot = SharpDX.Vector3.Dot(v, k);

        var vi = 0 <= vidot && vidot <= idot;
        var vj = 0 <= vjdot && vjdot <= jdot;
        var vk = 0 <= vkdot && vkdot <= kdot;

        return vi && vj && vk;
    }
}