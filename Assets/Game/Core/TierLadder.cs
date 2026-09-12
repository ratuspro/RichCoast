using System;

namespace RichCoast.Core
{
    /// <summary>
    /// The per-tier size/physics tables (a port of the Phaser <c>tuning.ts</c> + the tier-indexed
    /// half of <c>ballMath.ts</c>). Plain data + pure functions, engine-free: the Game layer's
    /// <c>TierLadderSO</c> authors one of these and hands it to the systems, so the same code is
    /// unit-tested here and live-tuned in the inspector.
    ///
    /// All lengths are in DESIGN PIXELS (the 390-wide portrait design space the tables were
    /// balanced in); the Game layer converts to world units with <see cref="DesignSpace"/>.
    /// </summary>
    public sealed class TierLadder
    {
        /// <summary>Ball radius per tier for the base table; index = tier-1. Beyond it radii grow by <see cref="RadiusGrowth"/>.</summary>
        public double[] Radii = { 17, 22, 28, 34, 41, 50, 60, 71, 84, 99 };

        /// <summary>Per-tier radius multiplier beyond the base table (≈ the table's own top step).</summary>
        public double RadiusGrowth = 1.18;

        /// <summary>Surface friction grows with tier: Base + Step*(tier-1), clamped at Max.</summary>
        public double FrictionBase = 0.4;
        public double FrictionStep = 0.025;
        public double FrictionMax = 0.5;

        /// <summary>Base density for the small tiers; larger balls taper (see <see cref="DensityForTier"/>).</summary>
        public double Density = 0.02;
        /// <summary>Tiers at or below this keep the flat Density; larger tiers taper.</summary>
        public int DensityTaperTier = 8;
        /// <summary>Above the taper tier, mass grows like radius^this (1 = linear in radius).</summary>
        public double DensityMassExp = 1;

        /// <summary>Base restitution (bounciness), before the material multiplier.</summary>
        public double Restitution = 0.2;

        /// <summary>Arena growth per TAIL milestone: flat, deliberately below the neutral match.</summary>
        public double TailMilestoneZoom = 1.2;

        public static TierLadder Default => new TierLadder();

        /// <summary>
        /// Visual/physics radius for a tier (1-based). Within the table it's read directly; past
        /// the table it keeps growing geometrically (no clamp). Tiers below 1 clamp to the smallest.
        /// </summary>
        public double RadiusForTier(int tier)
        {
            if (tier <= Radii.Length) return Radii[Math.Max(1, tier) - 1];
            return Radii[Radii.Length - 1] * Math.Pow(RadiusGrowth, tier - Radii.Length);
        }

        /// <summary>
        /// Arena growth factor for a milestone whose draw window's max tier moved old → new. Growing
        /// by exactly this keeps the window-max ball's apparent size constant. Unshifted → 1.
        /// </summary>
        public double NeutralGrowth(int oldMaxTier, int newMaxTier) =>
            RadiusForTier(newMaxTier) / RadiusForTier(oldMaxTier);

        /// <summary>
        /// Arena zoom-out factor owed by ONE level-up: NeutralGrowth × tightness on a shifted-window
        /// milestone, TailMilestoneZoom on a tail milestone, else exactly 1 (the "no zoom" sentinel —
        /// factors from a multi-level roll-through compose by product).
        /// </summary>
        public double MilestoneZoomFactor(int level, (int min, int max) prevWindow, (int min, int max) window, double? tightness, bool tail = false)
        {
            if (level % ProgressionCurve.MilestoneEvery != 0) return 1;
            bool shifted = window.min != prevWindow.min || window.max != prevWindow.max;
            if (!shifted) return 1;
            if (tail) return TailMilestoneZoom;
            return NeutralGrowth(prevWindow.max, window.max) * (tightness ?? 1);
        }

        /// <summary>Surface friction for a tier: the clamped size ramp shaped by the material's feel.</summary>
        public double FrictionForTier(int tier)
        {
            double raw = FrictionBase + FrictionStep * (Math.Max(1, tier) - 1);
            return Math.Min(raw, FrictionMax) * Materials.ForTier(tier).Def.Physics.FrictionMult;
        }

        /// <summary>
        /// Density (before the material DensityMult) for a tier. Small tiers keep the flat Density;
        /// larger balls taper so mass (∝ density·r²) grows like r^DensityMassExp — keeping big-ball
        /// collision momentum in check. Only ever reduces density.
        /// </summary>
        public double DensityForTier(int tier)
        {
            if (tier <= DensityTaperTier) return Density;
            double ratio = RadiusForTier(DensityTaperTier) / RadiusForTier(tier); // ≤ 1
            return Density * Math.Pow(ratio, 2 - DensityMassExp);
        }

        /// <summary>Restitution for a tier: base × the material's bounce multiplier.</summary>
        public double RestitutionForTier(int tier) => Restitution * Materials.ForTier(tier).Def.Physics.RestitutionMult;
    }

    /// <summary>
    /// The portrait design space every Core table is authored in: 390×844 design px, with the
    /// Zone A board band on top. The Game layer maps design px → world units through
    /// <see cref="UnitsPerPixel"/> (a 10-unit-wide board).
    /// </summary>
    public static class DesignSpace
    {
        public const double Width = 390;
        public const double Height = 844;

        /// <summary>HUD chrome height at the top of the design screen.</summary>
        public const double HudHeight = 42;
        /// <summary>Zone A board band height below the HUD (the merge tray).</summary>
        public const double BoardHeight = 465;

        /// <summary>The row the aim ball sits on (design px from the band top).</summary>
        public const double SpawnY = 78;
        /// <summary>Overflow line, just below the spawn row (design px from the band top).</summary>
        public const double DeathLineY = 108;
        /// <summary>Px below the death line within which a resting ball flags the warning.</summary>
        public const double WarnBand = 28;

        /// <summary>Zone C trap-door band height, directly under Zone A's funnel apex.</summary>
        public const double ZoneCHeight = 44;
        /// <summary>Zone B split-arena band height, directly under Zone C.</summary>
        public const double ZoneBHeight = 687;
        /// <summary>
        /// How far the camera scrolls between the A and B framings: the world is taller than the
        /// screen by this much (HUD + board + C + B − screen), so the B-phase brings Zone B's
        /// bottom edge flush with the screen bottom.
        /// </summary>
        public const double PanDistance = HudHeight + BoardHeight + ZoneCHeight + ZoneBHeight - Height; // 394

        /// <summary>Zone B balls are one fixed size (radius, design px) regardless of tier.</summary>
        public const double ZoneBBallRadius = 10;
        /// <summary>Inset of the trap-door sweep from each Zone B edge (~one ball radius + wall slack).</summary>
        public const double SweepMargin = 18;
        /// <summary>Evenly-spaced positions the lit trap-door marker steps between.</summary>
        public const int SweepPositions = 9;

        /// <summary>World units per design pixel: the 390px board is 10 world units wide.</summary>
        public const double UnitsPerPixel = 10.0 / Width;

        public static double ToUnits(double designPx) => designPx * UnitsPerPixel;
        public static double ToPixels(double units) => units / UnitsPerPixel;
    }
}
