using System;
using System.Collections.Generic;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Data;
using UnityEngine;

namespace RichCoast.Tests
{
    /// <summary>Ported from the original <c>zoneA/ballMath.test.ts</c>.</summary>
    public class BallMathTests
    {
        private TierTable _table;
        private Progression _progression;

        [SetUp]
        public void SetUp()
        {
            _table = DefaultTierLadder.CreateTable();
            _progression = DefaultProgression.Create();
        }

        // --- tuning tables -----------------------------------------------------

        [Test]
        public void TuningTables_HaveExactlyOneRadiusPerTier()
        {
            Assert.That(Tuning.Radii.Length, Is.EqualTo(Tiers.TierCount));
            // The look ladder is longer than the radius table; it must at least cover it.
            Assert.That(_table.MaterialCount, Is.GreaterThanOrEqualTo(Tiers.TierCount));
        }

        [Test]
        public void TuningTables_PlaceTheSpawnRowAboveTheDeathLine() =>
            Assert.That(Tuning.SpawnY, Is.LessThan(Tuning.DeathLineY));

        [Test]
        public void TuningTables_LetTheLargestSpawnableTierClearTheCeilingAtTheSpawnRow() =>
            Assert.That(_table.RadiusForTier(4), Is.LessThan(Tuning.SpawnY));

        [Test]
        public void TuningTables_KeepEvenTheLargestBaseBallWithinTheScreenWidth() =>
            Assert.That(_table.RadiusForTier(Tiers.TierCount) * 2, Is.LessThan(Layout.Width));

        // --- radius ------------------------------------------------------------

        [Test]
        public void RadiusForTier_IsStrictlyIncreasingAcrossTheBaseTable()
        {
            for (var t = 2; t <= Tiers.TierCount; t++)
            {
                Assert.That(_table.RadiusForTier(t), Is.GreaterThan(_table.RadiusForTier(t - 1)));
            }
        }

        [Test]
        public void RadiusForTier_KeepsGrowingPastTheBaseTable()
        {
            Assert.That(_table.RadiusForTier(Tiers.TierCount + 1), Is.GreaterThan(_table.RadiusForTier(Tiers.TierCount)));
            Assert.That(_table.RadiusForTier(Tiers.TierCount + 5), Is.GreaterThan(_table.RadiusForTier(Tiers.TierCount + 4)));
        }

        [Test]
        public void RadiusForTier_ClampsTiersBelowOneToTheSmallest() =>
            Assert.That(_table.RadiusForTier(0), Is.EqualTo(_table.RadiusForTier(1)));

        // --- arena growth ------------------------------------------------------

        [Test]
        public void NeutralGrowth_MatchesTheWindowMaxRadiusRatioInTheHandTableRegion() =>
            Assert.That(BallMath.NeutralGrowth(_table, 4, 8), Is.EqualTo(71f / 34f).Within(1e-5f));

        [Test]
        public void NeutralGrowth_ConvergesToRadiusGrowthPowFourPastTheTable() =>
            Assert.That(BallMath.NeutralGrowth(_table, 12, 16),
                Is.EqualTo(Mathf.Pow(Tuning.RadiusGrowth, 4)).Within(1e-4f));

        [Test]
        public void NeutralGrowth_IsExactlyOneWhenTheWindowDoesNotShift() =>
            Assert.That(BallMath.NeutralGrowth(_table, 20, 20), Is.EqualTo(1f));

        [Test]
        public void MilestoneZoom_IsOneOnANonMilestoneLevelEvenIfTheWindowsDiffer() =>
            Assert.That(BallMath.MilestoneZoomFactor(_table, 21, new TierWindow(1, 4), new TierWindow(5, 8), 0.92f),
                Is.EqualTo(1f));

        [Test]
        public void MilestoneZoom_IsOneOnAMilestoneWhoseWindowDidNotShift() =>
            Assert.That(BallMath.MilestoneZoomFactor(_table, 100, new TierWindow(17, 20), new TierWindow(17, 20), 1.05f),
                Is.EqualTo(1f));

        [Test]
        public void MilestoneZoom_IsTheNeutralGrowthTimesTightnessOnAShiftedMilestone() =>
            Assert.That(BallMath.MilestoneZoomFactor(_table, 20, new TierWindow(1, 4), new TierWindow(5, 8), 0.92f),
                Is.EqualTo(71f / 34f * 0.92f).Within(1e-5f));

        [Test]
        public void MilestoneZoom_TailMilestonesUseTheFlatUnderNeutralFactor()
        {
            var zoom = BallMath.MilestoneZoomFactor(_table, 100, _progression.WindowForLevel(99),
                _progression.WindowForLevel(100), 1f, tail: true);
            Assert.That(zoom, Is.EqualTo(BallMath.TailMilestoneZoom));
            // The flat tail zoom sits BELOW the neutral match for a +2 shift, so apparent ball
            // size creeps up each tail milestone — the endgame's mounting squeeze.
            Assert.That(BallMath.TailMilestoneZoom, Is.LessThan(BallMath.NeutralGrowth(_table, 20, 22)));
            Assert.That(BallMath.TailMilestoneZoom, Is.GreaterThan(1f));
        }

        [Test]
        public void MilestoneZoom_ARollThroughThatOvershootsAMilestoneStillZooms()
        {
            // One Zone B drain rolls the bar through levels 39 → 40 → 41: level 40 is a shifted
            // milestone, but the burst's FINAL level (41) is not. Folding the per-level factors
            // into a product — what Zone A's pending cash-in does — must preserve the zoom.
            var s39 = _progression.GetStage(39);
            var s40 = _progression.GetStage(40);
            var s41 = _progression.GetStage(41);

            var product =
                BallMath.MilestoneZoomFactor(_table, 40, s39.Window, s40.Window, s40.Tightness) *
                BallMath.MilestoneZoomFactor(_table, 41, s40.Window, s41.Window, s41.Tightness);

            Assert.That(product,
                Is.EqualTo(BallMath.NeutralGrowth(_table, s39.Window.Max, s40.Window.Max) * s40.Tightness).Within(1e-5f));
            Assert.That(product, Is.Not.EqualTo(1f));
        }

        // --- per-tier physics --------------------------------------------------

        [Test]
        public void FrictionForTier_IsTheClampedSizeRampShapedByTheMaterialFeel()
        {
            for (var t = 1; t <= _table.MaterialCount; t++)
            {
                var mult = _table.MaterialForTier(t).Def.Physics.FrictionMult;
                var f = BallMath.FrictionForTier(_table, t);
                Assert.That(f, Is.GreaterThan(0f));
                // The band stays subtle: never past the ramp cap × the largest material factor.
                Assert.That(f, Is.LessThanOrEqualTo(Tuning.FrictionMax * 1.2f));
                Assert.That(f / mult, Is.LessThanOrEqualTo(Tuning.FrictionMax + 1e-6f));
            }
        }

        [Test]
        public void FrictionForTier_GrowsWithTierWithinOneMaterialFamily()
        {
            // Tiers 1–4 are all primitives with the same multiplier except wood (tier 1).
            Assert.That(BallMath.FrictionForTier(_table, 3), Is.GreaterThan(BallMath.FrictionForTier(_table, 2)));
            Assert.That(BallMath.FrictionForTier(_table, 4), Is.GreaterThan(BallMath.FrictionForTier(_table, 3)));
        }

        [Test]
        public void DensityForTier_KeepsTheFlatBaseDensityAtAndBelowTheTaperTier()
        {
            for (var t = 1; t <= Tuning.DensityTaperTier; t++)
            {
                Assert.That(BallMath.DensityForTier(_table, t), Is.EqualTo(Tuning.Density));
            }
        }

        [Test]
        public void DensityForTier_ReducesDensityAboveTheTaperTierAndNeverRaisesIt()
        {
            Assert.That(BallMath.DensityForTier(_table, Tuning.DensityTaperTier + 1), Is.LessThan(Tuning.Density));
            for (var t = Tuning.DensityTaperTier + 2; t <= 24; t++)
            {
                Assert.That(BallMath.DensityForTier(_table, t), Is.LessThan(BallMath.DensityForTier(_table, t - 1)));
                Assert.That(BallMath.DensityForTier(_table, t), Is.LessThan(Tuning.Density));
            }
        }

        [Test]
        public void DensityForTier_MakesMassGrowLinearlyWithRadiusAboveTheTaper()
        {
            // mass ∝ density · radius²; with the exp-1 taper mass/radius is constant above the
            // taper tier, so big-ball collision momentum stays in check.
            float MassPerRadius(int t) => BallMath.DensityForTier(_table, t) * _table.RadiusForTier(t);

            var reference = MassPerRadius(Tuning.DensityTaperTier + 1);
            for (var t = Tuning.DensityTaperTier + 2; t <= 24; t++)
            {
                Assert.That(MassPerRadius(t), Is.EqualTo(reference).Within(reference * 1e-4f));
            }
        }

        // --- vectors -----------------------------------------------------------

        [Test]
        public void ClampSpeed_LeavesAVelocityUnderTheCapUnchanged()
        {
            var v = new Vector2(3f, 4f); // speed 5
            Assert.That(BallMath.ClampSpeed(v, 16f), Is.EqualTo(v));
        }

        [Test]
        public void ClampSpeed_ScalesAnOverCapVelocityToExactlyTheCapPreservingDirection()
        {
            var capped = BallMath.ClampSpeed(new Vector2(30f, 40f), 10f); // speed 50 → ×0.2
            Assert.That(capped.magnitude, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(capped.x / capped.y, Is.EqualTo(30f / 40f).Within(1e-5f));
        }

        [Test]
        public void ClampSpeed_IsANoOpOnTheZeroVector() =>
            Assert.That(BallMath.ClampSpeed(Vector2.zero, 16f), Is.EqualTo(Vector2.zero));

        [Test]
        public void ClampSpeed_AntiTunnelInvariant_BaseCapSitsBelowTheBaseWallThickness()
        {
            // Mirrors the arena wall thickness (kept literal so a change there is a conscious
            // edit). Both the cap and the walls scale with the arena, so a per-step speed under
            // the cap can never cross a wall.
            const float wallThickness = 40f;
            Assert.That(Tuning.MaxBallSpeed, Is.LessThan(wallThickness));
        }

        [Test]
        public void ClampSpawnX_KeepsTheBallFullyInsideTheBounds()
        {
            Assert.That(BallMath.ClampSpawnX(-100f, 13f, 0f, Layout.Width), Is.EqualTo(13f));
            Assert.That(BallMath.ClampSpawnX(10_000f, 13f, 0f, Layout.Width), Is.EqualTo(Layout.Width - 13f));
            Assert.That(BallMath.ClampSpawnX(195f, 13f, 0f, Layout.Width), Is.EqualTo(195f));
        }

        [Test]
        public void BlastImpulse_PushesOutwardWithLinearFalloffAndDiesAtTheRadius()
        {
            var origin = new Vector2(100f, 100f);
            var atHalf = BallMath.BlastImpulse(new Vector2(130f, 100f), origin, 60f, 1.6f);
            Assert.That(atHalf.x, Is.EqualTo(0.8f).Within(1e-5f)); // half-way out = half strength
            Assert.That(atHalf.y, Is.EqualTo(0f).Within(1e-5f));

            Assert.That(BallMath.BlastImpulse(origin, origin, 60f, 1.6f), Is.EqualTo(Vector2.zero));
            Assert.That(BallMath.BlastImpulse(new Vector2(200f, 100f), origin, 60f, 1.6f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Midpoint_IsWhereAMergedBallIsBorn() =>
            Assert.That(BallMath.Midpoint(new Vector2(0f, 10f), new Vector2(20f, 30f)), Is.EqualTo(new Vector2(10f, 20f)));

        // --- death line --------------------------------------------------------

        [Test]
        public void RestAndOverflow_AccumulateOnlyWhileResting()
        {
            var rest = BallMath.NextRestMs(0f, 16f, true);
            rest = BallMath.NextRestMs(rest, 16f, true);
            Assert.That(rest, Is.EqualTo(32f));
            Assert.That(BallMath.NextRestMs(rest, 16f, false), Is.EqualTo(0f));

            Assert.That(BallMath.IsOverflow(Tuning.RestMs - 1f, Tuning.RestMs), Is.False);
            Assert.That(BallMath.IsOverflow(Tuning.RestMs, Tuning.RestMs), Is.True);
        }

        [Test]
        public void IsRestingAbove_RequiresBothAboveTheLineAndSlow()
        {
            Assert.That(BallMath.IsRestingAbove(100f, 0.1f, Tuning.DeathLineY, Tuning.RestSpeed), Is.True);
            Assert.That(BallMath.IsRestingAbove(100f, 5f, Tuning.DeathLineY, Tuning.RestSpeed), Is.False, "moving fast");
            Assert.That(BallMath.IsRestingAbove(200f, 0.1f, Tuning.DeathLineY, Tuning.RestSpeed), Is.False, "below the line");
        }

        [Test]
        public void IsNearDeath_FlagsOnlySlowBallsInsideTheWarningBand()
        {
            var line = Tuning.DeathLineY;
            Assert.That(BallMath.IsNearDeath(line + 1f, 0.1f, line, Tuning.WarnBand, Tuning.RestSpeed), Is.True);
            Assert.That(BallMath.IsNearDeath(line + Tuning.WarnBand, 0.1f, line, Tuning.WarnBand, Tuning.RestSpeed), Is.False,
                "outside the band");
            Assert.That(BallMath.IsNearDeath(line - 1f, 0.1f, line, Tuning.WarnBand, Tuning.RestSpeed), Is.False,
                "already over the line");
            Assert.That(BallMath.IsNearDeath(line + 1f, 5f, line, Tuning.WarnBand, Tuning.RestSpeed), Is.False, "still moving");
        }

        // --- trap-door target --------------------------------------------------

        private sealed class FakeBall : BallMath.IDoorCandidate
        {
            public Vector2 Position { get; set; }
            public float Radius { get; set; }
            public string Name;
        }

        [Test]
        public void NearestDoorBall_PicksByEdgeDistanceSoABiggerBallCanWin()
        {
            var small = new FakeBall { Name = "small", Position = new Vector2(200f, 400f), Radius = 17f };
            var big = new FakeBall { Name = "big", Position = new Vector2(200f, 380f), Radius = 99f };
            var picked = BallMath.NearestDoorBall(new List<FakeBall> { small, big }, 195f, 507f);
            Assert.That(picked, Is.SameAs(big), "the big ball's EDGE reaches nearer the door");
        }

        [Test]
        public void NearestDoorBall_IgnoresBallsBelowTheDoorAndReturnsNullWhenNoneQualify()
        {
            var below = new FakeBall { Position = new Vector2(200f, 600f), Radius = 17f };
            Assert.That(BallMath.NearestDoorBall(new List<FakeBall> { below }, 195f, 507f), Is.Null);
            Assert.That(BallMath.NearestDoorBall(new List<FakeBall>(), 195f, 507f), Is.Null);
        }
    }
}
