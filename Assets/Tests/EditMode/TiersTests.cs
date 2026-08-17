using System;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>Ported from the original <c>core/contracts.test.ts</c>, plus the merge rules.</summary>
    public class TiersTests
    {
        [Test]
        public void TierToValue_MapsATierToItsPowerOfThreeValue()
        {
            // Merges always join two equal balls, so a merge yields 1.5*(V+V) = 3V — the value
            // ladder is powers of three.
            Assert.That(Tiers.TierToValue(1), Is.EqualTo(1));
            Assert.That(Tiers.TierToValue(2), Is.EqualTo(3));
            Assert.That(Tiers.TierToValue(3), Is.EqualTo(9));
            Assert.That(Tiers.TierToValue(4), Is.EqualTo(27));
            Assert.That(Tiers.TierToValue(5), Is.EqualTo(81));
        }

        [Test]
        public void TierToValue_KeepsClimbingPastTheBaseTable()
        {
            Assert.That(Tiers.TierToValue(Tiers.TierCount), Is.EqualTo(Math.Pow(3, Tiers.TierCount - 1)));
            Assert.That(Tiers.TierToValue(Tiers.TierCount + 3), Is.EqualTo(Math.Pow(3, Tiers.TierCount + 2)));
        }

        [Test]
        public void MergeLogic_MergesOnlyEqualTiersAndStepsUpWithoutACeiling()
        {
            Assert.That(MergeLogic.CanMerge(3, 3), Is.True);
            Assert.That(MergeLogic.CanMerge(3, 4), Is.False);
            Assert.That(MergeLogic.MergedTier(3), Is.EqualTo(4));
            Assert.That(MergeLogic.MergedTier(99), Is.EqualTo(100));
        }

        [Test]
        public void MergedBallTriplesTheValueOfEachSource()
        {
            // The reason the ladder is powers of three, asserted end to end.
            const int tier = 6;
            Assert.That(Tiers.TierToValue(MergeLogic.MergedTier(tier)),
                Is.EqualTo(Tiers.TierToValue(tier) * 3).Within(1e-6));
        }
    }
}
