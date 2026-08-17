using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>Ported from the original <c>zoneB/ScoreBar.test.ts</c>.</summary>
    public class ScoreBarTests
    {
        /// <summary>
        /// Drives the bar the way Zone B does live: add points, then consume one level per
        /// crossing, updating the target between crossings (as Zone A does via ProgressionChanged).
        /// Returns how many levels were crossed.
        /// </summary>
        private static int ConsumeCrossings(ScoreBar bar, System.Func<double> nextTarget)
        {
            var levels = 0;
            while (bar.CrossedTarget())
            {
                bar.ConsumeLevel();
                levels += 1;
                bar.SetTarget(nextTarget());
            }
            return levels;
        }

        [Test]
        public void AccumulatesPointsWithoutCrossing()
        {
            var bar = new ScoreBar(10);
            bar.Add(4);
            Assert.That(bar.Filled, Is.EqualTo(4));
            Assert.That(bar.CrossedTarget(), Is.False);
        }

        [Test]
        public void CrossedTargetIsTrueAtOrAboveTheTarget()
        {
            var bar = new ScoreBar(10);
            bar.Add(10);
            Assert.That(bar.CrossedTarget(), Is.True);
        }

        [Test]
        public void ConsumeLevelSubtractsTheCurrentTargetFromFilled()
        {
            var bar = new ScoreBar(10);
            bar.Add(13);
            bar.ConsumeLevel();
            Assert.That(bar.Filled, Is.EqualTo(3));
        }

        [Test]
        public void ASingleAddCanCrossSeveralLevelsLandingTheExactRemainder()
        {
            var bar = new ScoreBar(4); // level-1 target
            bar.Add(50);
            var targets = new double[] { 30, 40, 55 }; // successive level targets
            var i = 0;
            var levels = ConsumeCrossings(bar, () => targets[i++]);
            // 50 - 4 = 46 (L2); 46 - 30 = 16 (L3); 16 < 40 → stop.
            Assert.That(levels, Is.EqualTo(2));
            Assert.That(bar.Filled, Is.EqualTo(16));
            Assert.That(bar.Target, Is.EqualTo(40));
        }

        [Test]
        public void ExactFillLandsEmptyOnTheNextLevel()
        {
            var bar = new ScoreBar(10);
            bar.Add(10);
            var levels = ConsumeCrossings(bar, () => 30);
            Assert.That(levels, Is.EqualTo(1));
            Assert.That(bar.Filled, Is.EqualTo(0));
            Assert.That(bar.CrossedTarget(), Is.False);
        }

        [Test]
        public void ProgressReflectsTheCurrentFillAgainstTheCurrentTarget()
        {
            var bar = new ScoreBar(20);
            bar.Add(5);
            Assert.That(bar.Progress, Is.EqualTo(0.25).Within(1e-9));
        }

        [Test]
        public void ForfeitOverflowDropsAnOverTargetFillToJustUnderFull()
        {
            // The safety valve for a freak monster drain: the caller stops consuming levels at its
            // cap and forfeits the rest, leaving a nearly-full bar (not a crossed one).
            var bar = new ScoreBar(100);
            bar.Add(1_000_000);
            bar.ForfeitOverflow();
            Assert.That(bar.CrossedTarget(), Is.False);
            Assert.That(bar.Progress, Is.EqualTo(0.99).Within(1e-9));
        }

        [Test]
        public void ForfeitOverflowLeavesAFillWellBelowTheTargetAlone()
        {
            var bar = new ScoreBar(100);
            bar.Add(42);
            bar.ForfeitOverflow();
            Assert.That(bar.Filled, Is.EqualTo(42));
        }
    }
}
