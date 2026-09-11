using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class TierLadderTests
    {
        readonly TierLadder ladder = TierLadder.Default;

        [Test]
        public void RadiusTableHasOneEntryPerBaseTier()
        {
            Assert.AreEqual(TierMath.TierCount, ladder.Radii.Length);
            Assert.GreaterOrEqual(Materials.Count, TierMath.TierCount);
        }

        [Test]
        public void SpawnRowSitsAboveTheDeathLineAndClearsTheCeiling()
        {
            Assert.Less(DesignSpace.SpawnY, DesignSpace.DeathLineY);
            Assert.Less(ladder.RadiusForTier(4), DesignSpace.SpawnY);
            Assert.Less(ladder.RadiusForTier(TierMath.TierCount) * 2, DesignSpace.Width);
        }

        [Test]
        public void RadiusIsStrictlyIncreasingAndUnbounded()
        {
            for (int t = 2; t <= TierMath.TierCount; t++)
                Assert.Greater(ladder.RadiusForTier(t), ladder.RadiusForTier(t - 1));
            Assert.Greater(ladder.RadiusForTier(TierMath.TierCount + 1), ladder.RadiusForTier(TierMath.TierCount));
            Assert.Greater(ladder.RadiusForTier(TierMath.TierCount + 5), ladder.RadiusForTier(TierMath.TierCount + 4));
            Assert.AreEqual(ladder.RadiusForTier(1), ladder.RadiusForTier(0));
        }

        [Test]
        public void NeutralGrowthMatchesTheWindowMaxRadiusRatio()
        {
            Assert.AreEqual(71.0 / 34.0, ladder.NeutralGrowth(4, 8), 1e-10);
            Assert.AreEqual(System.Math.Pow(1.18, 4), ladder.NeutralGrowth(12, 16), 1e-10);
            Assert.AreEqual(1, ladder.NeutralGrowth(20, 20));
        }

        [Test]
        public void MilestoneZoomFactorRules()
        {
            Assert.AreEqual(1, ladder.MilestoneZoomFactor(21, (1, 4), (5, 8), 0.92));
            Assert.AreEqual(1, ladder.MilestoneZoomFactor(100, (17, 20), (17, 20), 1.05));
            Assert.AreEqual(71.0 / 34.0 * 0.92, ladder.MilestoneZoomFactor(20, (1, 4), (5, 8), 0.92), 1e-10);
            Assert.AreEqual(71.0 / 34.0, ladder.MilestoneZoomFactor(20, (1, 4), (5, 8), null), 1e-10);
            Assert.AreEqual(ladder.TailMilestoneZoom, ladder.MilestoneZoomFactor(100, (17, 20), (19, 22), null, tail: true));
            Assert.Less(ladder.TailMilestoneZoom, ladder.NeutralGrowth(20, 22));
            Assert.Greater(ladder.TailMilestoneZoom, 1);
        }

        [Test]
        public void RollThroughZoomFactorsComposeByProduct()
        {
            var curve = ProgressionCurve.Default;
            double f40 = ladder.MilestoneZoomFactor(40, curve.WindowForLevel(39), curve.WindowForLevel(40), curve.GetStage(40).Tightness);
            double f41 = ladder.MilestoneZoomFactor(41, curve.WindowForLevel(40), curve.WindowForLevel(41), curve.GetStage(41).Tightness);
            double product = f40 * f41;
            var s40 = curve.GetStage(40);
            Assert.AreEqual(ladder.NeutralGrowth(curve.WindowForLevel(39).max, s40.WindowMax) * (s40.Tightness ?? 1), product, 1e-10);
            Assert.AreNotEqual(1, product);
        }

        [Test]
        public void FrictionIsTheClampedRampShapedByMaterialFeel()
        {
            for (int t = 1; t <= Materials.Count; t++)
            {
                double mult = Materials.ForTier(t).Def.Physics.FrictionMult;
                double f = ladder.FrictionForTier(t);
                Assert.Greater(f, 0);
                Assert.LessOrEqual(f, ladder.FrictionMax * 1.2);
                Assert.LessOrEqual(f / mult, ladder.FrictionMax + 1e-12);
            }
            Assert.Greater(ladder.FrictionForTier(3), ladder.FrictionForTier(2));
            Assert.Greater(ladder.FrictionForTier(4), ladder.FrictionForTier(3));
        }

        [Test]
        public void DensityTapersAboveTheTaperTierOnly()
        {
            for (int t = 1; t <= ladder.DensityTaperTier; t++)
                Assert.AreEqual(ladder.Density, ladder.DensityForTier(t));
            Assert.Less(ladder.DensityForTier(ladder.DensityTaperTier + 1), ladder.Density);
            for (int t = ladder.DensityTaperTier + 2; t <= 24; t++)
            {
                Assert.Less(ladder.DensityForTier(t), ladder.DensityForTier(t - 1));
                Assert.Less(ladder.DensityForTier(t), ladder.Density);
            }
        }

        [Test]
        public void MassGrowsLinearlyWithRadiusAboveTheTaper()
        {
            double MassPerRadius(int t) => ladder.DensityForTier(t) * ladder.RadiusForTier(t) * ladder.RadiusForTier(t) / ladder.RadiusForTier(t);
            double reference = MassPerRadius(ladder.DensityTaperTier + 1);
            for (int t = ladder.DensityTaperTier + 2; t <= 24; t++)
                Assert.AreEqual(reference, MassPerRadius(t), 1e-6);
        }
    }

    public class BallMathTests
    {
        [Test]
        public void ClampSpawnXKeepsTheBallFullyInside()
        {
            Assert.AreEqual(13, BallMath.ClampSpawnX(-100, 13, 0, DesignSpace.Width));
            Assert.AreEqual(DesignSpace.Width - 13, BallMath.ClampSpawnX(10_000, 13, 0, DesignSpace.Width));
            Assert.AreEqual(195, BallMath.ClampSpawnX(195, 13, 0, DesignSpace.Width));
        }

        [Test]
        public void MidpointAveragesCoordinates() =>
            Assert.AreEqual(new Vec2(5, 10), BallMath.Midpoint(new Vec2(0, 0), new Vec2(10, 20)));

        [Test]
        public void BlastIsZeroAtOriginAndBeyondRadius()
        {
            var origin = new Vec2(5, 5);
            Assert.AreEqual(Vec2.Zero, BallMath.BlastImpulse(origin, origin, 60, 1.6));
            Assert.AreEqual(Vec2.Zero, BallMath.BlastImpulse(new Vec2(65, 5), origin, 60, 1.6));
        }

        [Test]
        public void BlastPointsAwayWithLinearFalloff()
        {
            var near = BallMath.BlastImpulse(new Vec2(10, 0), Vec2.Zero, 60, 1.6);
            var far = BallMath.BlastImpulse(new Vec2(50, 0), Vec2.Zero, 60, 1.6);
            Assert.Greater(near.X, 0);
            Assert.AreEqual(0, near.Y);
            Assert.Greater(near.X, far.X);
            Assert.AreEqual(1.6 * (1 - 10.0 / 60.0), near.X, 1e-12);
        }

        [Test]
        public void ClampSpeedCapsMagnitudeAndKeepsDirection()
        {
            var v = new Vec2(3, 4);
            Assert.AreEqual(v, BallMath.ClampSpeed(v, 16));
            var capped = BallMath.ClampSpeed(new Vec2(30, 40), 10);
            Assert.AreEqual(10, capped.Length, 1e-10);
            Assert.AreEqual(30.0 / 40.0, capped.X / capped.Y, 1e-10);
            Assert.AreEqual(Vec2.Zero, BallMath.ClampSpeed(Vec2.Zero, 16));
        }

        [Test]
        public void RestTimeAccumulatesWhileRestingAndResetsOtherwise()
        {
            Assert.AreEqual(116, BallMath.NextRestMs(100, 16, true));
            Assert.AreEqual(0, BallMath.NextRestMs(100, 16, false));
        }

        [Test]
        public void RestingAboveNeedsBothAboveAndSlow()
        {
            double line = DesignSpace.DeathLineY;
            Assert.IsTrue(BallMath.IsRestingAbove(50, 0.2, line, 0.8));
            Assert.IsFalse(BallMath.IsRestingAbove(50, 2.0, line, 0.8));
            Assert.IsFalse(BallMath.IsRestingAbove(150, 0.2, line, 0.8));
        }

        [Test]
        public void OverflowOnceThresholdReached()
        {
            Assert.IsFalse(BallMath.IsOverflow(999, 1000));
            Assert.IsTrue(BallMath.IsOverflow(1000, 1000));
        }

        [Test]
        public void NearDeathFlagsOnlySlowBallsInsideTheBand()
        {
            double line = DesignSpace.DeathLineY, band = DesignSpace.WarnBand;
            Assert.IsTrue(BallMath.IsNearDeath(line + 1, 0.2, line, band, 0.8));
            Assert.IsTrue(BallMath.IsNearDeath(line, 0.2, line, band, 0.8));
            Assert.IsFalse(BallMath.IsNearDeath(line - 1, 0.2, line, band, 0.8));
            Assert.IsFalse(BallMath.IsNearDeath(line + band, 0.2, line, band, 0.8));
            Assert.IsFalse(BallMath.IsNearDeath(line + 1, 2.0, line, band, 0.8));
        }
    }

    public class MaterialsTests
    {
        [Test]
        public void LadderHasTwentyMaterialsInFiveFamiliesOfFour()
        {
            Assert.AreEqual(20, Materials.Count);
            var families = new[] { MaterialFamily.Primitive, MaterialFamily.Metal, MaterialFamily.Precious, MaterialFamily.Gem, MaterialFamily.Exotic };
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(families[i / 4], Materials.Ladder[i].Family, Materials.Ladder[i].Name);
        }

        [Test]
        public void TiersWrapPastTheLadderWithACycleCount()
        {
            Assert.AreEqual("Wood", Materials.ForTier(1).Def.Name);
            Assert.AreEqual(0, Materials.ForTier(1).Cycle);
            Assert.AreEqual("Antimatter", Materials.ForTier(20).Def.Name);
            Assert.AreEqual("Wood", Materials.ForTier(21).Def.Name);
            Assert.AreEqual(1, Materials.ForTier(21).Cycle);
            Assert.AreEqual("Wood", Materials.ForTier(0).Def.Name);
        }

        [Test]
        public void GoldIsTheDensestAndGemsSlip()
        {
            Assert.AreEqual(1.3, Materials.ForTier(9).Def.Physics.DensityMult);
            Assert.AreEqual(0.5, Materials.ForTier(13).Def.Physics.FrictionMult);
            Assert.AreEqual(1.2, Materials.ForTier(1).Def.Physics.RestitutionMult); // wood bounces a touch
        }
    }
}
