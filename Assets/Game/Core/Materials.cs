using System;

namespace RichCoast.Core
{
    public enum MaterialFamily { Primitive, Metal, Precious, Gem, Exotic }

    /// <summary>Which detail pass the ball painter runs for this material (full LOD only).</summary>
    public enum MaterialDetail
    {
        Grain, Speckle, Gloss, Matte, Sheen, Rivets, Glint, Crescent, Facets, Glow, Crust, Specks, Corona,
    }

    /// <summary>The "subtle feel" band: multipliers on each zone's own tuned physics constants.</summary>
    public readonly struct MaterialPhysics
    {
        public readonly double RestitutionMult;
        public readonly double FrictionMult;
        public readonly double DensityMult;

        public MaterialPhysics(double restitutionMult, double frictionMult, double densityMult)
        {
            RestitutionMult = restitutionMult;
            FrictionMult = frictionMult;
            DensityMult = densityMult;
        }

        public MaterialPhysics With(double? restitution = null, double? friction = null, double? density = null) =>
            new MaterialPhysics(restitution ?? RestitutionMult, friction ?? FrictionMult, density ?? DensityMult);
    }

    public sealed class MaterialDef
    {
        public readonly string Name;
        public readonly MaterialFamily Family;
        /// <summary>Dominant fill colour, 0xRRGGBB — the identity colour at any size.</summary>
        public readonly uint BaseColor;
        /// <summary>Secondary recipe colour: grain, speckle, sheen, facet or glow.</summary>
        public readonly uint AccentColor;
        public readonly MaterialDetail Detail;
        public readonly MaterialPhysics Physics;

        public MaterialDef(string name, MaterialFamily family, uint baseColor, uint accentColor, MaterialDetail detail, MaterialPhysics physics)
        {
            Name = name;
            Family = family;
            BaseColor = baseColor;
            AccentColor = accentColor;
            Detail = detail;
            Physics = physics;
        }
    }

    /// <summary>A material resolved for a tier: the def plus how many times the ladder wrapped.</summary>
    public readonly struct TierMaterial
    {
        public readonly MaterialDef Def;
        /// <summary>Completed trips around the ladder: 0 for tiers 1–20, 1 for 21–40, …</summary>
        public readonly int Cycle;

        public TierMaterial(MaterialDef def, int cycle)
        {
            Def = def;
            Cycle = cycle;
        }
    }

    /// <summary>
    /// Single source of truth for the ball MATERIAL ladder. 20 materials in 5 families of 4,
    /// each family aligned with one 4-tier draw window ([1,4] Primitives → [5,8] Metals →
    /// [9,12] Precious → [13,16] Gems → [17,20] Exotic). Tiers past 20 wrap (one gold ring per
    /// cycle in the painter). Ported verbatim from the Phaser <c>Materials.ts</c>.
    /// </summary>
    public static class Materials
    {
        static readonly MaterialPhysics Primitive = new MaterialPhysics(0.9, 1.1, 1.0);
        static readonly MaterialPhysics Metal = new MaterialPhysics(0.8, 0.75, 1.15);
        static readonly MaterialPhysics Precious = new MaterialPhysics(0.9, 0.8, 1.2);
        static readonly MaterialPhysics Gem = new MaterialPhysics(1.2, 0.5, 1.1);
        static readonly MaterialPhysics Exotic = new MaterialPhysics(1.5, 0.6, 0.9);

        public static readonly MaterialDef[] Ladder =
        {
            // — Primitives [1,4] —
            new MaterialDef("Wood", MaterialFamily.Primitive, 0xa9713f, 0x7a4e2a, MaterialDetail.Grain, Primitive.With(restitution: 1.2, density: 0.85)),
            new MaterialDef("Stone", MaterialFamily.Primitive, 0x94a1b0, 0x6d7885, MaterialDetail.Speckle, Primitive),
            new MaterialDef("Turquoise", MaterialFamily.Primitive, 0x2fb3a4, 0x3d5049, MaterialDetail.Crust, Primitive),
            new MaterialDef("Clay", MaterialFamily.Primitive, 0xc2503a, 0x9c3a2a, MaterialDetail.Matte, Primitive),
            // — Metals [5,8] —
            new MaterialDef("Copper", MaterialFamily.Metal, 0xd47b3c, 0xf2b27a, MaterialDetail.Sheen, Metal),
            new MaterialDef("Iron", MaterialFamily.Metal, 0x5d6b7a, 0x3f4a56, MaterialDetail.Rivets, Metal),
            new MaterialDef("Steel", MaterialFamily.Metal, 0x6e8fb5, 0xa9c6e8, MaterialDetail.Sheen, Metal),
            new MaterialDef("Silver", MaterialFamily.Metal, 0xc8d2dc, 0x8fa0ad, MaterialDetail.Sheen, Metal),
            // — Precious [9,12] —
            new MaterialDef("Gold", MaterialFamily.Precious, 0xf2b024, 0xffe08a, MaterialDetail.Sheen, Precious.With(density: 1.3)),
            new MaterialDef("Rose gold", MaterialFamily.Precious, 0xe08a78, 0xf7c4b5, MaterialDetail.Sheen, Precious),
            new MaterialDef("Obsidian", MaterialFamily.Precious, 0x332e3b, 0x8a6ff0, MaterialDetail.Glint, Precious),
            new MaterialDef("Glass", MaterialFamily.Precious, 0xbfe8f0, 0xffffff, MaterialDetail.Crescent, Gem), // slips like a gem
            // — Gems [13,16] —
            new MaterialDef("Sapphire", MaterialFamily.Gem, 0x2f6fd0, 0x7fa8ef, MaterialDetail.Facets, Gem),
            new MaterialDef("Emerald", MaterialFamily.Gem, 0x2fae66, 0x7fdca8, MaterialDetail.Facets, Gem),
            new MaterialDef("Ruby", MaterialFamily.Gem, 0xd63a56, 0xf291a5, MaterialDetail.Facets, Gem),
            new MaterialDef("Diamond", MaterialFamily.Gem, 0xdff3fa, 0x9fd0e8, MaterialDetail.Facets, Gem),
            // — Exotic [17,20] —
            new MaterialDef("Plasma", MaterialFamily.Exotic, 0x3ee0d8, 0xd8fffb, MaterialDetail.Glow, Exotic),
            new MaterialDef("Magma", MaterialFamily.Exotic, 0xff5a2d, 0x3a1f16, MaterialDetail.Crust, Exotic),
            new MaterialDef("Void", MaterialFamily.Exotic, 0x4a3f8f, 0xc9c2ff, MaterialDetail.Specks, Exotic),
            new MaterialDef("Antimatter", MaterialFamily.Exotic, 0xe94fe0, 0xffffff, MaterialDetail.Corona, Exotic),
        };

        /// <summary>Length of the authored ladder. NOT a gameplay ceiling — tiers wrap past it.</summary>
        public static int Count => Ladder.Length;

        /// <summary>Material for a tier (1-based), wrapping past the ladder end. Tiers &lt; 1 clamp to 1.</summary>
        public static TierMaterial ForTier(int tier)
        {
            int t = Math.Max(1, tier) - 1;
            return new TierMaterial(Ladder[t % Count], t / Count);
        }

        /// <summary>Identity colour for a tier (0xRRGGBB).</summary>
        public static uint ColorForTier(int tier) => ForTier(tier).Def.BaseColor;
    }
}
