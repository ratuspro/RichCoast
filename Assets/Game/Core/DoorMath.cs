using System;
using System.Collections.Generic;

namespace RichCoast.Core
{
    /// <summary>A Zone A ball as the trap-door sees it: centre + radius (world units, y-up).</summary>
    public readonly struct DoorCandidate
    {
        public readonly double X, Y, Radius;

        public DoorCandidate(double x, double y, double radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }

    /// <summary>
    /// Pure trap-door math (port of <c>nearestDoorBall</c> + the Zone C sweep + Zone B's split fan),
    /// shared by Zone C's grab and Zone A's candidate glow so the two can never disagree.
    /// </summary>
    public static class DoorMath
    {
        /// <summary>
        /// Index of the ball a trap-door tap would grab: nearest the door mouth by EDGE distance
        /// (centre distance minus radius — a big ball whose surface reaches nearer wins), among balls
        /// still above the door (y ≥ doorY, y-up). −1 when none qualifies.
        /// </summary>
        public static int NearestDoorBall(IReadOnlyList<DoorCandidate> balls, double mouthX, double doorY)
        {
            int best = -1;
            double bestDist = double.PositiveInfinity;
            for (int i = 0; i < balls.Count; i++)
            {
                var b = balls[i];
                if (b.Y < doorY) continue;
                double dx = b.X - mouthX;
                double dy = b.Y - doorY;
                double dist = Math.Sqrt(dx * dx + dy * dy) - b.Radius;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The lit marker index at an elapsed sweep time: steps edge→edge and back (ping-pong).</summary>
        public static int SweepIndex(double elapsedMs, double stepMs, int positions)
        {
            if (positions <= 1) return 0;
            int cycle = 2 * (positions - 1);
            int step = (int)Math.Floor(elapsedMs / stepMs) % cycle;
            return step < positions ? step : cycle - step;
        }

        /// <summary>X of marker <paramref name="i"/>, evenly spaced across [minX, maxX].</summary>
        public static double SweepPositionX(int i, int positions, double minX, double maxX) =>
            positions <= 1 ? (minX + maxX) / 2 : minX + (maxX - minX) * i / (positions - 1);

        /// <summary>
        /// Fan angle (radians, 0 = straight along the local axis) of copy <paramref name="i"/> of
        /// <paramref name="multiplier"/>: copies spread symmetrically across ±spread/2; a lone copy goes straight.
        /// </summary>
        public static double SplitFanAngle(int i, int multiplier, double spread)
        {
            if (multiplier <= 1) return 0;
            double t = i / (double)(multiplier - 1) - 0.5;
            return t * spread;
        }
    }
}
