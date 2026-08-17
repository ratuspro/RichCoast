using System.Collections.Generic;
using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>
    /// Pure Zone A math — no scene, no state. Turns <see cref="Tuning"/> and the
    /// <see cref="TierTable"/> into the per-tier and per-frame values the systems need, so the
    /// fiddly arithmetic is unit-tested in isolation. Ported from <c>zoneA/ballMath.ts</c>.
    ///
    /// Positions here are design-space (y grows downward), matching <see cref="Layout"/>.
    /// </summary>
    public static class BallMath
    {
        /// <summary>
        /// Arena growth per TAIL milestone: a flat factor, deliberately BELOW the neutral match
        /// for a <see cref="Progression.TailWindowStep"/> shift (≈1.39), so apparent ball size
        /// creeps up ~16% per tail milestone — the endgame's mounting board squeeze.
        /// </summary>
        public const float TailMilestoneZoom = 1.2f;

        /// <summary>
        /// Arena growth factor for a milestone whose draw window's MAX tier moved
        /// old → new. Growing the arena by exactly this keeps the window-max ball's apparent
        /// on-screen size constant (the camera zoom is 1/scale), so a per-milestone
        /// <c>tightness</c> multiplier applied on top is the precise change in worst-case
        /// headroom (&lt;1 = tighter/harder, &gt;1 = roomier). An unshifted window yields 1.
        /// </summary>
        public static float NeutralGrowth(TierTable table, int oldMaxTier, int newMaxTier) =>
            table.RadiusForTier(newMaxTier) / table.RadiusForTier(oldMaxTier);

        /// <summary>
        /// Arena zoom-out factor owed by ONE level-up: neutral growth × tightness when
        /// <paramref name="level"/> is a shifted-window milestone, else exactly 1. Tail milestones
        /// ignore the neutral match and grow by the flat <see cref="TailMilestoneZoom"/>.
        ///
        /// Because a shifted window always zooms (radii strictly increase and tightness never
        /// inverts that), 1 doubles as the "no zoom" sentinel, and factors from a multi-level
        /// score-bar roll-through compose by product — so a burst that overshoots a milestone
        /// level still carries its zoom.
        /// </summary>
        public static float MilestoneZoomFactor(
            TierTable table,
            int level,
            TierWindow prevWindow,
            TierWindow window,
            float tightness,
            bool tail = false)
        {
            if (level % Progression.MilestoneEvery != 0) return 1f;
            var shifted = window.Min != prevWindow.Min || window.Max != prevWindow.Max;
            if (!shifted) return 1f;
            if (tail) return TailMilestoneZoom;
            return NeutralGrowth(table, prevWindow.Max, window.Max) * tightness;
        }

        /// <summary>
        /// Surface friction for a tier: the size ramp (grows with tier, clamped to
        /// <see cref="Tuning.FrictionMax"/>) shaped by the tier's material feel — metals slide,
        /// gems slip.
        /// </summary>
        public static float FrictionForTier(TierTable table, int tier)
        {
            var clamped = Mathf.Clamp(tier, 1, Tiers.TierCount);
            var raw = Tuning.FrictionBase + Tuning.FrictionStep * (clamped - 1);
            return Mathf.Min(raw, Tuning.FrictionMax) * table.MaterialForTier(tier).Def.Physics.FrictionMult;
        }

        /// <summary>
        /// Density (before the material multiplier) for a tier. Small tiers keep the flat
        /// <see cref="Tuning.Density"/>; larger balls taper so that mass (∝ density·radius²) grows
        /// like radius^<see cref="Tuning.DensityMassExp"/> instead of radius² — keeping big-ball
        /// collision momentum in check so late-milestone shoves stay gentle. The taper only ever
        /// reduces density, never raises it.
        /// </summary>
        public static float DensityForTier(TierTable table, int tier)
        {
            if (tier <= Tuning.DensityTaperTier) return Tuning.Density;
            var ratio = table.RadiusForTier(Tuning.DensityTaperTier) / table.RadiusForTier(tier); // ≤ 1
            return Tuning.Density * Mathf.Pow(ratio, 2f - Tuning.DensityMassExp);
        }

        /// <summary>Clamp a spawn x so a ball of <paramref name="radius"/> stays fully within [minX, maxX].</summary>
        public static float ClampSpawnX(float x, float radius, float minX, float maxX) =>
            Mathf.Clamp(x, minX + radius, maxX - radius);

        /// <summary>Midpoint of two points — where a merged ball is born.</summary>
        public static Vector2 Midpoint(Vector2 a, Vector2 b) => (a + b) * 0.5f;

        /// <summary>
        /// Outward velocity kick applied to <paramref name="target"/> by a blast at
        /// <paramref name="origin"/>. Zero when the target sits exactly on the origin or at/beyond
        /// <paramref name="radius"/>; otherwise it points away with linear falloff.
        /// </summary>
        public static Vector2 BlastImpulse(Vector2 target, Vector2 origin, float radius, float strength)
        {
            var delta = target - origin;
            var dist = delta.magnitude;
            if (dist <= 0f || dist >= radius) return Vector2.zero;
            return delta / dist * (strength * (1f - dist / radius));
        }

        /// <summary>
        /// Cap a velocity's magnitude, preserving direction. The anti-tunnel backstop: the board
        /// clamps every ball's speed each step, so no single-step displacement can exceed a wall's
        /// thickness.
        /// </summary>
        public static Vector2 ClampSpeed(Vector2 v, float maxSpeed)
        {
            var speed = v.magnitude;
            if (speed <= maxSpeed || speed <= 0f) return v;
            return v * (maxSpeed / speed);
        }

        /// <summary>Accumulate rest time: previous + delta while resting, else reset to 0.</summary>
        public static float NextRestMs(float prev, float deltaMs, bool resting) => resting ? prev + deltaMs : 0f;

        /// <summary>A ball "rests above the line" when its centre is above lineY AND it is slow.</summary>
        public static bool IsRestingAbove(float centerY, float speed, float lineY, float speedThreshold) =>
            centerY < lineY && speed < speedThreshold;

        /// <summary>Overflow (game over) once accumulated rest time reaches the threshold.</summary>
        public static bool IsOverflow(float restMs, float thresholdMs) => restMs >= thresholdMs;

        /// <summary>
        /// A slow ball whose centre sits just below the line — inside the warning band
        /// [lineY, lineY + band) — but not yet over it. Drives the red death-line warning.
        /// </summary>
        public static bool IsNearDeath(float centerY, float speed, float lineY, float band, float speedThreshold) =>
            speed < speedThreshold && centerY >= lineY && centerY < lineY + band;

        /// <summary>
        /// Minimal view of a ball for trap-door target selection. Real bodies and test fakes both
        /// satisfy it, so the pick can be unit-tested.
        /// </summary>
        public interface IDoorCandidate
        {
            Vector2 Position { get; }
            float Radius { get; }
        }

        /// <summary>
        /// The ball a trap-door tap would grab: nearest the door mouth by EDGE distance
        /// (centre-to-mouth minus radius, so a bigger ball whose edge reaches nearer wins), among
        /// candidates still above the door. Null when none qualifies.
        ///
        /// Shared by Zone C's grab and Zone A's candidate highlight — one source of truth, so the
        /// two can never disagree about which ball is next.
        /// </summary>
        public static T NearestDoorBall<T>(IEnumerable<T> candidates, float mouthX, float doorY)
            where T : class, IDoorCandidate
        {
            T best = null;
            var bestDist = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                var pos = candidate.Position;
                if (pos.y > doorY) continue;
                var dist = new Vector2(pos.x - mouthX, pos.y - doorY).magnitude - candidate.Radius;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
