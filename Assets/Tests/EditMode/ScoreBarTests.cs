using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class ScoreBarTests
    {
        [Test]
        public void AccumulatesTowardTarget()
        {
            var bar = new ScoreBar(20);
            bar.Add(5);
            bar.Add(6);
            Assert.AreEqual(11, bar.Filled);
            Assert.IsFalse(bar.CrossedTarget);
            Assert.AreEqual(0.55, bar.Progress, 1e-9);
        }

        [Test]
        public void ConsumesLevelsOneAtATimeAgainstTheirOwnTarget()
        {
            var bar = new ScoreBar(20);
            bar.Add(110); // 20 → 80 → 130 anchors: 110 crosses the first two
            Assert.IsTrue(bar.CrossedTarget);
            bar.ConsumeLevel();
            bar.SetTarget(80);
            Assert.AreEqual(90, bar.Filled);
            Assert.IsTrue(bar.CrossedTarget);
            bar.ConsumeLevel();
            bar.SetTarget(130);
            Assert.AreEqual(10, bar.Filled);
            Assert.IsFalse(bar.CrossedTarget);
        }

        [Test]
        public void ForfeitOverflowLeavesNearlyFullBar()
        {
            var bar = new ScoreBar(100);
            bar.Add(5000);
            bar.ForfeitOverflow();
            Assert.AreEqual(99, bar.Filled, 1e-9);
            Assert.IsFalse(bar.CrossedTarget);
            var low = new ScoreBar(100);
            low.Add(40);
            low.ForfeitOverflow();
            Assert.AreEqual(40, low.Filled);
        }

        [Test]
        public void RejectsNonPositiveTargets()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ScoreBar(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ScoreBar(10).SetTarget(-1));
        }
    }
}
