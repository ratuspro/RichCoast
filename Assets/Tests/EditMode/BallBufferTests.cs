using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>
    /// The ball supply's rules as SPEC.md describes them: spend per drop, drip-fed refills, the
    /// last-chance window, and the roll-through burst bonus.
    /// </summary>
    public class BallBufferTests
    {
        [Test]
        public void StartsFullAndSpendsOneSlotPerDrop()
        {
            var buffer = new BallBuffer(Progression.BufferForLevel(1));
            Assert.That(buffer.Count, Is.EqualTo(8));

            Assert.That(buffer.Spend(), Is.True);
            Assert.That(buffer.Count, Is.EqualTo(7));
        }

        [Test]
        public void RefusesToSpendWhenExhausted()
        {
            var buffer = new BallBuffer(1);
            buffer.Spend();

            Assert.That(buffer.IsExhausted, Is.True);
            Assert.That(buffer.Spend(), Is.False, "no ball may be dropped on an empty buffer");
            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void RefillLandsOneSlotAtATimeSoTheBufferVisiblyClimbs()
        {
            var buffer = new BallBuffer(0);
            var owed = buffer.BeginRefill(capacity: 5);

            Assert.That(owed, Is.EqualTo(5));
            Assert.That(buffer.Count, Is.EqualTo(0), "nothing lands until the drip runs");
            Assert.That(buffer.PendingRefill, Is.EqualTo(5));

            Assert.That(buffer.TakeRefillSlot(), Is.True);
            Assert.That(buffer.Count, Is.EqualTo(1), "dropping unlocks on the first slot");

            while (buffer.TakeRefillSlot()) { }
            Assert.That(buffer.Count, Is.EqualTo(5));
            Assert.That(buffer.PendingRefill, Is.EqualTo(0));
            Assert.That(buffer.TakeRefillSlot(), Is.False);
        }

        [Test]
        public void RefillTopsUpToCapacityRatherThanStackingOnTopOfCarriedSlots()
        {
            var buffer = new BallBuffer(3);
            var owed = buffer.BeginRefill(capacity: 10);

            Assert.That(owed, Is.EqualTo(7));
            while (buffer.TakeRefillSlot()) { }
            Assert.That(buffer.Count, Is.EqualTo(10));
        }

        [Test]
        public void ARollThroughPaysTheBurstBonusPerLevelBeyondTheFirst()
        {
            // A multi-level burst must be a jackpot in the resource that matters, not just a
            // bigger number: only the final level's refill runs, so the bonus makes it tangible.
            var buffer = new BallBuffer(0);
            buffer.BeginRefill(capacity: 10, levelsCrossed: 4);
            while (buffer.TakeRefillSlot()) { }

            Assert.That(buffer.Count, Is.EqualTo(10 + Progression.BurstRefillBonus * 3));
        }

        [Test]
        public void LastChanceWindow_ABankedRefillMeansTheRunStillHasSupply()
        {
            // Buffer at 0 with a ball still in Zone B: if it fills the bar before Zone B empties,
            // the run continues — the stalemate check must see the banked slots.
            var buffer = new BallBuffer(0);
            Assert.That(buffer.HasSupply, Is.False);

            buffer.BeginRefill(capacity: Progression.BufferForLevel(2));
            Assert.That(buffer.HasSupply, Is.True, "banked slots keep the run alive");
            Assert.That(buffer.IsExhausted, Is.True, "but nothing is droppable until a slot lands");
        }

        [Test]
        public void ResetStartsAFreshRunWithNothingOwed()
        {
            var buffer = new BallBuffer(0);
            buffer.BeginRefill(capacity: 12);
            buffer.Reset(Progression.BufferForLevel(1));

            Assert.That(buffer.Count, Is.EqualTo(8));
            Assert.That(buffer.PendingRefill, Is.EqualTo(0));
        }
    }
}
