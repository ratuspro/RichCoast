using System;
using UnityEngine;

namespace RichCoast.Core
{
    public enum MaterialFamily
    {
        Primitive,
        Metal,
        Precious,
        Gem,
        Exotic,
    }

    /// <summary>
    /// Which detail pass the full-LOD (Zone A) ball look runs. The small-LOD (Zone B) look skips
    /// details — colour is the identity at that size. Consumed only by the view layer.
    /// </summary>
    public enum MaterialDetail
    {
        Grain,     // curved wood-grain strokes
        Speckle,   // scattered stone dots
        Gloss,     // big soft resin highlight
        Matte,     // faint horizontal throw-lines
        Sheen,     // diagonal brushed-metal band
        Rivets,    // dot ring just inside the rim
        Glint,     // thin bright edge arc
        Crescent,  // glassy white crescent
        Facets,    // flat gem wedges
        Glow,      // emissive bright core
        Crust,     // dark cracks over a glowing fill
        Specks,    // tiny stars
        Corona,    // white core, coloured halo
    }

    /// <summary>
    /// The "subtle feel" band: multipliers applied on top of each zone's tuned constants at
    /// spawn. Narrow enough that no layout or balance retuning is needed — wood bounces a touch,
    /// metal thuds, gems slip.
    /// </summary>
    [Serializable]
    public struct MaterialPhysics
    {
        public float RestitutionMult;
        public float FrictionMult;

        /// <summary>Zone A only — Zone B leaves ball mass alone.</summary>
        public float DensityMult;

        public MaterialPhysics(float restitutionMult, float frictionMult, float densityMult)
        {
            RestitutionMult = restitutionMult;
            FrictionMult = frictionMult;
            DensityMult = densityMult;
        }
    }

    /// <summary>One rung of the material ladder: what a tier IS.</summary>
    [Serializable]
    public struct MaterialDef
    {
        public string Name;
        public MaterialFamily Family;

        /// <summary>Dominant fill colour — the identity colour at any size.</summary>
        public Color BaseColor;

        /// <summary>Secondary recipe colour: grain, speckle, sheen, facet or glow, per family.</summary>
        public Color AccentColor;

        public MaterialDetail Detail;
        public MaterialPhysics Physics;
    }

    /// <summary>A tier's material plus how many times the ladder has wrapped to reach it.</summary>
    public readonly struct TierMaterial
    {
        public readonly MaterialDef Def;

        /// <summary>
        /// Completed trips around the ladder: 0 for tiers 1–20, 1 for 21–40, … The view draws one
        /// gold ring per cycle so a wrapped tier never mimics its ancestor.
        /// </summary>
        public readonly int Cycle;

        public TierMaterial(MaterialDef def, int cycle)
        {
            Def = def;
            Cycle = cycle;
        }
    }

    /// <summary>
    /// The per-tier ball table: base radii plus the material ladder. Authored once in the data
    /// layer and passed into the pure math here, so gameplay code and tests share one source of
    /// truth without either loading an asset.
    ///
    /// Neither table is a gameplay ceiling. Merges are uncapped: radii past the base table grow
    /// geometrically by <see cref="RadiusGrowth"/>, and materials wrap around the ladder.
    /// </summary>
    public sealed class TierTable
    {
        private readonly float[] _radii;
        private readonly MaterialDef[] _materials;

        /// <summary>Per-tier radius multiplier beyond the base table.</summary>
        public readonly float RadiusGrowth;

        public TierTable(float[] radii, float radiusGrowth, MaterialDef[] materials)
        {
            if (radii == null || radii.Length == 0) throw new ArgumentException("radii must be non-empty", nameof(radii));
            if (materials == null || materials.Length == 0) throw new ArgumentException("materials must be non-empty", nameof(materials));
            _radii = radii;
            _materials = materials;
            RadiusGrowth = radiusGrowth;
        }

        /// <summary>Number of entries in the base radius table.</summary>
        public int RadiusTableSize => _radii.Length;

        /// <summary>Length of the authored material ladder. NOT a gameplay ceiling — tiers wrap.</summary>
        public int MaterialCount => _materials.Length;

        /// <summary>
        /// Visual/physics radius for a tier (1-based). Tiers within the base table read it
        /// directly; tiers past it keep growing geometrically with no clamp, so ever-larger balls
        /// render — the expanding arena is what makes room for them. Tiers below 1 clamp to the
        /// smallest.
        /// </summary>
        public float RadiusForTier(int tier)
        {
            if (tier < 1) return _radii[0];
            if (tier <= _radii.Length) return _radii[tier - 1];
            return _radii[_radii.Length - 1] * Mathf.Pow(RadiusGrowth, tier - _radii.Length);
        }

        /// <summary>Material for a tier (1-based), wrapping past the ladder end. Tiers &lt; 1 clamp to 1.</summary>
        public TierMaterial MaterialForTier(int tier)
        {
            var t = Mathf.Max(1, tier) - 1;
            return new TierMaterial(_materials[t % _materials.Length], t / _materials.Length);
        }

        /// <summary>Identity colour for a tier — one table for both zones, so a transferred ball keeps its look.</summary>
        public Color ColorForTier(int tier) => MaterialForTier(tier).Def.BaseColor;
    }
}
