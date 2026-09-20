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
        /// <summary>
        /// Shortest barrier gate the mouth may leave when it is punched in. Deliberately below
        /// <see cref="MinGateLen"/>: a flank only has to be a real gate that splits a ball, and holding
        /// it to the full minimum would exclude 88 px of barrier around every crack, leaving too few
        /// columns the mouth could ever sit on.
        /// </summary>
        public double MouthFlankMin = 14;
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
    /// Pure and deterministic: the same pair of seeds always yields the same arena. The roll is split in
    /// two so the arena can change at two different speeds — a STRUCTURE seed fixes the silhouette and is
    /// re-rolled only at a milestone, while a DRESSING seed moves the golden mouth and re-rolls the
    /// multipliers once per level. Every candidate is checked against <see cref="Validate"/>.
    /// </summary>
    public static class ZoneBGenerator
    {
        const double BallRadius = DesignSpace.ZoneBBallRadius;
        const double Width = DesignSpace.Width;
        const double Height = DesignSpace.ZoneBHeight;

        /// <summary>
        /// How many distinct mouth columns a skeleton must offer before it is accepted. The mouth
        /// moves every level while the skeleton holds for a whole milestone window, so a skeleton that
        /// only admits one column would pin the player's aim for twenty levels.
        /// </summary>
        public const int MinMouthColumns = 3;

        /// <summary>Internal re-rolls of the skeleton itself before <see cref="Generate"/> moves on.</summary>
        const int SkeletonAttempts = 64;

        /// <summary>
        /// A fresh arena for <paramref name="seed"/>, used for both halves of the roll. Convenience for
        /// callers that do not care about the two-speed split (the screenshot tool and the seed sweeps).
        /// </summary>
        public static ZoneBLayout Generate(int seed, ZoneBGenParams p = null, IReadOnlyList<double> entryXs = null)
            => Generate(seed, seed, p, entryXs);

        /// <summary>
        /// A fresh arena from two independent seeds.
        ///
        /// <para><paramref name="structureSeed"/> fixes the SKELETON — row depths, the barrier's gates
        /// and cracks, the spread rows, the guide diagonals and the gilded gate's depth. It is re-rolled
        /// only at a milestone, so the arena's silhouette is a room the player keeps for twenty levels.</para>
        ///
        /// <para><paramref name="dressingSeed"/> fixes the DRESSING — which column the golden mouth sits
        /// on and every gate's multiplier. It is re-rolled once per level, so the aim target moves often
        /// enough to stay interesting without the board becoming unrecognisable.</para>
        ///
        /// <para>When <paramref name="entryXs"/> is given the mouth is centred exactly on one of those
        /// columns (the interior ones — a chute at the very edge would run into a side wall); pass null
        /// to let the mouth land anywhere, which is what a continuous aim would do.</para>
        /// </summary>
        public static ZoneBLayout Generate(int structureSeed, int dressingSeed, ZoneBGenParams p = null,
            IReadOnlyList<double> entryXs = null)
        {
            p = p ?? new ZoneBGenParams();
            for (int s = 0; s <= p.MaxRerolls; s++)
            {
                var skeleton = BuildSkeleton(unchecked(structureSeed + s), p, entryXs);
                if (skeleton == null) continue;
                // Dressing-first retry: preserving the skeleton matters more than preserving the mouth,
                // and every column in MouthColumns already validated, so this lands on the first pass.
                for (int d = 0; d <= p.MaxRerolls; d++)
                {
                    var layout = skeleton.Dress(unchecked(dressingSeed + d));
                    if (layout != null && Validate(layout, p, entryXs, out _)) return layout;
                }
            }
            throw new InvalidOperationException(
                $"ZoneBGenerator: no valid arena within {p.MaxRerolls} re-rolls from structure {structureSeed} / dressing {dressingSeed}");
        }

        // --- Seeding --------------------------------------------------------------------------------

        /// <summary>
        /// One independent RNG stream per section of the grammar. A single interleaved stream would
        /// couple the sections: the barrier row consumes a different number of draws depending on how
        /// its partition falls, which would shift the spread rows and the diagonals with it. Separate
        /// streams are what makes "the skeleton is byte-identical across a milestone window" true rather
        /// than merely intended.
        /// </summary>
        static Random Sub(int seed, int salt) => new Random(unchecked(seed * 486187739 + salt));

        // --- Structure ------------------------------------------------------------------------------

        /// <summary>
        /// Everything the dressing does not touch, plus the set of mouth columns this skeleton can
        /// actually accept. The columns are worked out ONCE, here, by dressing a trial arena at each
        /// candidate and running the full <see cref="Validate"/> contract over it — so a dressing roll
        /// can never fail on a diagonal it happens to land behind.
        /// </summary>
        sealed class Skeleton
        {
            public ZoneBGenParams P;
            public int StructureSeed;
            public double Row1Y, Row2Y, Row3Y, GateY;
            public List<Span> BarrierSpans;
            public List<Span> Row2Spans, Row3Spans;
            /// <summary>Crack dividers then guide diagonals. The chute rails are dressing, not skeleton.</summary>
            public List<WallDef> Walls;
            public List<double> MouthColumns;

            public ZoneBLayout Dress(int dressingSeed)
            {
                var rng = Sub(dressingSeed, 5);
                double mouthX = MouthColumns[rng.Next(MouthColumns.Count)];
                return Compose(mouthX, rng, dressingSeed);
            }

            /// <summary>
            /// Punch the mouth into the barrier, hang the chute and the gilded gate off it, and roll
            /// every multiplier. Null when the mouth cannot be punched cleanly — it must sit wholly
            /// inside one barrier gate, leaving a real gate on each side, or the barrier stops being a
            /// barrier.
            /// </summary>
            public ZoneBLayout Compose(double mouthX, Random rng, int dressingSeed)
            {
                double half = P.GoldenMouthWidth / 2;
                double mouthL = mouthX - half, mouthR = mouthX + half;
                var gates = new List<GateDef>();
                var walls = new List<WallDef>(Walls);

                bool punched = false;
                foreach (var span in BarrierSpans)
                {
                    bool overlaps = mouthR > span.X0 + 1e-9 && mouthL < span.X1 - 1e-9;
                    if (!overlaps) { gates.Add(BarrierGate(span, rng)); continue; }
                    if (punched) return null;
                    var left = new Span(span.X0, mouthL);
                    var right = new Span(mouthR, span.X1);
                    if (left.Len < P.MouthFlankMin || right.Len < P.MouthFlankMin) return null;
                    gates.Add(BarrierGate(left, rng));
                    gates.Add(BarrierGate(right, rng));
                    punched = true;
                }
                if (!punched) return null; // the mouth fell in a crack: that crack would become passable

                var railL = WallDef.Line(mouthX - P.ChuteHalfWidth, Row1Y - P.ChuteRise, mouthX - P.ChuteHalfWidth, GateY - 12);
                var railR = WallDef.Line(mouthX + P.ChuteHalfWidth, Row1Y - P.ChuteRise, mouthX + P.ChuteHalfWidth, GateY - 12);
                railL.IsGolden = railR.IsGolden = true;
                walls.Add(railL);
                walls.Add(railR);

                int goldenMult = rng.Next(P.GoldenMultiplierMin, P.GoldenMultiplierMax + 1);
                var golden = GateDef.Static(mouthX, GateY, 0, P.GoldenGateLength, goldenMult);
                golden.IsGolden = true;
                int goldenIndex = gates.Count;
                gates.Add(golden);

                foreach (var span in Row2Spans) gates.Add(SpreadGate(span, Row2Y, 4, rng));
                foreach (var span in Row3Spans) gates.Add(SpreadGate(span, Row3Y, 3, rng));

                walls.AddRange(ZoneBLayouts.FunnelRamps());

                return new ZoneBLayout
                {
                    Name = $"GEN-{StructureSeed}/{dressingSeed}",
                    Seed = StructureSeed,
                    StructureSeed = StructureSeed,
                    DressingSeed = dressingSeed,
                    Gates = gates.ToArray(),
                    Walls = walls.ToArray(),
                    Collectors = new[] { ZoneBLayouts.BottomCollector() },
                    Golden = new GoldenPath
                    {
                        MouthX = mouthX,
                        MouthY = Row1Y,
                        MouthWidth = P.GoldenMouthWidth,
                        GateY = GateY,
                        GateIndex = goldenIndex,
                        Multiplier = goldenMult,
                    },
                };
            }

            /// <summary>
            /// Can the mouth be cut at this column? It must land wholly inside one barrier gate and
            /// leave a real gate on each side — a mouth straddling a crack would widen that crack into
            /// a second way through, and the barrier's whole job is being the only one.
            /// </summary>
            public bool CanPunch(double mouthX)
            {
                double half = P.GoldenMouthWidth / 2;
                foreach (var span in BarrierSpans)
                {
                    if (!(mouthX + half > span.X0 + 1e-9 && mouthX - half < span.X1 - 1e-9)) continue;
                    return mouthX - half - span.X0 >= P.MouthFlankMin
                        && span.X1 - (mouthX + half) >= P.MouthFlankMin;
                }
                return false;
            }

            GateDef BarrierGate(Span span, Random rng) =>
                GateDef.Static(span.Mid, Row1Y, 0, span.Len, rng.NextDouble() < 0.65 ? 2 : 3);

            static GateDef SpreadGate(Span span, double rowY, int maxMultiplier, Random rng)
            {
                double roll = rng.NextDouble();
                int mult = roll < 0.45 ? 2 : roll < 0.8 ? 3 : 4;
                return GateDef.Static(span.Mid, rowY, 0, span.Len, Math.Min(mult, maxMultiplier));
            }
        }

        static Skeleton BuildSkeleton(int structureSeed, ZoneBGenParams p, IReadOnlyList<double> entryXs)
        {
            var candidates = CandidateColumns(p, entryXs, out int stride);
            for (int attempt = 0; attempt < SkeletonAttempts; attempt++)
            {
                int s = unchecked(structureSeed * 31 + attempt);
                var rowsRng = Sub(s, 1);
                var barrierRng = Sub(s, 2);
                var spreadRng = Sub(s, 3);
                var diagRng = Sub(s, 4);

                var sk = new Skeleton { P = p, StructureSeed = structureSeed };
                sk.Row1Y = Range(rowsRng, p.Row1YMin, p.Row1YMax);
                sk.Row2Y = Range(rowsRng, p.Row2YMin, p.Row2YMax);
                sk.Row3Y = Range(rowsRng, p.Row3YMin, p.Row3YMax);
                sk.GateY = sk.Row1Y + Range(rowsRng, p.GoldenGateDropMin, p.GoldenGateDropMax);

                sk.BarrierSpans = new List<Span>();
                var cracks = new List<Span>();
                if (!BuildBarrierRow(barrierRng, p, sk.BarrierSpans, cracks)) continue;

                sk.Walls = new List<WallDef>();
                foreach (var crack in cracks)
                    sk.Walls.Add(WallDef.Line(crack.Mid, sk.Row1Y - 5, crack.Mid, sk.Row1Y + p.DividerDrop));

                int row2Gaps = spreadRng.Next(p.Row2GapsMin, p.Row2GapsMax + 1);
                sk.Row2Spans = SpreadRow(spreadRng, p, row2Gaps);
                int row3Gaps = spreadRng.Next(p.Row3GapsMin, p.Row3GapsMax + 1);
                sk.Row3Spans = SpreadRow(spreadRng, p, row3Gaps);

                var reserved = ReserveMouthColumns(sk, candidates, stride, diagRng);
                if (reserved == null) continue;

                BuildDiagonals(diagRng, p, sk, reserved);

                var trialRng = Sub(s, 9);
                sk.MouthColumns = new List<double>();
                foreach (double x in reserved)
                {
                    var trial = sk.Compose(x, trialRng, 0);
                    if (trial != null && Validate(trial, p, entryXs, out _)) sk.MouthColumns.Add(x);
                }
                if (sk.MouthColumns.Count >= MinMouthColumns) return sk;
            }
            return null;
        }

        /// <summary>
        /// Every mouth position worth trying, and the index stride that counts as "the next column
        /// along". With entry columns supplied the mouth snaps EXACTLY onto one — the old ±4 px jitter
        /// bought nothing a player could see and cost the aim its dead centre, so it is gone. Without
        /// them (a future continuous aim) the band is sampled coarsely and the stride stands in for a
        /// column's width.
        /// </summary>
        static List<double> CandidateColumns(ZoneBGenParams p, IReadOnlyList<double> entryXs, out int stride)
        {
            double minX = p.ChuteHalfWidth + p.GoldenGateLength / 2 + 4;
            double maxX = Width - minX;
            var list = new List<double>();
            if (entryXs != null && entryXs.Count > 0)
            {
                foreach (double x in entryXs) if (x >= minX && x <= maxX) list.Add(x);
                if (list.Count > 0) { stride = 1; return list; }
            }
            const double step = 8;
            for (double x = minX; x <= maxX + 1e-9; x += step) list.Add(x);
            stride = (int)Math.Ceiling(88 / step);
            return list;
        }

        /// <summary>
        /// The columns this skeleton will offer the mouth: a run of <see cref="MinMouthColumns"/>
        /// NEIGHBOURING candidates the barrier can be punched at. Neighbouring on purpose — across a
        /// milestone window the mouth should shuffle one sweep step at a time, a nudge the player
        /// re-reads in a drop or two, rather than jumping the width of the board every level.
        /// </summary>
        static List<double> ReserveMouthColumns(Skeleton sk, IReadOnlyList<double> candidates, int stride, Random rng)
        {
            var starts = new List<int>();
            int span = stride * (MinMouthColumns - 1);
            for (int i = 0; i + span < candidates.Count; i++)
            {
                bool ok = true;
                for (int k = 0; k < MinMouthColumns && ok; k++) ok = sk.CanPunch(candidates[i + k * stride]);
                if (ok) starts.Add(i);
            }
            if (starts.Count == 0) return null;
            int start = starts[rng.Next(starts.Count)];
            var run = new List<double>();
            for (int k = 0; k < MinMouthColumns; k++) run.Add(candidates[start + k * stride]);
            return run;
        }

        /// <summary>
        /// The barrier row as a solid shelf — gates butted against sub-ball-width cracks, laid across the
        /// FULL width with no knowledge of the mouth. The mouth is punched in later by the dressing, which
        /// is what keeps the cracks (and so the dividers, and so the diagonals that dodge them) skeletal.
        /// </summary>
        static bool BuildBarrierRow(Random rng, ZoneBGenParams p, List<Span> gates, List<Span> cracks)
        {
            // Two cracks leaves three wide gates, and punching the mouth into one of them yields the
            // four-gate barrier the grammar wants. Three is occasionally drawn for a busier shelf, but
            // the gates get tight enough that most such skeletons fail the mouth-column count and re-roll.
            int gaps = rng.NextDouble() < 0.7 ? 2 : 3;
            while (gaps > 0 && !Partition(rng, 0, Width, gaps, p.CrackMin, p.CrackMax, p.MinGateLen, gates, cracks))
            {
                gates.Clear();
                cracks.Clear();
                gaps--;
            }
            return gates.Count > 0;
        }

        /// <summary>A lower row: gates separated by gaps a ball can actually fall through.</summary>
        static List<Span> SpreadRow(Random rng, ZoneBGenParams p, int gaps)
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
            return spans;
        }

        /// <summary>
        /// Guide rails in the two inter-row bands. Each band is inset far enough from the rows above and
        /// below that a diagonal can never touch a gate, so what a candidate must dodge is every other
        /// RAIL: a sloped rail passing close under a vertical divider makes a wedge no ball can escape.
        ///
        /// Blind to WHICH reserved column the mouth will use this level, but not to the reserved set: an
        /// upper-band diagonal keeps clear of every column the dressing might pick, so the chute can drop
        /// at any of them without the skeleton moving. Dodging one known mouth instead — what the
        /// single-seed generator did — would make the diagonals shift every time the mouth did.
        /// </summary>
        static void BuildDiagonals(Random rng, ZoneBGenParams p, Skeleton sk, List<double> reserved)
        {
            int count = rng.Next(p.DiagonalsMin, p.DiagonalsMax + 1);
            var bands = new[]
            {
                (top: sk.Row1Y + 45, bottom: sk.Row2Y - 25),
                (top: sk.Row2Y + 45, bottom: sk.Row3Y - 25),
            };
            // Widest the golden path can be at any reserved column: the mouth plus its chute rails, or
            // the gilded gate's ends, whichever reaches further.
            double corridor = Math.Max(p.GoldenMouthWidth / 2 + p.ChuteHalfWidth + 6, p.GoldenGateLength / 2 + 14);

            for (int i = 0; i < count; i++)
            {
                var band = bands[i % 2];
                if (band.bottom - band.top < 30) continue;
                // Most rejections are the clearance rule in the upper band, where the dividers crowd
                // things; keep trying rather than silently shipping a bare arena.
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    double y1 = Range(rng, band.top, band.top + (band.bottom - band.top) * 0.35);
                    double y2 = Range(rng, band.bottom - (band.bottom - band.top) * 0.35, band.bottom);
                    double run = Range(rng, p.DiagonalRunMin, p.DiagonalRunMax) * (rng.NextDouble() < 0.5 ? -1 : 1);
                    double edge = p.GuideClearance;
                    double x1 = Range(rng, edge, Width - edge);
                    double x2 = Clamp(x1 + run, edge, Width - edge);
                    if (Math.Abs(x2 - x1) < p.DiagonalRunMin * 0.6) continue;
                    if (i % 2 == 0)
                    {
                        double lo = Math.Min(x1, x2), hi = Math.Max(x1, x2);
                        bool fouls = false;
                        foreach (double c in reserved) if (hi > c - corridor && lo < c + corridor) { fouls = true; break; }
                        if (fouls) continue;
                    }
                    var candidate = WallDef.Line(x1, y1, x2, y2);
                    candidate.IsGuide = true;
                    if (!GuideIsClear(candidate, sk.Walls, p)) continue;
                    sk.Walls.Add(candidate);
                    break;
                }
            }
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
        /// The generator now snaps the mouth dead onto a column, so this is headroom rather than budget.
        /// </summary>
        public static double MouthJitterSlack(ZoneBGenParams p)
        {
            p = p ?? new ZoneBGenParams();
            return ApertureHalfWidth(p) - BallRadius - p.ChuteClearance;
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
