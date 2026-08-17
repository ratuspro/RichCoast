using System.Collections.Generic;
using RichCoast.Core;

namespace RichCoast.Data
{
    /// <summary>
    /// The two authored Zone B playfields, ported verbatim from <c>zoneB/zoneLayout.ts</c>.
    ///
    /// Both are stacked horizontal multiplier shelves split by vertical and diagonal guide rails,
    /// funnelling into a single bottom collector. One is picked at random per run (including every
    /// restart). Multipliers are tuned to ≤4 so cascades stay balanced.
    ///
    /// All y values are absolute in the 390×1238 design world; Zone B spans y=551..1238 and the
    /// cascade is authored for the 607..1238 band — the extra headroom above the first shelf is
    /// deliberate free-fall room, not an unremapped leftover.
    /// </summary>
    public static class DefaultZoneBLayouts
    {
        /// <summary>The two funnel ramps that feed the single bottom collector — shared by both layouts.</summary>
        private static IEnumerable<WallDef> FunnelRamps()
        {
            yield return new WallDef(0f, 1129f, 110f, 1233f, fillBelow: true);
            yield return new WallDef(390f, 1129f, 280f, 1233f, fillBelow: true);
        }

        /// <summary>
        /// The drain sits right at the score bar: its top is one ball-radius above the bar, so a
        /// ball vanishes just as its bottom meets the bar rather than floating above it.
        /// </summary>
        private static readonly CollectorDef BottomCollector = new CollectorDef(110f, 1212f, 170f, 26f, 1f);

        /// <summary>Three-segment top row narrowing to a central bottom gate.</summary>
        public static ZoneBLayout Layout1()
        {
            var gates = new[]
            {
                // Row 1 — gaps at x∈[100,116] and x∈[248,264], each holding a divider post below.
                GateDef.StaticGate(54f, 720f, 0f, 92f, 4),
                GateDef.StaticGate(182f, 720f, 0f, 132f, 3),
                GateDef.StaticGate(323f, 720f, 0f, 118f, 2),
                // Row 2
                GateDef.StaticGate(45f, 901f, 0f, 85f, 2),
                GateDef.StaticGate(195f, 901f, 0f, 110f, 2),
                GateDef.StaticGate(348f, 901f, 0f, 80f, 2),
                // Row 3
                GateDef.StaticGate(195f, 1051f, 0f, 90f, 2),
            };

            var walls = new List<WallDef>
            {
                // Row-1 gate dividers, centred in the gaps and framing the three top gates.
                new WallDef(108f, 715f, 108f, 810f),
                new WallDef(256f, 715f, 256f, 810f),
                // Frame posts rising above the right gate.
                new WallDef(264f, 677f, 264f, 720f),
                new WallDef(382f, 677f, 382f, 720f),
                // Left outer rail: down the left side, then a diagonal converging to centre.
                new WallDef(75f, 752f, 75f, 921f),
                new WallDef(75f, 921f, 165f, 1051f),
                // Right outer rail: mirror of the left.
                new WallDef(320f, 752f, 320f, 921f),
                new WallDef(320f, 921f, 230f, 1051f),
                // Post above the row-2 centre gate.
                new WallDef(140f, 854f, 140f, 901f),
                // Small end caps on the row-3 centre gate.
                new WallDef(150f, 1051f, 150f, 1079f),
                new WallDef(240f, 1051f, 240f, 1079f),
            };
            walls.AddRange(FunnelRamps());

            return new ZoneBLayout(gates, new[] { BottomCollector }, walls.ToArray());
        }

        /// <summary>Offset rows with zig-zag diagonal rails.</summary>
        public static ZoneBLayout Layout2()
        {
            var gates = new[]
            {
                // Row 1 — gaps at x∈[195,211] and x∈[293,309].
                GateDef.StaticGate(105f, 720f, 0f, 180f, 4),
                GateDef.StaticGate(252f, 720f, 0f, 82f, 3),
                GateDef.StaticGate(348f, 720f, 0f, 78f, 4),
                // Row 2
                GateDef.StaticGate(95f, 908f, 0f, 170f, 4),
                GateDef.StaticGate(250f, 908f, 0f, 85f, 2),
                GateDef.StaticGate(345f, 908f, 0f, 85f, 3),
                // Row 3
                GateDef.StaticGate(100f, 1051f, 0f, 120f, 2),
                GateDef.StaticGate(300f, 1051f, 0f, 140f, 3),
            };

            var walls = new List<WallDef>
            {
                // Row-1 gate dividers, rising slightly above the bars.
                new WallDef(203f, 681f, 203f, 752f),
                new WallDef(301f, 681f, 301f, 752f),
                // Upper diagonal off the left divider, down-left toward the row-2 left gate.
                new WallDef(203f, 752f, 150f, 869f),
                // Tall right rail: straight down past row 2 into row 3, forming the central channel.
                new WallDef(301f, 752f, 301f, 1038f),
                // Lower zig-zag from below the row-2 centre gate to the row-3 right gate.
                new WallDef(207f, 921f, 250f, 1038f),
                // Short vertical cap on the far left of row 3.
                new WallDef(40f, 980f, 40f, 1053f),
            };
            walls.AddRange(FunnelRamps());

            return new ZoneBLayout(gates, new[] { BottomCollector }, walls.ToArray());
        }

        public static ZoneBLayout[] All() => new[] { Layout1(), Layout2() };

        /// <summary>Pick a layout for a run. Uniform-random, once per run, as the original did.</summary>
        public static ZoneBLayout PickRandom(System.Random rng)
        {
            var all = All();
            return all[rng.Next(all.Length)];
        }
    }
}
