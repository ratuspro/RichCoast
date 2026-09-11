using System;
using System.Collections.Generic;

namespace RichCoast.Core
{
    /// <summary>
    /// The current + next ball draw (port of the Phaser <c>BallQueue.ts</c>). Random draws come
    /// from the active [min,max] tier window; stages can seed a curated hand that is consumed
    /// first. The RNG is injectable so tests are deterministic.
    /// </summary>
    public sealed class BallQueue
    {
        readonly Func<int, int, int> randomInclusive;
        readonly Queue<int> seeded = new Queue<int>();
        int minTier = 1;
        int maxTier = 4;

        public int CurrentTier { get; private set; }
        public int NextTier { get; private set; }

        /// <param name="randomInclusive">Returns a tier in [min, max]. Defaults to <see cref="System.Random"/>.</param>
        public BallQueue(Func<int, int, int> randomInclusive = null)
        {
            if (randomInclusive == null)
            {
                var rng = new Random();
                randomInclusive = (lo, hi) => rng.Next(lo, hi + 1);
            }
            this.randomInclusive = randomInclusive;
            CurrentTier = PullNext();
            NextTier = PullNext();
        }

        /// <summary>Consume the current ball, advance the queue, and return the dropped tier.</summary>
        public int Pop()
        {
            int dropped = CurrentTier;
            CurrentTier = NextTier;
            NextTier = PullNext();
            return dropped;
        }

        /// <summary>Update the random tier range. Takes effect for future random draws only.</summary>
        public void SetWindow(int min, int max)
        {
            minTier = min;
            maxTier = max;
        }

        /// <summary>
        /// Re-draw current + next from the live window (a milestone shifted it up, so the in-hand
        /// and preview balls aren't stranded on blacklisted tiers). Clears any unconsumed seed.
        /// </summary>
        public void Reroll()
        {
            seeded.Clear();
            CurrentTier = PullNext();
            NextTier = PullNext();
        }

        /// <summary>Pre-load specific tiers into the front of the queue; random draws resume after.</summary>
        public void Seed(IEnumerable<int> tiers)
        {
            seeded.Clear();
            foreach (var t in tiers) seeded.Enqueue(t);
            CurrentTier = PullNext();
            NextTier = PullNext();
        }

        int PullNext() => seeded.Count > 0 ? seeded.Dequeue() : randomInclusive(minTier, maxTier);
    }
}
