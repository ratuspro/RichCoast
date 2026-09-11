using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class TierMathTests
    {
        [Test]
        public void ValueIsPowersOfThree()
        {
            Assert.AreEqual(1, TierMath.ValueForTier(1));
            Assert.AreEqual(3, TierMath.ValueForTier(2));
            Assert.AreEqual(9, TierMath.ValueForTier(3));
            Assert.AreEqual(27, TierMath.ValueForTier(4));
            Assert.AreEqual(531441, TierMath.ValueForTier(13));
        }

        [Test]
        public void MergingTwoEqualBallsTriplesTheValue()
        {
            for (int t = 1; t < 30; t++)
            {
                double merged = TierMath.ValueForTier(MergeLogic.MergedTier(t));
                Assert.AreEqual(3 * TierMath.ValueForTier(t), merged, merged * 1e-12, $"tier {t}");
            }
        }
    }

    public class MergeLogicTests
    {
        [Test]
        public void CanMergeIsTrueForEqualTiers() => Assert.IsTrue(MergeLogic.CanMerge(3, 3));

        [Test]
        public void CanMergeIsFalseForDifferentTiers() => Assert.IsFalse(MergeLogic.CanMerge(2, 3));

        [Test]
        public void StillMergesAtAndBeyondTheBaseTable()
        {
            Assert.IsTrue(MergeLogic.CanMerge(TierMath.TierCount, TierMath.TierCount));
            Assert.IsTrue(MergeLogic.CanMerge(TierMath.TierCount + 5, TierMath.TierCount + 5));
        }

        [Test]
        public void MergedTierStepsUpExactlyOne()
        {
            Assert.AreEqual(2, MergeLogic.MergedTier(1));
            Assert.AreEqual(6, MergeLogic.MergedTier(5));
            Assert.AreEqual(TierMath.TierCount + 1, MergeLogic.MergedTier(TierMath.TierCount));
            Assert.AreEqual(TierMath.TierCount + 8, MergeLogic.MergedTier(TierMath.TierCount + 7));
        }
    }

    public class NumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(981, "981")]
        [TestCase(999, "999")]
        [TestCase(1000, "1K")]
        [TestCase(1594, "1.6K")]
        [TestCase(19683, "20K")]
        [TestCase(531441, "531K")]
        [TestCase(1594323, "1.6M")]
        [TestCase(2700000000, "2.7B")]
        [TestCase(33000000, "33M")]
        [TestCase(1.5e17, "150Qa")]
        [TestCase(1.5e32, "150No")]
        public void CompactMatchesTheWebBuild(double value, string expected) =>
            Assert.AreEqual(expected, NumberFormat.Compact(value));

        [Test]
        public void FallsBackToExponentFormBeyondTheUnitTable()
        {
            var s = NumberFormat.Compact(4e34);
            Assert.AreEqual("4e34", s);
        }
    }
}
