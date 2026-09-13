using System;
using System.Collections.Generic;

namespace RichCoast.Core
{
    /// <summary>
    /// Tuning for <see cref="ZoneBGenerator"/>. Every distance is design px with y measured DOWN from
    /// the Zone B band top; the band is <see cref="DesignSpace.Width"/> × <see cref="DesignSpace.ZoneBHeight"/>.
    /// Mirrored by <c>ZoneBArenaSO</c> in the Game assembly so it is tunable live.
    /// </summary>
    public sealed class ZoneBGenParams
    {
        /// <summary>Depth of the barrier row — the one every ordinary drop is guaranteed to hit.</summary>
        public double Row1YMin = 160, Row1YMax = 185;
        public double Row2YMin = 330, Row2YMax = 365;
        public double Row3YMin = 485, Row3YMax = 515;

        /// <summary>Barrier-row cracks: deliberately narrower than a ball, so only the mouth lets one through.</summary>
        public double CrackMin = 10, CrackMax = 16;
        /// <summary>Passable gaps in the lower rows.</summary>
        public double GapMin = 44, GapMax = 64;

        /// <summary>The golden mouth's aperture. Must exceed a ball diameter (20) by enough to admit one aimed at its column.</summary>
        public double GoldenMouthWidth = 32;
        public int GoldenMultiplierMin = 6, GoldenMultiplierMax = 8;
        public double GoldenGateLength = 60;
        /// <summary>How far below the barrier row the gilded gate hangs.</summary>
        public double GoldenGateDropMin = 100, GoldenGateDropMax = 130;
        /// <summary>Half-width of the gilded chute (rail CENTRES); also the mouth's flanking posts.</summary>
        public double ChuteHalfWidth = 19;
        /// <summary>Clear air a ball keeps on each side while falling through the chute.</summary>
        public double ChuteClearance = 2;
        /// <summary>How far the chute rails rise above the barrier row.</summary>
        public double ChuteRise = 24;

        public double MinGateLen = 28;
        /// <summary>Vertical divider dropped from each barrier-row crack so a ball cannot perch in it.</summary>
        public double DividerDrop = 90;

        public int Row2GapsMin = 2, Row2GapsMax = 3;
        public int Row3GapsMin = 2, Row3GapsMax = 3;
        public int DiagonalsMin = 2, DiagonalsMax = 4;
        public double DiagonalRunMin = 40, DiagonalRunMax = 95;
        /// <summary>
        /// Clear air a guide diagonal must keep from every other rail and from the side walls. A gap
        /// narrower than a ball diameter (20) is a WEDGE: the ball drops in, cannot fall through, and
        /// the stuck-nudge watchdog can never shake it out. Gates are exempt — a ball that touches one
        /// is split and consumed, so a gate can never trap.
        /// </summary>
        public double GuideClearance = 26;

        /// <summary>Re-rolls allowed before <see cref="ZoneBGenerator.Generate"/> gives up.</summary>
        public int MaxRerolls = 24;
    }

    /// <summary>
    /// Builds a Zone B arena from a seed. The grammar is a three-row shelf cascade:
    ///
    /// <list type="bullet">
    /// <item><b>Row 1 — the barrier.</b> Gates separated by cracks NARROWER than a ball, so every
    /// ordinary drop hits a gate and splits. Exactly one gap is passable: the golden mouth.</item>
    /// <item><b>The golden path.</b> The mouth's centre snaps onto one of the caller's entry columns
    /// (the trap-door's sweep positions), flanked by two gilded rails that form a chute down to a
    /// single gilded ×6–×8 gate. Thread the mouth and the payoff is guaranteed; miss and the
    /// ordinary ×2–×4 cascade plays out.</item>
    /// <item><b>Rows 2 and 3 — the spread.</b> Ordinary gates with passable gaps, feeding the fixed
    /// funnel ramps and bottom collector.</item>
    /// </list>
    ///
    /// Pure and deterministic: the same seed always yields the same arena. Every candidate is checked
    /// against <see cref="Validate"/> and re-rolled with seed+1 until it holds.
    /// </summary>
    public static class ZoneBGenerator
    {
        const double BallRadius = DesignSpace.ZoneBBallRadius;
        const double Width = DesignSpace.Width;
        const double Height = DesignSpace.ZoneBHeight;

        /// <summary>
        /// A fresh arena for <paramref name="seed"/>. When <paramref name="entryXs"/> is given the
        /// golden mouth is centred on one of those columns (the interior ones — a chute at the very
        /// edge would run into a side wall); pass null to let the mouth land anywhere, which is what a
        /// continuous aim would do.
        /// </summary>
        public static ZoneBLayout Generate(int seed, ZoneBGenParams p = null, IReadOnlyList<double> entryXs = null)
        {
            p = p ?? new ZoneBGenParams();
            for (int attempt = 0; attempt <= p.MaxRerolls; attempt++)
            {
                var layout = Build(seed + attempt, p, entryXs);
                if (Validate(layout, p, entryXs, out _)) return layout;
            }
            throw new InvalidOperationException($"ZoneBGenerator: no valid arena within {p.MaxRerolls} re-rolls from seed {seed}");
        }

        // --- Build ---------------------------------------------------------------------------------

        static ZoneBLayout Build(int seed, ZoneBGenParams p, IReadOnlyList<double> entryXs)
        {
            var rng = new Random(seed);
            var gates = new List<GateDef>();
            var walls = new List<WallDef>();

            double row1Y = Range(rng, p.Row1YMin, p.Row1YMax);
            double row2Y = Range(rng, p.Row2YMin, p.Row2YMax);
            double row3Y = Range(rng, p.Row3YMin, p.Row3YMax);
            double gateY = row1Y + Range(rng, p.GoldenGateDropMin, p.GoldenGateDropMax);

            double mouthHalf = p.GoldenMouthWidth / 2;
            double mouthX = PickMouthX(rng, p, entryXs);

            // --- Row 1: the barrier, split either side of the mouth ---------------------------------
            var cracks = new List<Span>();
            BuildBarrierSide(rng, p, 0, mouthX - mouthHalf, row1Y, gates, cracks);
            BuildBarrierSide(rng, p, mouthX + mouthHalf, Width, row1Y, gates, cracks);
            foreach (var crack in cracks)
            {
                walls.Add(WallDef.Line(crack.Mid, row1Y - 5, crack.Mid, row1Y + p.DividerDrop));
            }

            // --- The golden chute -------------------------------------------------------------------
            var railL = WallDef.Line(mouthX - p.ChuteHalfWidth, row1Y - p.ChuteRise, mouthX - p.ChuteHalfWidth, gateY - 12);
            var railR = WallDef.Line(mouthX + p.ChuteHalfWidth, row1Y - p.ChuteRise, mouthX + p.ChuteHalfWidth, gateY - 12);
            railL.IsGolden = railR.IsGolden = true;
            walls.Add(railL);
            walls.Add(railR);

            int goldenMult = rng.Next(p.GoldenMultiplierMin, p.GoldenMultiplierMax + 1);
            var golden = GateDef.Static(mouthX, gateY, 0, p.GoldenGateLength, goldenMult);
            golden.IsGolden = true;
            int goldenIndex = gates.Count;
            gates.Add(golden);

            // --- Rows 2 and 3: the spread -------------------------------------------------------------
            BuildSpreadRow(rng, p, row2Y, rng.Next(p.Row2GapsMin, p.Row2GapsMax + 1), 4, gates);
            BuildSpreadRow(rng, p, row3Y, rng.Next(p.Row3GapsMin, p.Row3GapsMax + 1), 3, gates);

            // --- Guide diagonals ----------------------------------------------------------------------
            BuildDiagonals(rng, p, row1Y, row2Y, row3Y, mouthX, mouthHalf, golden, walls);

            walls.AddRange(ZoneBLayouts.FunnelRamps());

            return new ZoneBLayout
            {
                Name = $"GEN-{seed}",
                Seed = seed,
                Gates = gates.ToArray(),
                Walls = walls.ToArray(),
                Collectors = new[] { ZoneBLayouts.BottomCollector() },
                Golden = new GoldenPath
                {
                    MouthX = mouthX,
                    MouthY = row1Y,
                    MouthWidth = p.GoldenMouthWidth,
                    GateY = gateY,
                    GateIndex = goldenIndex,
                    Multiplier = goldenMult,
                },
            };
        }

        /// <summary>
        /// Half the narrowest opening on the golden path: the mouth between the flanking gates, or the
        /// clear air between the chute rails' inner faces — whichever pinches first.
        /// </summary>
        public static double ApertureHalfWidth(ZoneBGenParams p)
        {
            p = p ?? new ZoneBGenParams();
            return Math.Min(p.GoldenMouthWidth / 2, p.ChuteHalfWidth - WallDef.DefaultThickness / 2);
        }

        /// <summary>
        /// How far the mouth may sit off an entry column and still swallow a ball dropped straight down
        /// it. Zero or less means the aperture cannot admit a ball at all — the arena is unbuildable.
        /// </summary>
        public static double MouthJitterSlack(ZoneBGenParams p)
        {
            p = p ?? new ZoneBGenParams();
            return ApertureHalfWidth(p) - BallRadius - p.ChuteClearance;
        }

        /// <summary>
        /// The mouth centre. With entry columns supplied it snaps to an interior one, jittered only as
        /// far as <see cref="MouthJitterSlack"/> allows — so a ball dropped down that column always
        /// falls clean through the mouth and between the chute rails.
        /// </summary>
        static double PickMouthX(Random rng, ZoneBGenParams p, IReadOnlyList<double> entryXs)
        {
            double slack = Math.Max(0, MouthJitterSlack(p));
            double minX = p.ChuteHalfWidth + p.GoldenGateLength / 2 + 4;
            double maxX = Width - minX;
            if (entryXs == null || entryXs.Count == 0) return Range(rng, minX, maxX);

            var allowed = new List<double>();
            foreach (double x in entryXs) if (x >= minX && x <= maxX) allowed.Add(x);
            if (allowed.Count == 0) return Range(rng, minX, maxX);
            double chosen = allowed[rng.Next(allowed.Count)];
            return Clamp(chosen + Range(rng, -slack, slack), minX, maxX);
        }

        /// <summary>One side of the barrier row: gates butted against sub-ball-width cracks.</summary>
        static void BuildBarrierSide(Random rng, ZoneBGenParams p, double x0, double x1, double rowY, List<GateDef> gates, List<Span> cracks)
        {
            double width = x1 - x0;
            if (width < p.MinGateLen) return;

            int n = (int)Math.Round(width / 130.0);
            n = (int)Clamp(n, 0, 2);
            while (n > 0 && width < (n + 1) * p.MinGateLen + n * p.CrackMax) n--;

            var spans = new List<Span>();
            var gapSpans = new List<Span>();
            if (!Partition(rng, x0, x1, n, p.CrackMin, p.CrackMax, p.MinGateLen, spans, gapSpans))
            {
                spans.Clear();
                gapSpans.Clear();
                Partition(rng, x0, x1, 0, p.CrackMin, p.CrackMax, p.MinGateLen, spans, gapSpans);
            }
            foreach (var span in spans) gates.Add(GateDef.Static(span.Mid, rowY, 0, span.Len, rng.NextDouble() < 0.65 ? 2 : 3));
            cracks.AddRange(gapSpans);
        }

        /// <summary>A lower row: gates separated by gaps a ball can actually fall through.</summary>
        static void BuildSpreadRow(Random rng, ZoneBGenParams p, double rowY, int gaps, int maxMultiplier, List<GateDef> gates)
        {
            var spans = new List<Span>();
            var gapSpans = new List<Span>();
            while (gaps > 0 && !Partition(rng, 0, Width, gaps, p.GapMin, p.GapMax, p.MinGateLen, spans, gapSpans))
            {
                spans.Clear();
                gapSpans.Clear();
                gaps--;
            }
            if (spans.Count == 0) Partition(rng, 0, Width, Math.Max(1, gaps), p.GapMin, p.GapMax, p.MinGateLen, spans, gapSpans);
            foreach (var span in spans)
            {
                double roll = rng.NextDouble();
                int mult = roll < 0.45 ? 2 : roll < 0.8 ? 3 : 4;
                gates.Add(GateDef.Static(span.Mid, rowY, 0, span.Len, Math.Min(mult, maxMultiplier)));
            }
        }

        /// <summary>
        /// Guide rails in the two inter-row bands. Each band is inset far enough from the rows above and
        /// below that a diagonal can never touch a gate, so what a candidate must dodge is the golden
        /// chute, the gilded gate, and — critically — every other RAIL: a sloped rail passing close
        /// under a vertical divider makes a wedge no ball can escape.
        /// </summary>
        static void BuildDiagonals(Random rng, ZoneBGenParams p, double row1Y, double row2Y, double row3Y,
            double mouthX, double mouthHalf, GateDef golden, List<WallDef> walls)
        {
            int count = rng.Next(p.DiagonalsMin, p.DiagonalsMax + 1);
            var bands = new[]
            {
                (top: row1Y + 45, bottom: row2Y - 25, upper: true),
                (top: row2Y + 45, bottom: row3Y - 25, upper: false),
            };
            double channelL = mouthX - mouthHalf - p.ChuteHalfWidth - 6;
            double channelR = mouthX + mouthHalf + p.ChuteHalfWidth + 6;
            double goldenL = golden.Cx - golden.Length / 2 - 14, goldenR = golden.Cx + golden.Length / 2 + 14;

            for (int i = 0; i < count; i++)
            {
                var band = bands[i % 2];
                if (band.bottom - band.top < 30) continue;
                // Most rejections are the clearance rule in the upper band, where the dividers and the
                // chute crowd things; keep trying rather than silently shipping a bare arena.
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    double y1 = Range(rng, band.top, band.top + (band.bottom - band.top) * 0.35);
                    double y2 = Range(rng, band.bottom - (band.bottom - band.top) * 0.35, band.bottom);
                    double run = Range(rng, p.DiagonalRunMin, p.DiagonalRunMax) * (rng.NextDouble() < 0.5 ? -1 : 1);
                    double edge = p.GuideClearance;
                    double x1 = Range(rng, edge, Width - edge);
                    double x2 = Clamp(x1 + run, edge, Width - edge);
                    if (Math.Abs(x2 - x1) < p.DiagonalRunMin * 0.6) continue;
                    if (band.upper)
                    {
                        // Never pinch the chute, and never sit close enough to the gilded gate to
                        // deflect a copy straight back into it.
                        double lo = Math.Min(x1, x2), hi = Math.Max(x1, x2);
                        if (hi > channelL && lo < channelR) continue;
                        bool spansGoldenY = Math.Min(y1, y2) <= golden.Cy + 14 && Math.Max(y1, y2) >= golden.Cy - 14;
                        if (spansGoldenY && hi > goldenL && lo < goldenR) continue;
                    }
                    var candidate = WallDef.Line(x1, y1, x2, y2);
                    candidate.IsGuide = true;
                    if (!GuideIsClear(candidate, walls, p)) continue;
                    walls.Add(candidate);
                    break;
                }
            }
        }

        // --- Wedge avoidance -------------------------------------------------------------------------

        /// <summary>
        /// Does this guide rail keep a ball's width of clear air from every rail already placed, and
        /// from both side walls? Anything less is a pocket a ball can fall into but not out of.
        /// </summary>
        static bool GuideIsClear(WallDef guide, List<WallDef> placed, ZoneBGenParams p)
        {
            if (Math.Min(guide.X1, guide.X2) < p.GuideClearance) return false;
            if (Math.Max(guide.X1, guide.X2) > Width - p.GuideClearance) return false;
            foreach (var other in placed)
            {
                if (other.FillBelow) continue; // the funnel is far below every guide band
                if (ClearGap(guide, other) < p.GuideClearance) return false;
            }
            return true;
        }

        /// <summary>Clear air between two rails: centreline distance less both half-thicknesses.</summary>
        static double ClearGap(WallDef a, WallDef b) =>
            SegmentDistance(a.X1, a.Y1, a.X2, a.Y2, b.X1, b.Y1, b.X2, b.Y2) - (a.Thickness + b.Thickness) / 2;

        /// <summary>Shortest distance between two segments (0 when they cross).</summary>
        static double SegmentDistance(double ax0, double ay0, double ax1, double ay1,
            double bx0, double by0, double bx1, double by1)
        {
            if (SegmentsCross(ax0, ay0, ax1, ay1, bx0, by0, bx1, by1)) return 0;
            double d = PointSegmentDistance(ax0, ay0, bx0, by0, bx1, by1);
            d = Math.Min(d, PointSegmentDistance(ax1, ay1, bx0, by0, bx1, by1));
            d = Math.Min(d, PointSegmentDistance(bx0, by0, ax0, ay0, ax1, ay1));
            return Math.Min(d, PointSegmentDistance(bx1, by1, ax0, ay0, ax1, ay1));
        }

        static double PointSegmentDistance(double px, double py, double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0, dy = y1 - y0;
            double len2 = dx * dx + dy * dy;
            double t = len2 <= 1e-12 ? 0 : Clamp(((px - x0) * dx + (py - y0) * dy) / len2, 0, 1);
            double qx = x0 + t * dx - px, qy = y0 + t * dy - py;
            return Math.Sqrt(qx * qx + qy * qy);
        }

        static bool SegmentsCross(double ax0, double ay0, double ax1, double ay1,
            double bx0, double by0, double bx1, double by1)
        {
            double d1 = Cross(bx0, by0, bx1, by1, ax0, ay0);
            double d2 = Cross(bx0, by0, bx1, by1, ax1, ay1);
            double d3 = Cross(ax0, ay0, ax1, ay1, bx0, by0);
            double d4 = Cross(ax0, ay0, ax1, ay1, bx1, by1);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static double Cross(double x0, double y0, double x1, double y1, double px, double py) =>
            (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);

        // --- Partitioning ---------------------------------------------------------------------------

        readonly struct Span
        {
            public readonly double X0, X1;
            public Span(double x0, double x1) { X0 = x0; X1 = x1; }
            public double Mid => (X0 + X1) / 2;
            public double Len => X1 - X0;
        }

        /// <summary>
        /// Lay <paramref name="gaps"/>+1 gates across [x0, x1] separated by gaps drawn from
        /// [gapMin, gapMax]. False when the region cannot hold that many gates at <paramref name="minGateLen"/>.
        /// </summary>
        static bool Partition(Random rng, double x0, double x1, int gaps, double gapMin, double gapMax,
            double minGateLen, List<Span> gates, List<Span> gapSpans)
        {
            double width = x1 - x0;
            var gapWidths = new double[gaps];
            double gapTotal = 0;
            for (int i = 0; i < gaps; i++)
            {
                gapWidths[i] = Range(rng, gapMin, gapMax);
                gapTotal += gapWidths[i];
            }
            int n = gaps + 1;
            double gateTotal = width - gapTotal;
            if (gateTotal < n * minGateLen) return false;

            var parts = SplitWithMin(rng, gateTotal, n, minGateLen);
            double cursor = x0;
            for (int i = 0; i < n; i++)
            {
                gates.Add(new Span(cursor, cursor + parts[i]));
                cursor += parts[i];
                if (i < gaps)
                {
                    gapSpans.Add(new Span(cursor, cursor + gapWidths[i]));
                    cursor += gapWidths[i];
                }
            }
            return true;
        }

        /// <summary>Split <paramref name="total"/> into <paramref name="n"/> parts, each at least <paramref name="min"/>.</summary>
        static double[] SplitWithMin(Random rng, double total, int n, double min)
        {
            var parts = new double[n];
            var weights = new double[n];
            double slack = total - n * min, weightSum = 0;
            for (int i = 0; i < n; i++)
            {
                weights[i] = 0.35 + rng.NextDouble();
                weightSum += weights[i];
            }
            for (int i = 0; i < n; i++) parts[i] = min + slack * weights[i] / weightSum;
            return parts;
        }

        // --- Validation ------------------------------------------------------------------------------

        /// <summary>
        /// Every invariant a playable arena must hold. Also the contract the EditMode seed sweep
        /// asserts — if this passes, a ball can always reach the collector and the golden mouth is the
        /// only way past the barrier row.
        /// </summary>
        public static bool Validate(ZoneBLayout layout, ZoneBGenParams p, IReadOnlyList<double> entryXs, out string reason)
        {
            p = p ?? new ZoneBGenParams();
            reason = null;
            var g = layout.Golden;
            if (g == null) return Fail("no golden path", out reason);

            // 1. Everything inside the band.
            foreach (var gate in layout.Gates)
            {
                var (x0, y0, x1, y1) = gate.Segment();
                if (Math.Min(x0, x1) < -1e-9 || Math.Max(x0, x1) > Width + 1e-9) return Fail($"gate x out of band ({gate.Cx})", out reason);
                if (Math.Min(y0, y1) < 0 || Math.Max(y0, y1) > Height) return Fail($"gate y out of band ({gate.Cy})", out reason);
            }
            foreach (var w in layout.Walls)
            {
                if (w.X1 < -1e-9 || w.X1 > Width + 1e-9 || w.X2 < -1e-9 || w.X2 > Width + 1e-9) return Fail("wall x out of band", out reason);
                if (w.Y1 < 0 || w.Y1 > Height || w.Y2 < 0 || w.Y2 > Height) return Fail("wall y out of band", out reason);
                if (Math.Abs(w.X2 - w.X1) + Math.Abs(w.Y2 - w.Y1) <= 0) return Fail("zero-length wall", out reason);
            }

            // 2. Exactly one gilded gate; every other multiplier stays in the balanced band.
            int goldenCount = 0;
            foreach (var gate in layout.Gates)
            {
                if (gate.IsGolden)
                {
                    goldenCount++;
                    if (gate.Multiplier < p.GoldenMultiplierMin || gate.Multiplier > p.GoldenMultiplierMax) return Fail("golden multiplier out of range", out reason);
                }
                else if (gate.Multiplier < 2 || gate.Multiplier > 4) return Fail($"gate x{gate.Multiplier} outside 2..4", out reason);
            }
            if (goldenCount != 1) return Fail($"{goldenCount} gilded gates", out reason);
            if (layout.Gates[g.GateIndex] == null || !layout.Gates[g.GateIndex].IsGolden) return Fail("golden index mismatch", out reason);

            // 3. No two gates of a row overlap in x.
            for (int i = 0; i < layout.Gates.Length; i++)
            {
                for (int j = i + 1; j < layout.Gates.Length; j++)
                {
                    var a = layout.Gates[i];
                    var b = layout.Gates[j];
                    if (Math.Abs(a.Cy - b.Cy) > ZoneBLayouts.GateThickness) continue;
                    if (a.Cx - a.Length / 2 < b.Cx + b.Length / 2 - 1e-6 && b.Cx - b.Length / 2 < a.Cx + a.Length / 2 - 1e-6)
                        return Fail($"gates overlap at y={a.Cy:0}", out reason);
                }
            }

            // 4. The barrier row admits a ball through the golden mouth and nowhere else.
            double rowY = double.NaN;
            foreach (var gate in layout.Gates) if (!gate.IsGolden && (double.IsNaN(rowY) || gate.Cy < rowY)) rowY = gate.Cy;
            var barrier = GatesNear(layout, rowY);
            if (barrier.Count == 0) return Fail("no barrier row", out reason);
            barrier.Sort((a, b) => a.Cx.CompareTo(b.Cx));
            double cursor = 0;
            int apertures = 0;
            foreach (var gate in barrier)
            {
                double left = gate.Cx - gate.Length / 2;
                if (left - cursor >= 2 * BallRadius) apertures++;
                cursor = Math.Max(cursor, gate.Cx + gate.Length / 2);
            }
            if (Width - cursor >= 2 * BallRadius) apertures++;
            if (apertures != 1) return Fail($"barrier row has {apertures} passable gaps, want exactly the mouth", out reason);

            // 5. Lower rows each leave a way through.
            foreach (var y in RowDepths(layout, rowY))
            {
                if (!HasGap(GatesNear(layout, y), 2 * BallRadius + 4)) return Fail($"row at y={y:0} is impassable", out reason);
            }

            // 6. The golden channel is clear from the mouth down to the gilded gate.
            double channelHalf = g.MouthWidth / 2 - 1;
            double chL = g.MouthX - channelHalf, chR = g.MouthX + channelHalf;
            double chTop = rowY - p.ChuteRise - 1, chBottom = g.GateY - 2;
            foreach (var gate in layout.Gates)
            {
                if (gate.IsGolden) continue;
                if (gate.Cy < chTop || gate.Cy > chBottom) continue;
                if (gate.Cx - gate.Length / 2 < chR && gate.Cx + gate.Length / 2 > chL) return Fail("a gate blocks the golden channel", out reason);
            }
            foreach (var w in layout.Walls)
            {
                if (w.IsGolden || w.FillBelow) continue;
                if (SegmentHitsRect(w.X1, w.Y1, w.X2, w.Y2, chL, chTop, chR, chBottom)) return Fail("a wall blocks the golden channel", out reason);
            }

            // 7. Mouth geometry, and its column when the caller supplied one. The aperture is the
            //    narrower of the mouth and the chute rails' inner faces — a ball aimed at the column
            //    must fall through BOTH with clear air, or the golden path is a lie.
            if (Math.Abs(g.MouthWidth - p.GoldenMouthWidth) > 1e-9) return Fail("mouth width drifted", out reason);
            double slack = MouthJitterSlack(p);
            if (slack < 0) return Fail($"aperture {2 * ApertureHalfWidth(p):0.#} cannot admit a {2 * BallRadius:0.#} ball", out reason);
            if (entryXs != null && entryXs.Count > 0)
            {
                bool snapped = false;
                foreach (double x in entryXs) if (Math.Abs(x - g.MouthX) <= slack + 1e-6) snapped = true;
                if (!snapped) return Fail($"mouth at {g.MouthX:0.#} is not within {slack:0.#} of an entry column", out reason);
            }

            // 8. No wedges: every guide rail keeps a ball's width from every other rail. This is the
            //    invariant that stops an arena trapping a ball forever and locking the trap-door.
            for (int i = 0; i < layout.Walls.Length; i++)
            {
                var guide = layout.Walls[i];
                if (!guide.IsGuide) continue;
                if (Math.Min(guide.X1, guide.X2) < p.GuideClearance || Math.Max(guide.X1, guide.X2) > Width - p.GuideClearance)
                    return Fail("a guide rail crowds a side wall", out reason);
                for (int j = 0; j < layout.Walls.Length; j++)
                {
                    if (i == j) continue;
                    var other = layout.Walls[j];
                    if (other.FillBelow) continue;
                    if (ClearGap(guide, other) < p.GuideClearance)
                        return Fail($"guide rail wedges against another rail (gap {ClearGap(guide, other):0.#})", out reason);
                }
            }

            // 9. The funnel's region belongs to the funnel; the fixed furniture is intact.
            foreach (var gate in layout.Gates)
                if (gate.Cy + ZoneBLayouts.GateThickness / 2 > ZoneBLayouts.FunnelTopY) return Fail("a gate reaches into the funnel", out reason);
            foreach (var w in layout.Walls)
                if (!w.FillBelow && Math.Max(w.Y1, w.Y2) > ZoneBLayouts.FunnelTopY) return Fail("a wall reaches into the funnel", out reason);
            if (layout.Collectors.Length != 1) return Fail("want exactly one collector", out reason);
            var c = layout.Collectors[0];
            if (Math.Abs(c.Y + c.Height - Height) > 1e-9) return Fail("collector not flush with the band bottom", out reason);
            int ramps = 0;
            foreach (var w in layout.Walls) if (w.FillBelow) ramps++;
            if (ramps != 2) return Fail($"{ramps} funnel ramps, want 2", out reason);

            return true;
        }

        static bool Fail(string why, out string reason)
        {
            reason = why;
            return false;
        }

        static List<GateDef> GatesNear(ZoneBLayout layout, double y)
        {
            var row = new List<GateDef>();
            foreach (var gate in layout.Gates)
                if (!gate.IsGolden && Math.Abs(gate.Cy - y) <= ZoneBLayouts.GateThickness) row.Add(gate);
            return row;
        }

        /// <summary>Distinct row depths below the barrier row.</summary>
        static List<double> RowDepths(ZoneBLayout layout, double barrierY)
        {
            var ys = new List<double>();
            foreach (var gate in layout.Gates)
            {
                if (gate.IsGolden || gate.Cy <= barrierY + ZoneBLayouts.GateThickness) continue;
                bool seen = false;
                foreach (double y in ys) if (Math.Abs(y - gate.Cy) <= ZoneBLayouts.GateThickness) seen = true;
                if (!seen) ys.Add(gate.Cy);
            }
            return ys;
        }

        static bool HasGap(List<GateDef> row, double needed)
        {
            row.Sort((a, b) => a.Cx.CompareTo(b.Cx));
            double cursor = 0;
            foreach (var gate in row)
            {
                if (gate.Cx - gate.Length / 2 - cursor >= needed) return true;
                cursor = Math.Max(cursor, gate.Cx + gate.Length / 2);
            }
            return Width - cursor >= needed;
        }

        /// <summary>Does segment (x1,y1)-(x2,y2) touch the axis-aligned rect? Liang-Barsky clip.</summary>
        static bool SegmentHitsRect(double x1, double y1, double x2, double y2, double left, double top, double right, double bottom)
        {
            double dx = x2 - x1, dy = y2 - y1;
            double t0 = 0, t1 = 1;
            double[] ps = { -dx, dx, -dy, dy };
            double[] qs = { x1 - left, right - x1, y1 - top, bottom - y1 };
            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(ps[i]) < 1e-12)
                {
                    if (qs[i] < 0) return false;
                    continue;
                }
                double r = qs[i] / ps[i];
                if (ps[i] < 0)
                {
                    if (r > t1) return false;
                    if (r > t0) t0 = r;
                }
                else
                {
                    if (r < t0) return false;
                    if (r < t1) t1 = r;
                }
            }
            return true;
        }

        static double Range(Random rng, double min, double max) => min + rng.NextDouble() * (max - min);

        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
