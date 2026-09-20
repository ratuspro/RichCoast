using System;
using System.Collections.Generic;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    /// <summary>
    /// The generated Zone B arena. The load-bearing test here is the seed sweep: every arena the game
    /// can ever build must satisfy <see cref="ZoneBGenerator.Validate"/>, because a bad one would
    /// either trap a ball (locking the trap-door forever) or hand out the golden payoff for free.
    /// </summary>
    public class ZoneBGeneratorTests
    {
        const double BallDiameter = 2 * DesignSpace.ZoneBBallRadius;

        /// <summary>The trap-door's nine sweep columns — what GameBootstrap hands the generator.</summary>
        static double[] SweepColumns()
        {
            var xs = new double[DesignSpace.SweepPositions];
            for (int i = 0; i < xs.Length; i++)
                xs[i] = DoorMath.SweepPositionX(i, DesignSpace.SweepPositions, DesignSpace.SweepMargin, DesignSpace.Width - DesignSpace.SweepMargin);
            return xs;
        }

        static string Describe(ZoneBLayout layout)
        {
            var parts = new List<string>();
            foreach (var g in layout.Gates) parts.Add($"{(g.IsGolden ? "*" : "")}x{g.Multiplier}@({g.Cx:0},{g.Cy:0})/{g.Length:0}");
            return $"{layout.Name}: {string.Join(" ", parts)}";
        }

        [Test]
        public void SameSeedGivesIdenticalLayout()
        {
            var a = ZoneBGenerator.Generate(4242);
            var b = ZoneBGenerator.Generate(4242);
            Assert.AreEqual(a.Seed, b.Seed);
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.Gates.Length, b.Gates.Length);
            Assert.AreEqual(a.Walls.Length, b.Walls.Length);
            for (int i = 0; i < a.Gates.Length; i++)
            {
                Assert.AreEqual(a.Gates[i].Cx, b.Gates[i].Cx, 1e-12);
                Assert.AreEqual(a.Gates[i].Cy, b.Gates[i].Cy, 1e-12);
                Assert.AreEqual(a.Gates[i].Length, b.Gates[i].Length, 1e-12);
                Assert.AreEqual(a.Gates[i].Multiplier, b.Gates[i].Multiplier);
            }
            Assert.AreEqual(a.Golden.MouthX, b.Golden.MouthX, 1e-12);
        }

        [Test]
        public void DifferentSeedsGiveDifferentArenas()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 40; seed++) seen.Add(Describe(ZoneBGenerator.Generate(seed)));
            Assert.That(seen.Count, Is.GreaterThan(30), "40 seeds should nearly all differ");
        }

        [Test]
        public void EveryGeneratedArenaHoldsTheInvariants()
        {
            var columns = SweepColumns();
            var p = new ZoneBGenParams();
            for (int seed = 0; seed < 500; seed++)
            {
                var layout = ZoneBGenerator.Generate(seed, p, columns);
                Assert.IsTrue(ZoneBGenerator.Validate(layout, p, columns, out string why), $"seed {seed}: {why}\n{Describe(layout)}");
            }
        }

        [Test]
        public void EveryArenaHoldsTheInvariantsWithoutEntryColumnsToo()
        {
            // A future continuous aim passes no columns; the mouth may then land anywhere.
            var p = new ZoneBGenParams();
            for (int seed = 0; seed < 200; seed++)
            {
                var layout = ZoneBGenerator.Generate(seed, p, null);
                Assert.IsTrue(ZoneBGenerator.Validate(layout, p, null, out string why), $"seed {seed}: {why}");
            }
        }

        [Test]
        public void GoldenMouthSnapsToAnInteriorEntryColumn()
        {
            var columns = SweepColumns();
            var p = new ZoneBGenParams();
            double slack = ZoneBGenerator.MouthJitterSlack(p);
            double aperture = ZoneBGenerator.ApertureHalfWidth(p);
            Assert.That(slack, Is.GreaterThan(0), "the default aperture must admit a ball with room to spare");
            for (int seed = 0; seed < 200; seed++)
            {
                var layout = ZoneBGenerator.Generate(seed, p, columns);
                double mouth = layout.Golden.MouthX;
                int nearest = -1;
                for (int i = 0; i < columns.Length; i++)
                    if (Math.Abs(columns[i] - mouth) <= slack + 1e-6) nearest = i;
                Assert.That(nearest, Is.InRange(1, columns.Length - 2),
                    $"seed {seed}: mouth at {mouth:0.#} must sit on an interior column, not the wall-hugging ends");

                // A ball dropped straight down that column must clear the narrowest point of the
                // golden path — the mouth or the chute rails' inner faces, whichever pinches first.
                double column = columns[nearest];
                Assert.That(column - DesignSpace.ZoneBBallRadius, Is.GreaterThanOrEqualTo(mouth - aperture - 1e-6), $"seed {seed}: ball clips the left of the aperture");
                Assert.That(column + DesignSpace.ZoneBBallRadius, Is.LessThanOrEqualTo(mouth + aperture + 1e-6), $"seed {seed}: ball clips the right of the aperture");
            }
        }

        [Test]
        public void TheBarrierRowIsSolidExceptTheGoldenMouth()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var layout = ZoneBGenerator.Generate(seed, null, SweepColumns());
                double rowY = double.MaxValue;
                foreach (var g in layout.Gates) if (!g.IsGolden) rowY = Math.Min(rowY, g.Cy);

                var row = new List<GateDef>();
                foreach (var g in layout.Gates) if (!g.IsGolden && Math.Abs(g.Cy - rowY) < 1e-9) row.Add(g);
                row.Sort((a, b) => a.Cx.CompareTo(b.Cx));

                double cursor = 0;
                var apertures = new List<(double from, double to)>();
                foreach (var g in row)
                {
                    double left = g.Cx - g.Length / 2;
                    if (left - cursor >= BallDiameter) apertures.Add((cursor, left));
                    cursor = Math.Max(cursor, g.Cx + g.Length / 2);
                }
                if (DesignSpace.Width - cursor >= BallDiameter) apertures.Add((cursor, DesignSpace.Width));

                Assert.AreEqual(1, apertures.Count, $"seed {seed}: the barrier row must have exactly one passable gap");
                double mid = (apertures[0].from + apertures[0].to) / 2;
                Assert.AreEqual(layout.Golden.MouthX, mid, 1e-6, $"seed {seed}: the one passable gap must be the golden mouth");
            }
        }

        [Test]
        public void OneGildedGatePaysSixToEightAndEverythingElseStaysUnderFive()
        {
            var seenMultipliers = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                var layout = ZoneBGenerator.Generate(seed, null, SweepColumns());
                int golden = 0;
                foreach (var g in layout.Gates)
                {
                    if (g.IsGolden)
                    {
                        golden++;
                        Assert.That(g.Multiplier, Is.InRange(6, 8), $"seed {seed}");
                        seenMultipliers.Add(g.Multiplier);
                        Assert.AreEqual(layout.Golden.Multiplier, g.Multiplier, $"seed {seed}");
                    }
                    else
                    {
                        Assert.That(g.Multiplier, Is.InRange(2, 4), $"seed {seed}: ordinary gate x{g.Multiplier}");
                    }
                }
                Assert.AreEqual(1, golden, $"seed {seed}");
            }
            CollectionAssert.AreEquivalent(new[] { 6, 7, 8 }, seenMultipliers, "all three golden payouts should appear across 200 seeds");
        }

        [Test]
        public void TheFixedFunnelAndCollectorSurviveGeneration()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = ZoneBGenerator.Generate(seed, null, SweepColumns());
                Assert.AreEqual(1, layout.Collectors.Length, $"seed {seed}");
                var c = layout.Collectors[0];
                Assert.AreEqual(DesignSpace.ZoneBHeight, c.Y + c.Height, 1e-9, $"seed {seed}: collector flush with the band bottom");
                int ramps = 0;
                foreach (var w in layout.Walls) if (w.FillBelow) ramps++;
                Assert.AreEqual(2, ramps, $"seed {seed}: two funnel ramps feed the collector");
            }
        }

        [Test]
        public void AnImpossibleMouthWidthIsRejectedRatherThanShippingABrokenArena()
        {
            // A mouth narrower than a ball can never be the barrier row's one passable gap.
            var p = new ZoneBGenParams { GoldenMouthWidth = 12, MaxRerolls = 3 };
            Assert.That(ZoneBGenerator.MouthJitterSlack(p), Is.LessThan(0));
            Assert.Throws<InvalidOperationException>(() => ZoneBGenerator.Generate(1, p, SweepColumns()));
        }

        [Test]
        public void GatePosesFollowTheirKind()
        {
            var s = GateDef.Static(10, 20, 0.3, 50, 2);
            Assert.AreEqual((10.0, 20.0, 0.3), s.PoseAt(12345));

            var t = GateDef.Translating(0, 0, 100, 0, 0, 40, 2, periodMs: 1000);
            Assert.AreEqual(50, t.PoseAt(0).x, 1e-9);     // sin(0) → midpoint
            Assert.AreEqual(100, t.PoseAt(250).x, 1e-9);  // sin(pi/2) → B end
            Assert.AreEqual(0, t.PoseAt(750).x, 1e-9);    // sin(3pi/2) → A end

            var r = GateDef.Rotating(5, 5, 30, 3, speedRadPerMs: 0.001);
            Assert.AreEqual(1.0, r.PoseAt(1000).angle, 1e-9);
            Assert.AreEqual(5.0, r.PoseAt(1000).x);
        }

        [Test]
        public void AStaticGateSegmentSpansItsLength()
        {
            var g = GateDef.Static(100, 50, 0, 60, 2);
            var (x0, y0, x1, y1) = g.Segment();
            Assert.AreEqual(70, x0, 1e-9);
            Assert.AreEqual(130, x1, 1e-9);
            Assert.AreEqual(50, y0, 1e-9);
            Assert.AreEqual(50, y1, 1e-9);
        }
    
        // --- The two-speed split -----------------------------------------------------------------

        /// <summary>
        /// Everything the DRESSING is not allowed to move: the spread rows, the guide diagonals, the
        /// row depths and the gilded gate's depth. The barrier row is excluded on purpose — the mouth is
        /// cut into it, so it must shift when the mouth does.
        /// </summary>
        static string DescribeSkeleton(ZoneBLayout layout)
        {
            double rowY = double.NaN;
            foreach (var g in layout.Gates) if (!g.IsGolden && (double.IsNaN(rowY) || g.Cy < rowY)) rowY = g.Cy;
            var parts = new List<string>();
            foreach (var g in layout.Gates)
            {
                if (g.IsGolden || Math.Abs(g.Cy - rowY) <= ZoneBLayouts.GateThickness) continue;
                parts.Add($"G{g.Cx:0.######},{g.Cy:0.######},{g.Length:0.######}");
            }
            foreach (var w in layout.Walls)
            {
                if (!w.IsGuide) continue;
                parts.Add($"W{w.X1:0.######},{w.Y1:0.######},{w.X2:0.######},{w.Y2:0.######}");
            }
            parts.Add($"Y{layout.Golden.GateY:0.######}");
            return string.Join(" ", parts);
        }

        /// <summary>
        /// THE test for the whole two-speed idea. Re-dressing an arena forty times must not move one
        /// pixel of its silhouette — if the RNG streams ever re-couple (a section drawing a different
        /// NUMBER of values because the mouth moved), this is what catches it.
        /// </summary>
        [Test]
        public void ReDressingNeverMovesTheSkeleton()
        {
            var columns = SweepColumns();
            for (int structure = 0; structure < 60; structure++)
            {
                string expected = null;
                for (int dressing = 0; dressing < 40; dressing++)
                {
                    var layout = ZoneBGenerator.Generate(structure, dressing, null, columns);
                    string actual = DescribeSkeleton(layout);
                    if (expected == null) expected = actual;
                    else Assert.AreEqual(expected, actual, $"structure {structure} shifted on dressing {dressing}");
                }
            }
        }

        /// <summary>
        /// The other half of the contract: a skeleton the player keeps for twenty levels must still
        /// give the mouth somewhere to go, or the aim target is pinned for the whole window.
        /// </summary>
        [Test]
        public void ReDressingMovesTheMouthAndTheMultipliers()
        {
            var columns = SweepColumns();
            for (int structure = 0; structure < 60; structure++)
            {
                var mouths = new HashSet<double>();
                var goldens = new HashSet<int>();
                for (int dressing = 0; dressing < 40; dressing++)
                {
                    var layout = ZoneBGenerator.Generate(structure, dressing, null, columns);
                    mouths.Add(Math.Round(layout.Golden.MouthX, 6));
                    goldens.Add(layout.Golden.Multiplier);
                }
                Assert.GreaterOrEqual(mouths.Count, ZoneBGenerator.MinMouthColumns,
                    $"structure {structure} pins the mouth to {mouths.Count} column(s)");
                Assert.GreaterOrEqual(goldens.Count, 2, $"structure {structure} never varies the gilded payout");
            }
        }

        /// <summary>
        /// The reserved columns are NEIGHBOURS, so a level-up nudges the aim one sweep step instead of
        /// throwing it across the board. That is the difference between re-reading the arena and
        /// re-learning it.
        /// </summary>
        [Test]
        public void TheMouthOnlyEverMovesToAdjacentColumns()
        {
            var columns = SweepColumns();
            double spacing = columns[1] - columns[0];
            for (int structure = 0; structure < 80; structure++)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                for (int dressing = 0; dressing < 40; dressing++)
                {
                    double x = ZoneBGenerator.Generate(structure, dressing, null, columns).Golden.MouthX;
                    lo = Math.Min(lo, x);
                    hi = Math.Max(hi, x);
                }
                double span = spacing * (ZoneBGenerator.MinMouthColumns - 1);
                Assert.LessOrEqual(hi - lo, span + 1e-6,
                    $"structure {structure} spreads the mouth over {hi - lo:0.#} px, wider than {ZoneBGenerator.MinMouthColumns} adjacent columns");
            }
        }

        /// <summary>Both halves of the roll are deterministic, and the layout reports both back.</summary>
        [Test]
        public void BothSeedsAreRecordedAndDeterministic()
        {
            var columns = SweepColumns();
            var a = ZoneBGenerator.Generate(77, 12, null, columns);
            var b = ZoneBGenerator.Generate(77, 12, null, columns);
            Assert.AreEqual(77, a.StructureSeed);
            Assert.AreEqual(12, a.DressingSeed);
            Assert.AreEqual(a.StructureSeed, a.Seed, "Seed must stay an alias of the structure seed");
            Assert.AreEqual(DescribeSkeleton(a), DescribeSkeleton(b));
            Assert.AreEqual(a.Golden.MouthX, b.Golden.MouthX, 1e-12);
            Assert.AreEqual(a.Golden.Multiplier, b.Golden.Multiplier);
        }

        /// <summary>Every (structure, dressing) pair the runtime can reach holds the whole contract.</summary>
        [Test]
        public void EverySeedPairHoldsTheInvariants()
        {
            var columns = SweepColumns();
            var p = new ZoneBGenParams();
            for (int structure = 0; structure < 500; structure++)
            {
                var layout = ZoneBGenerator.Generate(structure, structure * 7 + 3, p, columns);
                Assert.IsTrue(ZoneBGenerator.Validate(layout, p, columns, out string why),
                    $"structure {structure}: {why}\n{Describe(layout)}");
            }
        }
    }
}
