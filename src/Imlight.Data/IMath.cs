using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Data
{
    public static class IMath
    {
        /// <summary>
        ///     Determines if a 2D point is within a convex polygon.
        /// </summary>
        /// <param name="p">An array of 2D points to describe a polygon.</param>
        /// <param name="n">The number of points the polygon has.</param>
        /// <param name="pos">The position to be checked.</param>
        /// <returns>If the position is inside of the polygon or not.</returns>
        public static bool InsideOfPolygon(SharpDX.Vector2[] p, int n, SharpDX.Vector2 pos)
        {
            double angle = 0;
            SharpDX.Vector2 p1, p2;

            for (int i = 0; i < n; i++)
            {
                p1.X = p[i].X - pos.X;
                p1.Y = p[i].Y - pos.Y;
                p2.X = p[(i + 1) % n].X - pos.X;
                p2.Y = p[(i + 1) % n].Y - pos.Y;

                angle += Angle2D(p1.X, p1.Y, p2.X, p2.Y);
            }
            return (Math.Abs(Math.Abs(angle) - (Math.PI * 2)) < 0.01); //Some tolerance for rounding errors
        }

        private static double Angle2D(float x1, float y1, float x2, float y2)
        {
            double diff, theta1, theta2;

            theta1 = Math.Atan2(y1, x1);
            theta2 = Math.Atan2(y2, x2);
            diff = theta2 - theta1;
            while (diff > Math.PI)
                diff -= Math.PI * 2;
            while (diff < -Math.PI)
                diff += Math.PI * 2;

            return diff;
        }

        /// <summary>
        ///     Determines if a 2D point is within a circle around another 2D point.
        /// </summary>
        /// <param name="p">The origin of the circle.</param>
        /// <param name="n">The radius of the circle.</param>
        /// <param name="pos">The position to be checked.</param>
        /// <returns>If the position is inside of the circle or not.</returns>
        public static bool InsideOfCircle(SharpDX.Vector2 p, float radius, SharpDX.Vector2 pos)
        {
            return Math.Abs(SharpDX.Vector2.Distance(p, pos)) < radius;
        }
    }
}
