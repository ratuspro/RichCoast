using System;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class ProgressionTests
    {
        readonly ProgressionCurve curve = ProgressionCurve.Default;
        const int M = ProgressionCurve.MilestoneEvery;

        [Test]
        public void MilestoneProgressTracksTheWindow()
        {
            Assert.AreEqual(1.0 / M, ProgressionCurve.MilestoneProgress(1), 1e-9);
            Assert.AreEqual((M - 1.0) / M, ProgressionCurve.MilestoneProgress(M - 1), 1e-9);
            Assert.AreEqual(0, ProgressionCurve.MilestoneProgress(M));
            Assert.AreEqual(0, ProgressionCurve.MilestoneProgress(M * 3));
            Assert.AreEqual(10.0 / M, ProgressionCurve.MilestoneProgress(M * 2 + 10), 1e-9);
        }

        [Test]
        public void PaletteIsAuthorThenHold()
        {
            Assert.AreEqual("workshop", curve.PaletteNameForLevel(1));
            Assert.AreEqual("workshop", curve.PaletteNameForLevel(19));
            Assert.AreEqual("dusk", curve.PaletteNameForLevel(20));
            Assert.AreEqual("dusk", curve.PaletteNameForLevel(39));
            Assert.AreEqual("night", curve.PaletteNameForLevel(40));
            Assert.AreEqual("dawn", curve.PaletteNameForLevel(60));
            Assert.AreEqual("gilded", curve.PaletteNameForLevel(80));
            Assert.AreEqual("gilded", curve.PaletteNameForLevel(500));
        }

        [Test]
        public void ScoreBarTargetHitsTheAnchorsExactly()
        {
            Assert.AreEqual(20, curve.ScoreBarTargetForLevel(1));
            Assert.AreEqual(200, curve.ScoreBarTargetForLevel(4));
            Assert.AreEqual(5_000, curve.ScoreBarTargetForLevel(20));
            Assert.AreEqual(400_000, curve.ScoreBarTargetForLevel(40));
            Assert.AreEqual(2_700_000_000, curve.ScoreBarTargetForLevel(80));
        }

        [Test]
        public void ScoreBarTargetInterpolatesGeometricallyWithNoPlateaus()
        {
            for (int level = 2; level <= 80; level++)
                Assert.Greater(curve.ScoreBarTargetForLevel(level), curve.ScoreBarTargetForLevel(level - 1), $"level {level}");
            double ratio = Math.Pow(5_000.0 / 200.0, 1.0 / 16);
            Assert.AreEqual(Math.Round(200 * ratio), curve.ScoreBarTargetForLevel(5));
        }

        [Test]
        public void ScoreBarTargetKeepsGrowingInTheTail()
        {
            double last = curve.ScoreBarTargetForLevel(80);
            Assert.AreEqual(last * ProgressionCurve.TailTargetGrowth, curve.ScoreBarTargetForLevel(81), 1);
            Assert.Greater(curve.ScoreBarTargetForLevel(500), curve.ScoreBarTargetForLevel(499));
            Assert.AreEqual(Math.Pow(3, 4), Math.Pow(ProgressionCurve.TailTargetGrowth, M), 1e-8);
            Assert.AreEqual(Math.Pow(3, 4), curve.ScoreBarTargetForLevel(80 + M) / curve.ScoreBarTargetForLevel(80), 1e-2);
        }

        [Test]
        public void BufferOscillatesHarvestPressureByParity()
        {
            Assert.AreEqual(8, ProgressionCurve.BufferForLevel(1));
            Assert.AreEqual(17, ProgressionCurve.BufferForLevel(2));
            Assert.AreEqual(13, ProgressionCurve.BufferForLevel(3));
            Assert.AreEqual(17, ProgressionCurve.BufferForLevel(4));
            Assert.AreEqual(13, ProgressionCurve.BufferForLevel(5));
            Assert.AreEqual(18, ProgressionCurve.BufferForLevel(10));
            Assert.AreEqual(14, ProgressionCurve.BufferForLevel(11));
            Assert.AreEqual(19, ProgressionCurve.BufferForLevel(20));
            foreach (int level in new[] { 20, 40, 80 })
                Assert.Greater(ProgressionCurve.BufferForLevel(level), ProgressionCurve.BufferForLevel(level - 1));
            Assert.AreEqual(20, ProgressionCurve.BufferForLevel(30));
            Assert.AreEqual(16, ProgressionCurve.BufferForLevel(31));
            for (int level = 2; level <= 500; level++)
            {
                Assert.LessOrEqual(ProgressionCurve.BufferForLevel(level), 20);
                Assert.GreaterOrEqual(ProgressionCurve.BufferForLevel(level), 8);
            }
        }

        [Test]
        public void WindowFollowsAuthoredStagesThenStepsInTheTail()
        {
            Assert.AreEqual((1, 1), curve.WindowForLevel(1));
            Assert.AreEqual((1, 4), curve.WindowForLevel(4));
            Assert.AreEqual((5, 8), curve.WindowForLevel(20));
            Assert.AreEqual((17, 20), curve.WindowForLevel(80));
            Assert.AreEqual((17, 20), curve.WindowForLevel(99));
            Assert.AreEqual((19, 22), curve.WindowForLevel(100));
            Assert.AreEqual((19, 22), curve.WindowForLevel(119));
            Assert.AreEqual((21, 24), curve.WindowForLevel(120));
            Assert.AreEqual((17 + 12, 20 + 12), curve.WindowForLevel(200));
        }

        [Test]
        public void WindowFloorOnlyShiftsOnMilestoneLevels()
        {
            for (int level = 2; level <= 400; level++)
            {
                if (curve.WindowForLevel(level).min != curve.WindowForLevel(level - 1).min)
                    Assert.AreEqual(0, level % M, $"floor shift at level {level}");
            }
        }

        [Test]
        public void EveryLevelTo100IsReachableWithItsOwnRefill_AntiSoftLockGuard()
        {
            // Conservative model of one A→B cycle with NO carried-over board balls: N balls of the
            // window's MAX tier, perfectly pair-merged (×1.5 per pairing round), drained through a
            // pessimistic ×4 gate cascade. If this ever dips below the target, the curve authored a
            // level that can soft-lock a run into the stalemate game-over.
            for (int level = 1; level <= 100; level++)
            {
                int n = ProgressionCurve.BufferForLevel(level);
                double maxTierValue = Math.Pow(3, curve.WindowForLevel(level).max - 1);
                double merged = n * maxTierValue * Math.Pow(1.5, Math.Floor(Math.Log(n, 2)));
                double drained = merged * 4;
                Assert.GreaterOrEqual(drained, curve.ScoreBarTargetForLevel(level), $"level {level} unreachable");
            }
        }
    }

    public class ComboPitchTests
    {
        const double Window = 500;

        [Test]
        public void FirstTriggerStartsAtStepZeroUnityPitch()
        {
            var s = new ComboState();
            var r = ComboPitch.Next(s, 1000, Window);
            Assert.AreEqual(0, r.Step);
            Assert.AreEqual(1, r.Mult);
        }

        [Test]
        public void SecondTriggerInsideTheWindowStepsUpASemitone()
        {
            var s = new ComboState();
            ComboPitch.Next(s, 1000, Window);
            var r = ComboPitch.Next(s, 1000 + Window - 1, Window);
            Assert.AreEqual(1, r.Step);
            Assert.AreEqual(Math.Pow(2, 1.0 / 12), r.Mult, 1e-9);
        }

        [Test]
        public void ChainClimbsWhileInsideTheWindowAndResetsOnAGap()
        {
            var s = new ComboState();
            double now = 0;
            for (int i = 0; i < 4; i++) { ComboPitch.Next(s, now, Window); now += 100; }
            Assert.AreEqual(3, s.Step);
            var r = ComboPitch.Next(s, now - 100 + Window, Window); // exactly a window away → reset
            Assert.AreEqual(0, r.Step);
            Assert.AreEqual(1, r.Mult);
        }

        [Test]
        public void StepIsCappedSoLongChainsDoNotGetShrill()
        {
            var s = new ComboState();
            double now = 0;
            for (int i = 0; i < 50; i++) { ComboPitch.Next(s, now, Window, 8); now += 50; }
            Assert.AreEqual(8, s.Step);
        }
    }

    public class BallQueueTests
    {
        [Test]
        public void SeededHandIsConsumedBeforeRandomDraws()
        {
            var q = new BallQueue((lo, hi) => 99);
            q.Seed(new[] { 1, 2, 3 });
            Assert.AreEqual(1, q.CurrentTier);
            Assert.AreEqual(2, q.NextTier);
            Assert.AreEqual(1, q.Pop());
            Assert.AreEqual(2, q.CurrentTier);
            Assert.AreEqual(3, q.NextTier);
            q.Pop();
            Assert.AreEqual(3, q.CurrentTier);
            Assert.AreEqual(99, q.NextTier);
        }

        [Test]
        public void RandomDrawsRespectTheWindow()
        {
            var windows = new System.Collections.Generic.List<(int, int)>();
            var q = new BallQueue((lo, hi) => { windows.Add((lo, hi)); return lo; });
            windows.Clear(); // the constructor draws from the default [1,4] window
            q.SetWindow(5, 8);
            q.Reroll();
            Assert.AreEqual(6 - 1, q.CurrentTier);
            Assert.AreEqual(5, q.NextTier);
            Assert.AreEqual(2, windows.Count);
            foreach (var w in windows) Assert.AreEqual((5, 8), w);
        }

        [Test]
        public void RerollClearsAnUnconsumedSeed()
        {
            var q = new BallQueue((lo, hi) => 7);
            q.Seed(new[] { 1, 1, 1, 1 });
            q.Reroll();
            Assert.AreEqual(7, q.CurrentTier);
            Assert.AreEqual(7, q.NextTier);
            Assert.AreEqual(7, q.Pop());
            Assert.AreEqual(7, q.NextTier);
        }
    }

    public class GameEventsTests
    {
        [SetUp, TearDown]
        public void Clear() => GameEvents.Reset();

        [Test]
        public void TypedEventsDeliverPayloadsAndResetDropsSubscribers()
        {
            int got = -1;
            GameEvents.BallBufferChanged += c => got = c;
            GameEvents.RaiseBallBufferChanged(7);
            Assert.AreEqual(7, got);
            GameEvents.Reset();
            GameEvents.RaiseBallBufferChanged(9);
            Assert.AreEqual(7, got);
        }
    }
}
