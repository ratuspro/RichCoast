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
        /// <summary>The one big gilded gate at the foot of the golden chute — painted bright brass, pays a fanfare.</summary>
        public bool IsGolden;

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

        /// <summary>Left/right ends of a static gate's line segment (design px).</summary>
        public (double x0, double y0, double x1, double y1) Segment()
        {
            double half = Length / 2;
            double c = Math.Cos(Angle), s = Math.Sin(Angle);
            return (Cx - half * c, Cy - half * s, Cx + half * c, Cy + half * s);
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
        /// <summary>Part of the golden chute — painted bright brass rather than pine.</summary>
        public bool IsGolden;
        /// <summary>
        /// A free-placed guide diagonal. These are the only sloped rails in an arena, so they are the
        /// only ones that can form a WEDGE with another rail — hence the clearance rule they must hold.
        /// </summary>
        public bool IsGuide;

        public const double DefaultThickness = 6;

        public static WallDef Line(double x1, double y1, double x2, double y2, bool fillBelow = false) =>
            new WallDef { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, FillBelow = fillBelow };
    }

    /// <summary>
    /// Where the golden path of a generated arena sits: the mouth is the one gap in the barrier row
    /// wide enough to admit a ball, and it drops through a gilded chute onto the gilded gate.
    /// </summary>
    public sealed class GoldenPath
    {
        /// <summary>Centre of the barrier-row mouth (design px) — the column a player must hit.</summary>
        public double MouthX;
        /// <summary>Depth of the barrier row the mouth pierces (design px, down from the band top).</summary>
        public double MouthY;
        public double MouthWidth;
        /// <summary>Depth of the gilded gate (design px, down from the band top).</summary>
        public double GateY;
        /// <summary>Index into <see cref="ZoneBLayout.Gates"/> of the gilded gate.</summary>
        public int GateIndex;
        public int Multiplier;
    }

    public sealed class ZoneBLayout
    {
        public string Name;
        /// <summary>Back-compat alias for <see cref="StructureSeed"/>.</summary>
        public int Seed;
        /// <summary>Fixes the silhouette. Re-rolled only at a milestone.</summary>
        public int StructureSeed;
        /// <summary>Fixes the golden mouth column and every multiplier. Re-rolled once per level.</summary>
        public int DressingSeed;
        public GateDef[] Gates;
        public CollectorDef[] Collectors;
        public WallDef[] Walls;
        /// <summary>Never null for a generated arena.</summary>
        public GoldenPath Golden;
    }

    /// <summary>
    /// The fixed furniture every Zone B arena shares, in design px with y measured DOWN from the band
    /// top. The playfield above it is generated per drop by <see cref="ZoneBGenerator"/>; only the two
    /// funnel ramps and the single bottom collector are authored, because they line up with the score
    /// bar. The numbers keep the Phaser file's ABSOLUTE screen coordinates (Zone B spanned 551..1238)
    /// through <see cref="Y"/> so they still read verbatim against master.
    /// </summary>
    public static class ZoneBLayouts
    {
        /// <summary>Gate slab thickness (design px).</summary>
        public const double GateThickness = 16;
        /// <summary>Score-bar height along the bottom of Zone B (design px).</summary>
        public const double BarHeight = 32;
        /// <summary>Depth (design px) below which only the funnel and collector may live.</summary>
        public const double FunnelTopY = 560;

        const double PhaserZoneBTop = 551;
        static double Y(double phaserY) => phaserY - PhaserZoneBTop;

        /// <summary>The two funnel ramps that feed the single bottom collector.</summary>
        public static WallDef[] FunnelRamps() => new[]
        {
            WallDef.Line(0, Y(1129), 110, Y(1233), fillBelow: true),
            WallDef.Line(390, Y(1129), 280, Y(1233), fillBelow: true),
        };

        /// <summary>
        /// The drain sits right at the score bar (bar top ≈ y=1222): its top is one ball-radius above
        /// the bar so a ball vanishes just as its bottom meets the bar, not floating above it.
        /// </summary>
        public static CollectorDef BottomCollector() => new CollectorDef { X = 110, Y = Y(1212), Width = 170, Height = 26, ScoreMultiplier = 1 };
    }
}
