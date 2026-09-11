using System;

namespace RichCoast.Core
{
    /// <summary>
    /// Pure, tier-independent Zone A math (port of the geometric half of <c>ballMath.ts</c>):
    /// spawn clamping, merge midpoints, the neighbour-shoving blast, speed capping, and the
    /// rest / overflow / near-death predicates. Unit-tested in EditMode with no engine types.
    /// </summary>
    public static class BallMath
    {
        /// <summary>Clamp a spawn X so a ball of <paramref name="radius"/> stays fully within [minX, maxX].</summary>
        public static double ClampSpawnX(double x, double radius, double minX, double maxX)
        {
            double lo = minX + radius;
            double hi = maxX - radius;
            if (x < lo) return lo;
            if (x > hi) return hi;
            return x;
        }

        /// <summary>Midpoint of two points — where a merged ball is born.</summary>
        public static Vec2 Midpoint(Vec2 a, Vec2 b) => new Vec2((a.X + b.X) / 2, (a.Y + b.Y) / 2);

        /// <summary>
        /// Outward velocity kick applied to <paramref name="target"/> by a blast at <paramref name="origin"/>.
        /// Zero when the target sits exactly on the origin or at/beyond <paramref name="radius"/>; otherwise
        /// it points away from the origin with linear falloff: magnitude = strength × (1 − dist/radius).
        /// </summary>
        public static Vec2 BlastImpulse(Vec2 target, Vec2 origin, double radius, double strength)
        {
            double dx = target.X - origin.X;
            double dy = target.Y - origin.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist == 0 || dist >= radius) return Vec2.Zero;
            double mag = strength * (1 - dist / radius);
            return new Vec2(dx / dist * mag, dy / dist * mag);
        }

        /// <summary>
        /// Cap a velocity's magnitude at <paramref name="maxSpeed"/>, preserving direction. Below the
        /// cap (or a zero vector) it's returned unchanged — the anti-tunnel backstop.
        /// </summary>
        public static Vec2 ClampSpeed(Vec2 v, double maxSpeed)
        {
            double speed = v.Length;
            if (speed <= maxSpeed || speed == 0) return v;
            return v * (maxSpeed / speed);
        }

        /// <summary>Accumulate rest time: previous + delta while resting, else reset to 0.</summary>
        public static double NextRestMs(double prev, double delta, bool resting) => resting ? prev + delta : 0;

        /// <summary>
        /// A ball "rests above the line" when its centre is above the line AND it's slow. "Above"
        /// means a smaller <paramref name="heightFromTop"/> — both arguments measure distance DOWN
        /// from the band top (design-space convention), so the caller flips y-up world coords first.
        /// </summary>
        public static bool IsRestingAbove(double heightFromTop, double speed, double lineFromTop, double speedThreshold) =>
            heightFromTop < lineFromTop && speed < speedThreshold;

        /// <summary>Overflow (game over) once accumulated rest time reaches the threshold.</summary>
        public static bool IsOverflow(double restMs, double thresholdMs) => restMs >= thresholdMs;

        /// <summary>
        /// A slow ball whose centre sits just below the line — inside the warning band
        /// [line, line + band) — but not yet over it. Drives the red death-line warning.
        /// Same top-down convention as <see cref="IsRestingAbove"/>.
        /// </summary>
        public static bool IsNearDeath(double heightFromTop, double speed, double lineFromTop, double band, double speedThreshold) =>
            speed < speedThreshold && heightFromTop >= lineFromTop && heightFromTop < lineFromTop + band;
    }
}
