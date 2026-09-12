using System;

namespace RichCoast.Core
{
    public enum GateKind { Static, Translating, Rotating }

    /// <summary>
    /// One gate of a Zone B layout, in design px with y measured DOWN from Zone B's top edge.
    /// Static: centre (<see cref="Cx"/>, <see cref="Cy"/>) + angle. Translating: slides between
    /// (Ax, Ay) and (Bx, By) on <see cref="PeriodMs"/>. Rotating: spins about (Cx, Cy) at
    /// <see cref="SpeedRadPerMs"/>.
    /// </summary>
    public sealed class GateDef
    {
        public GateKind Kind;
        public double Cx, Cy;
        /// <summary>Radians; 0 = horizontal.</summary>
        public double Angle;
        public double Length;
        public int Multiplier;
        public double Ax, Ay, Bx, By;
        public double PeriodMs;
        public double SpeedRadPerMs;

        public static GateDef Static(double cx, double cy, double angle, double length, int multiplier) =>
            new GateDef { Kind = GateKind.Static, Cx = cx, Cy = cy, Angle = angle, Length = length, Multiplier = multiplier };

        public static GateDef Translating(double ax, double ay, double bx, double by, double angle, double length, int multiplier, double periodMs) =>
            new GateDef { Kind = GateKind.Translating, Ax = ax, Ay = ay, Bx = bx, By = by, Cx = ax, Cy = ay, Angle = angle, Length = length, Multiplier = multiplier, PeriodMs = periodMs };

        public static GateDef Rotating(double cx, double cy, double length, int multiplier, double speedRadPerMs) =>
            new GateDef { Kind = GateKind.Rotating, Cx = cx, Cy = cy, Length = length, Multiplier = multiplier, SpeedRadPerMs = speedRadPerMs };

        /// <summary>Centre + angle at an elapsed time (static gates never move).</summary>
        public (double x, double y, double angle) PoseAt(double elapsedMs)
        {
            switch (Kind)
            {
                case GateKind.Translating:
                {
                    double t = (Math.Sin(elapsedMs / PeriodMs * Math.PI * 2) + 1) / 2;
                    return (Ax + (Bx - Ax) * t, Ay + (By - Ay) * t, Angle);
                }
                case GateKind.Rotating:
                    return (Cx, Cy, elapsedMs * SpeedRadPerMs);
                default:
                    return (Cx, Cy, Angle);
            }
        }
    }

    /// <summary>A sensor rectangle (design px, y down from Zone B's top) that drains balls into score × <see cref="ScoreMultiplier"/>.</summary>
    public sealed class CollectorDef
    {
        public double X, Y, Width, Height;
        public int ScoreMultiplier = 1;
    }

    /// <summary>A static line-segment barrier (design px, y down from Zone B's top).</summary>
    public sealed class WallDef
    {
        public double X1, Y1, X2, Y2;
        public double Thickness = DefaultThickness;
        /// <summary>Fill the area between this rail and the Zone B bottom with solid wood (funnel ramps).</summary>
        public bool FillBelow;

        public const double DefaultThickness = 6;

        public static WallDef Line(double x1, double y1, double x2, double y2, bool fillBelow = false) =>
            new WallDef { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, FillBelow = fillBelow };
    }

    public sealed class ZoneBLayout
    {
        public string Name;
        public GateDef[] Gates;
        public CollectorDef[] Collectors;
        public WallDef[] Walls;
    }

    /// <summary>
    /// The two hand-built "shelf cascade" layouts (1:1 port of <c>zoneLayout.ts</c>): stacked horizontal
    /// multiplier shelves (static gates) split by vertical/diagonal guide rails (walls), funnelling
    /// into a bottom cup (collector). One is picked per run. The Phaser file authored y in ABSOLUTE
    /// screen coordinates (Zone B spanning 551..1238); <see cref="Y"/> rebases them onto the band so
    /// the numbers below still read verbatim against master.
    /// </summary>
    public static class ZoneBLayouts
    {
        /// <summary>Gate slab thickness (design px).</summary>
        public const double GateThickness = 16;
        /// <summary>Score-bar height along the bottom of Zone B (design px).</summary>
        public const double BarHeight = 32;

        const double PhaserZoneBTop = 551;
        static double Y(double phaserY) => phaserY - PhaserZoneBTop;

        // The two funnel ramps that feed the single bottom collector — shared by both layouts.
        static WallDef[] FunnelRamps() => new[]
        {
            WallDef.Line(0, Y(1129), 110, Y(1233), fillBelow: true),
            WallDef.Line(390, Y(1129), 280, Y(1233), fillBelow: true),
        };

        // The drain sits right at the score bar (bar top ≈ y=1222): its top is one ball-radius above
        // the bar so a ball vanishes just as its bottom meets the bar, not floating above it.
        static CollectorDef BottomCollector() => new CollectorDef { X = 110, Y = Y(1212), Width = 170, Height = 26, ScoreMultiplier = 1 };

        static WallDef[] Concat(WallDef[] a, WallDef[] b)
        {
            var r = new WallDef[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        /// <summary>LAYOUT_1 — three-segment top row narrowing to a central bottom gate.</summary>
        public static ZoneBLayout Layout1 => new ZoneBLayout
        {
            Name = "LAYOUT_1",
            Gates = new[]
            {
                // Row 1 — gaps at x∈[100,116] and x∈[248,264].
                GateDef.Static(54, Y(720), 0, 92, 4),
                GateDef.Static(182, Y(720), 0, 132, 3),
                GateDef.Static(323, Y(720), 0, 118, 2),
                // Row 2
                GateDef.Static(45, Y(901), 0, 85, 2),
                GateDef.Static(195, Y(901), 0, 110, 2),
                GateDef.Static(348, Y(901), 0, 80, 2),
                // Row 3
                GateDef.Static(195, Y(1051), 0, 90, 2),
            },
            Collectors = new[] { BottomCollector() },
            Walls = Concat(new[]
            {
                // Row-1 gate dividers, centred in the gaps and framing the three top gates.
                WallDef.Line(108, Y(715), 108, Y(810)),
                WallDef.Line(256, Y(715), 256, Y(810)),
                // Frame posts rising above the right (X2) gate.
                WallDef.Line(264, Y(677), 264, Y(720)),
                WallDef.Line(382, Y(677), 382, Y(720)),
                // Left outer rail: straight down the left side, then a diagonal converging to centre.
                WallDef.Line(75, Y(752), 75, Y(921)),
                WallDef.Line(75, Y(921), 165, Y(1051)),
                // Right outer rail: straight down the right side, then a diagonal converging to centre.
                WallDef.Line(320, Y(752), 320, Y(921)),
                WallDef.Line(320, Y(921), 230, Y(1051)),
                // Post above the row-2 centre gate.
                WallDef.Line(140, Y(854), 140, Y(901)),
                // Small end caps on the row-3 centre gate.
                WallDef.Line(150, Y(1051), 150, Y(1079)),
                WallDef.Line(240, Y(1051), 240, Y(1079)),
            }, FunnelRamps()),
        };

        /// <summary>LAYOUT_2 — offset rows with zig-zag diagonal rails.</summary>
        public static ZoneBLayout Layout2 => new ZoneBLayout
        {
            Name = "LAYOUT_2",
            Gates = new[]
            {
                // Row 1 — gaps at x∈[195,211] and x∈[293,309].
                GateDef.Static(105, Y(720), 0, 180, 4),
                GateDef.Static(252, Y(720), 0, 82, 3),
                GateDef.Static(348, Y(720), 0, 78, 4),
                // Row 2
                GateDef.Static(95, Y(908), 0, 170, 4),
                GateDef.Static(250, Y(908), 0, 85, 2),
                GateDef.Static(345, Y(908), 0, 85, 3),
                // Row 3
                GateDef.Static(100, Y(1051), 0, 120, 2),
                GateDef.Static(300, Y(1051), 0, 140, 3),
            },
            Collectors = new[] { BottomCollector() },
            Walls = Concat(new[]
            {
                // Row-1 gate dividers, centred in the gaps and rising slightly above the bars.
                WallDef.Line(203, Y(681), 203, Y(752)),
                WallDef.Line(301, Y(681), 301, Y(752)),
                // Upper diagonal off the left divider, down-left toward the row-2 left gate.
                WallDef.Line(203, Y(752), 150, Y(869)),
                // Tall right rail: the right divider runs straight down past row 2 into row 3.
                WallDef.Line(301, Y(752), 301, Y(1038)),
                // Lower zig-zag diagonal from below the row-2 centre gate down to the row-3 right gate.
                WallDef.Line(207, Y(921), 250, Y(1038)),
                // Short vertical cap on the far left of row 3.
                WallDef.Line(40, Y(980), 40, Y(1053)),
            }, FunnelRamps()),
        };

        public static ZoneBLayout[] All => new[] { Layout1, Layout2 };

        /// <summary>Layout for a run, from a random draw in [0, 1] (the caller supplies the RNG).</summary>
        public static ZoneBLayout Pick(double unit)
        {
            var all = All;
            int i = (int)Math.Floor(Math.Max(0, Math.Min(0.999999, unit)) * all.Length);
            return all[i];
        }
    }
}
