using System;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Data;

namespace RichCoast.Tests
{
    /// <summary>
    /// Ported from the original <c>core/Progression.test.ts</c>. These assertions encode the
    /// tuned progression curve — they are the safety net for the "faithful port" requirement.
    /// </summary>
    public class ProgressionTests
    {
        private Progression _progression;

        [SetUp]
        public void SetUp() => _progression = DefaultProgression.Create();

        // --- milestone progress ------------------------------------------------

        [Test]
        public void MilestoneProgress_StartsNearEmptyOnLevelOne() =>
            Assert.That(Progression.MilestoneProgress(1), Is.EqualTo(1f / Progression.MilestoneEvery).Within(1e-6));

        [Test]
        public void MilestoneProgress_IsNearlyFullOneLevelBeforeAMilestone() =>
            Assert.That(Progression.MilestoneProgress(Progression.MilestoneEvery - 1),
                Is.EqualTo((Progression.MilestoneEvery - 1) / (float)Progression.MilestoneEvery).Within(1e-6));

        [Test]
        public void MilestoneProgress_ResetsToEmptyTheLevelAMilestoneLands()
        {
            Assert.That(Progression.MilestoneProgress(Progression.MilestoneEvery), Is.EqualTo(0f));
            Assert.That(Progression.MilestoneProgress(Progression.MilestoneEvery * 3), Is.EqualTo(0f));
        }

        // --- palettes ----------------------------------------------------------

        [Test]
        public void Palette_StartsOnTheWorkshopPalette()
        {
            Assert.That(_progression.PaletteNameForLevel(1), Is.EqualTo("workshop"));
            Assert.That(_progression.PaletteNameForLevel(19), Is.EqualTo("workshop"));
        }

        [Test]
        public void Palette_SwapsAtEachAuthoredMilestoneStage()
        {
            Assert.That(_progression.PaletteNameForLevel(20), Is.EqualTo("dusk"));
            Assert.That(_progression.PaletteNameForLevel(39), Is.EqualTo("dusk"));
            Assert.That(_progression.PaletteNameForLevel(40), Is.EqualTo("night"));
            Assert.That(_progression.PaletteNameForLevel(60), Is.EqualTo("dawn"));
            Assert.That(_progression.PaletteNameForLevel(80), Is.EqualTo("gilded"));
        }

        [Test]
        public void Palette_HoldsTheLastAuthoredPaletteForever()
        {
            Assert.That(_progression.PaletteNameForLevel(81), Is.EqualTo("gilded"));
            Assert.That(_progression.PaletteNameForLevel(500), Is.EqualTo("gilded"));
        }

        // --- score-bar target --------------------------------------------------

        [Test]
        public void ScoreBarTarget_ReturnsTheAuthoredTargetExactlyAtEachAnchor()
        {
            Assert.That(_progression.ScoreBarTargetForLevel(1), Is.EqualTo(20));
            Assert.That(_progression.ScoreBarTargetForLevel(4), Is.EqualTo(200));
            Assert.That(_progression.ScoreBarTargetForLevel(20), Is.EqualTo(5_000));
            Assert.That(_progression.ScoreBarTargetForLevel(40), Is.EqualTo(400_000));
            Assert.That(_progression.ScoreBarTargetForLevel(80), Is.EqualTo(2_700_000_000d));
        }

        [Test]
        public void ScoreBarTarget_InterpolatesGeometricallyBetweenAnchors()
        {
            // Anchors are NOT plateaus: every level's bar is bigger than the last, so a
            // multi-level roll-through burst self-limits instead of wrapping a flat target.
            for (var level = 2; level <= 80; level++)
            {
                Assert.That(_progression.ScoreBarTargetForLevel(level),
                    Is.GreaterThan(_progression.ScoreBarTargetForLevel(level - 1)),
                    $"target must grow at level {level}");
            }

            // Spot-check the constant per-level ratio inside the L4→L20 span.
            var ratio = Math.Pow(5_000d / 200d, 1d / 16d);
            Assert.That(_progression.ScoreBarTargetForLevel(5), Is.EqualTo(Math.Round(200 * ratio)));
        }

        [Test]
        public void ScoreBarTarget_KeepsGrowingPastTheLastAuthoredStage()
        {
            var last = _progression.ScoreBarTargetForLevel(80);
            Assert.That(_progression.ScoreBarTargetForLevel(81),
                Is.EqualTo(last * Progression.TailTargetGrowth).Within(last * 1e-9));
            Assert.That(_progression.ScoreBarTargetForLevel(81), Is.GreaterThan(last));
            // Strictly increasing far into the tail — the flat-forever bug.
            Assert.That(_progression.ScoreBarTargetForLevel(500),
                Is.GreaterThan(_progression.ScoreBarTargetForLevel(499)));
        }

        [Test]
        public void ScoreBarTarget_TailRateContinuesTheAuthoredCurve()
        {
            // Ball values grow 3^4 per window shift every milestone span, and the authored anchors
            // track that. The tail keeps the same per-level rate so one good drain stays worth
            // ~one level far into the tail.
            Assert.That(Math.Pow(Progression.TailTargetGrowth, Progression.MilestoneEvery),
                Is.EqualTo(Math.Pow(3, 4)).Within(1e-8));
            Assert.That(
                _progression.ScoreBarTargetForLevel(80 + Progression.MilestoneEvery) / _progression.ScoreBarTargetForLevel(80),
                Is.EqualTo(Math.Pow(3, 4)).Within(0.01));
        }

        // --- ball supply -------------------------------------------------------

        [Test]
        public void BufferForLevel_StartsLeanThenOscillatesByParity()
        {
            Assert.That(Progression.BufferForLevel(1), Is.EqualTo(8));
            Assert.That(Progression.BufferForLevel(2), Is.EqualTo(17)); // harvest
            Assert.That(Progression.BufferForLevel(3), Is.EqualTo(13)); // pressure
            Assert.That(Progression.BufferForLevel(4), Is.EqualTo(17));
            Assert.That(Progression.BufferForLevel(5), Is.EqualTo(13));
            Assert.That(Progression.BufferForLevel(10), Is.EqualTo(18));
            Assert.That(Progression.BufferForLevel(11), Is.EqualTo(14));
        }

        [Test]
        public void BufferForLevel_AlwaysPaysTheHarvestAmountOnMilestones()
        {
            foreach (var level in new[] { 20, 40, 80 })
            {
                Assert.That(Progression.BufferForLevel(level), Is.GreaterThan(Progression.BufferForLevel(level - 1)),
                    $"milestone level {level}");
            }
            Assert.That(Progression.BufferForLevel(20), Is.EqualTo(19));
        }

        [Test]
        public void BufferForLevel_CapsForeverSoDroppingNeverBecomesAChore()
        {
            Assert.That(Progression.BufferForLevel(30), Is.EqualTo(20));
            Assert.That(Progression.BufferForLevel(31), Is.EqualTo(16));
            for (var level = 2; level <= 500; level++)
            {
                Assert.That(Progression.BufferForLevel(level), Is.InRange(8, 20));
            }
        }

        // --- draw window -------------------------------------------------------

        [Test]
        public void WindowForLevel_ServesTheAuthoredWindowThroughTheLastStage()
        {
            AssertWindow(_progression.WindowForLevel(1), 1, 1);
            AssertWindow(_progression.WindowForLevel(4), 1, 4);
            AssertWindow(_progression.WindowForLevel(20), 5, 8);
            AssertWindow(_progression.WindowForLevel(80), 17, 20);
            AssertWindow(_progression.WindowForLevel(99), 17, 20);
        }

        [Test]
        public void WindowForLevel_StepsPlusTwoPerTailMilestoneHoldingBetweenThem()
        {
            AssertWindow(_progression.WindowForLevel(100), 19, 22);
            AssertWindow(_progression.WindowForLevel(119), 19, 22);
            AssertWindow(_progression.WindowForLevel(120), 21, 24);
            AssertWindow(_progression.WindowForLevel(200), 17 + 2 * 6, 20 + 2 * 6);
        }

        [Test]
        public void WindowFloor_OnlyShiftsOnMilestoneLevels()
        {
            // The milestone reads the new window's floor as its blacklist threshold, so a floor
            // that moved off-milestone would desync the arena scale and blacklist from the pool.
            for (var level = 2; level <= 400; level++)
            {
                var prev = _progression.WindowForLevel(level - 1);
                var window = _progression.WindowForLevel(level);
                if (window.Min != prev.Min)
                {
                    Assert.That(level % Progression.MilestoneEvery, Is.EqualTo(0), $"floor shift at level {level}");
                }
            }
        }

        // --- anti-soft-lock guard ---------------------------------------------

        [Test]
        public void EveryLevelUpToOneHundredIsCrossableWithThatLevelsSupplyAlone()
        {
            // Conservative model of one A-phase → B-phase cycle with NO carried-over board balls:
            // N = BufferForLevel(level) balls of the window's MAX tier; perfect pair-merging
            // multiplies total board value ×1.5 per full pairing round (a merge turns 2V into 3V);
            // every ball then drains through a deliberately low ×4 gate cascade (typical cascades
            // run ×8–14). If this ever dips below the target, the curve has authored a level that
            // can soft-lock a run into the stalemate game-over. Deeper in the tail the target
            // outruns the +2-per-milestone supply by design — that wall IS the endgame — so the
            // guard stops at the first tail window.
            for (var level = 1; level <= 100; level++)
            {
                var n = Progression.BufferForLevel(level);
                var maxTierValue = Math.Pow(3, _progression.WindowForLevel(level).Max - 1);
                var rounds = Math.Floor(Math.Log(n, 2));
                var mergedBoardValue = n * maxTierValue * Math.Pow(1.5, rounds);
                var drained = mergedBoardValue * 4;
                Assert.That(drained, Is.GreaterThanOrEqualTo(_progression.ScoreBarTargetForLevel(level)),
                    $"level {level} unreachable");
            }
        }

        private static void AssertWindow(TierWindow window, int min, int max)
        {
            Assert.That(window.Min, Is.EqualTo(min), $"window min ({window})");
            Assert.That(window.Max, Is.EqualTo(max), $"window max ({window})");
        }
    }
}
