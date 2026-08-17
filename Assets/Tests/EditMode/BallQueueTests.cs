using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    public class BallQueueTests
    {
        [Test]
        public void AlwaysHasABallInHandAndAPreview()
        {
            var queue = new BallQueue(new TierWindow(1, 4), seed: 1);
            Assert.That(queue.Current, Is.InRange(1, 4));
            Assert.That(queue.Next, Is.InRange(1, 4));
        }

        [Test]
        public void TakingAdvancesThePreviewImmediatelySoThePlayerNeverWaits()
        {
            var queue = new BallQueue(new TierWindow(1, 4), seed: 7);
            var previewed = queue.Next;

            var taken = queue.Take();

            Assert.That(taken, Is.InRange(1, 4));
            Assert.That(queue.Current, Is.EqualTo(previewed), "the preview becomes the ball in hand");
            Assert.That(queue.Next, Is.InRange(1, 4), "a fresh preview is drawn at once");
        }

        [Test]
        public void OnlyEverDrawsFromTheCurrentWindow()
        {
            var queue = new BallQueue(new TierWindow(5, 8), seed: 3);
            for (var i = 0; i < 500; i++)
            {
                Assert.That(queue.Current, Is.InRange(5, 8));
                Assert.That(queue.Next, Is.InRange(5, 8));
                queue.Take();
            }
        }

        [Test]
        public void CoversEveryTierInTheWindowOverManyDraws()
        {
            var queue = new BallQueue(new TierWindow(1, 4), seed: 11);
            var seen = new bool[5];
            for (var i = 0; i < 400; i++) seen[queue.Take()] = true;

            for (var tier = 1; tier <= 4; tier++)
            {
                Assert.That(seen[tier], Is.True, $"tier {tier} never appeared");
            }
        }

        [Test]
        public void AMilestoneWindowShiftRerollsBlacklistedTiersOutOfTheHandAndPreview()
        {
            var queue = new BallQueue(new TierWindow(1, 4), seed: 5);
            Assert.That(queue.Current, Is.LessThanOrEqualTo(4));

            queue.SetWindow(new TierWindow(5, 8));

            Assert.That(queue.Current, Is.InRange(5, 8), "the in-hand ball must not stay blacklisted");
            Assert.That(queue.Next, Is.InRange(5, 8), "nor may the preview");
        }

        [Test]
        public void AWindowThatStillContainsTheHeldTierLeavesItAlone()
        {
            // The opening levels widen the window by ceiling only, so nothing is blacklisted and a
            // re-roll would be a pointless (and visible) swap in the player's hand.
            var queue = new BallQueue(new TierWindow(1, 1), seed: 2);
            var held = queue.Current;

            queue.SetWindow(new TierWindow(1, 3));

            Assert.That(queue.Current, Is.EqualTo(held));
        }

        [Test]
        public void IsDeterministicForASeed()
        {
            var a = new BallQueue(new TierWindow(1, 20), seed: 42);
            var b = new BallQueue(new TierWindow(1, 20), seed: 42);
            for (var i = 0; i < 50; i++) Assert.That(a.Take(), Is.EqualTo(b.Take()));
        }
    }
}
