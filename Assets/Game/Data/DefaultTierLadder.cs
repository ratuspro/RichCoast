using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Data
{
    /// <summary>
    /// The authored ball ladder — the single source of truth both the ScriptableObject and the
    /// tests seed from, so neither can drift from the other.
    ///
    /// "Bright Workshop / industrial materials": each tier IS a physical material — the higher the
    /// tier, the more valuable the stuff. 20 materials in 5 families of 4, each family aligned
    /// with one 4-tier draw window ([1,4] Primitives → [5,8] Metals → [9,12] Precious →
    /// [13,16] Gems → [17,20] Exotic), so a window shift reads as advancing to the next material
    /// age. Tiers past 20 wrap, with one ring added per completed cycle to keep them distinct.
    ///
    /// Any 4 consecutive tiers must stay mutually distinguishable at Zone B's tiny ball size,
    /// where colour is the only identity — hue/value spacing inside each family matters more than
    /// realism. Ported from <c>core/Materials.ts</c>.
    /// </summary>
    public static class DefaultTierLadder
    {
        // Family-level feel. A few materials override.
        private static readonly MaterialPhysics Primitive = new MaterialPhysics(0.9f, 1.1f, 1.0f);
        private static readonly MaterialPhysics Metal = new MaterialPhysics(0.8f, 0.75f, 1.15f);
        private static readonly MaterialPhysics Precious = new MaterialPhysics(0.9f, 0.8f, 1.2f);
        private static readonly MaterialPhysics Gem = new MaterialPhysics(1.2f, 0.5f, 1.1f);
        private static readonly MaterialPhysics Exotic = new MaterialPhysics(1.5f, 0.6f, 0.9f);

        /// <summary>Build the authored ladder. A fresh array each call, so callers may edit theirs.</summary>
        public static MaterialDef[] CreateMaterials() => new[]
        {
            // — Primitives [1,4] —
            Def("Wood", MaterialFamily.Primitive, 0xa9713f, 0x7a4e2a, MaterialDetail.Grain, new MaterialPhysics(1.2f, 1.1f, 0.85f)),
            Def("Stone", MaterialFamily.Primitive, 0x94a1b0, 0x6d7885, MaterialDetail.Speckle, Primitive),
            // Turquoise (not the spec's Amber): every warm slot is taken by a merge-reachable
            // neighbour (Wood brown, Clay red, Copper orange, Gold yellow), so an amber ball is
            // ambiguous at Zone B size. Teal is unclaimed until Plasma (tier 17), which can never
            // share a board with tier 3.
            Def("Turquoise", MaterialFamily.Primitive, 0x2fb3a4, 0x3d5049, MaterialDetail.Crust, Primitive),
            Def("Clay", MaterialFamily.Primitive, 0xc2503a, 0x9c3a2a, MaterialDetail.Matte, Primitive),

            // — Metals [5,8] —
            Def("Copper", MaterialFamily.Metal, 0xd47b3c, 0xf2b27a, MaterialDetail.Sheen, Metal),
            Def("Iron", MaterialFamily.Metal, 0x5d6b7a, 0x3f4a56, MaterialDetail.Rivets, Metal),
            Def("Steel", MaterialFamily.Metal, 0x6e8fb5, 0xa9c6e8, MaterialDetail.Sheen, Metal),
            Def("Silver", MaterialFamily.Metal, 0xc8d2dc, 0x8fa0ad, MaterialDetail.Sheen, Metal),

            // — Precious [9,12] —
            Def("Gold", MaterialFamily.Precious, 0xf2b024, 0xffe08a, MaterialDetail.Sheen, new MaterialPhysics(0.9f, 0.8f, 1.3f)),
            Def("Rose gold", MaterialFamily.Precious, 0xe08a78, 0xf7c4b5, MaterialDetail.Sheen, Precious),
            Def("Obsidian", MaterialFamily.Precious, 0x332e3b, 0x8a6ff0, MaterialDetail.Glint, Precious),
            Def("Glass", MaterialFamily.Precious, 0xbfe8f0, 0xffffff, MaterialDetail.Crescent, Gem), // slips like a gem

            // — Gems [13,16] —
            Def("Sapphire", MaterialFamily.Gem, 0x2f6fd0, 0x7fa8ef, MaterialDetail.Facets, Gem),
            Def("Emerald", MaterialFamily.Gem, 0x2fae66, 0x7fdca8, MaterialDetail.Facets, Gem),
            Def("Ruby", MaterialFamily.Gem, 0xd63a56, 0xf291a5, MaterialDetail.Facets, Gem),
            Def("Diamond", MaterialFamily.Gem, 0xdff3fa, 0x9fd0e8, MaterialDetail.Facets, Gem),

            // — Exotic [17,20] —
            Def("Plasma", MaterialFamily.Exotic, 0x3ee0d8, 0xd8fffb, MaterialDetail.Glow, Exotic),
            Def("Magma", MaterialFamily.Exotic, 0xff5a2d, 0x3a1f16, MaterialDetail.Crust, Exotic),
            Def("Void", MaterialFamily.Exotic, 0x4a3f8f, 0xc9c2ff, MaterialDetail.Specks, Exotic),
            Def("Antimatter", MaterialFamily.Exotic, 0xe94fe0, 0xffffff, MaterialDetail.Corona, Exotic),
        };

        /// <summary>The authored table: base radii from <see cref="Tuning"/> plus the ladder above.</summary>
        public static TierTable CreateTable() =>
            new TierTable((float[])Tuning.Radii.Clone(), Tuning.RadiusGrowth, CreateMaterials());

        private static MaterialDef Def(string name, MaterialFamily family, int baseColor, int accentColor, MaterialDetail detail, MaterialPhysics physics) =>
            new MaterialDef
            {
                Name = name,
                Family = family,
                BaseColor = FromHex(baseColor),
                AccentColor = FromHex(accentColor),
                Detail = detail,
                Physics = physics,
            };

        /// <summary>0xRRGGBB → opaque Color, keeping the authored hex values readable above.</summary>
        public static Color FromHex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}
