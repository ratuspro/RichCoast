using System;

namespace RichCoast.Core
{
    /// <summary>
    /// The ball the player is about to drop and the one after it.
    ///
    /// There is always a ball ready: as soon as one is released the next appears immediately, so
    /// the player is never waiting. Both are drawn from the current draw window, which is what
    /// keeps the board from filling with tiers far above what the player can merge.
    ///
    /// When a milestone shifts the window, tiers below the new floor are blacklisted: the in-hand
    /// and preview balls are re-rolled off them, matching how balls already on the board drain
    /// away. Seeded so the sampling is deterministic in tests.
    /// </summary>
    public sealed class BallQueue
    {
        private readonly Random _rng;
        private TierWindow _window;

        public BallQueue(TierWindow window, int seed)
        {
            _rng = new Random(seed);
            _window = window;
            Current = Draw();
            Next = Draw();
        }

        /// <summary>Tier of the ball in hand.</summary>
        public int Current { get; private set; }

        /// <summary>Tier of the preview ball shown in the HUD's queue row.</summary>
        public int Next { get; private set; }

        public TierWindow Window => _window;

        /// <summary>Release the current ball: the preview becomes current and a new preview is drawn.</summary>
        public int Take()
        {
            var taken = Current;
            Current = Next;
            Next = Draw();
            return taken;
        }

        /// <summary>
        /// Adopt a new draw window. Anything now outside it is re-rolled, so a milestone can never
        /// leave a blacklisted tier in the player's hand.
        /// </summary>
        public void SetWindow(TierWindow window)
        {
            _window = window;
            if (!_window.Contains(Current)) Current = Draw();
            if (!_window.Contains(Next)) Next = Draw();
        }

        /// <summary>Uniform draw across the window's inclusive tier range.</summary>
        private int Draw() => _rng.Next(_window.Min, _window.Max + 1);
    }
}
