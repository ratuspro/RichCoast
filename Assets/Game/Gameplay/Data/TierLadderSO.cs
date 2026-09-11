using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Inspector-editable tier size/physics tables. Produces the engine-free
    /// <see cref="TierLadder"/> the systems compute with (so the same math is unit-tested).
    /// Lengths are DESIGN PIXELS (see <see cref="DesignSpace"/>); the Game layer converts.
    /// </summary>
    [CreateAssetMenu(menuName = "RichCoast/Tier Ladder", fileName = "TierLadder")]
    public sealed class TierLadderSO : ScriptableObject
    {
        [Header("Size (design px)")]
        [Tooltip("Ball radius per tier for the base table; index = tier-1. Beyond it radii grow by Radius Growth.")]
        public double[] radii = { 17, 22, 28, 34, 41, 50, 60, 71, 84, 99 };
        [Tooltip("Per-tier radius multiplier beyond the base table.")]
        public double radiusGrowth = 1.18;

        [Header("Friction ramp (× material multiplier)")]
        public double frictionBase = 0.4;
        public double frictionStep = 0.025;
        public double frictionMax = 0.5;

        [Header("Density taper")]
        public double density = 0.02;
        public int densityTaperTier = 8;
        public double densityMassExp = 1;

        [Header("Bounce (× material multiplier)")]
        public double restitution = 0.2;

        [Header("Milestones")]
        public double tailMilestoneZoom = 1.2;

        public TierLadder ToLadder() => new TierLadder
        {
            Radii = (double[])radii.Clone(),
            RadiusGrowth = radiusGrowth,
            FrictionBase = frictionBase,
            FrictionStep = frictionStep,
            FrictionMax = frictionMax,
            Density = density,
            DensityTaperTier = densityTaperTier,
            DensityMassExp = densityMassExp,
            Restitution = restitution,
            TailMilestoneZoom = tailMilestoneZoom,
        };
    }
}
