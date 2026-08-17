using RichCoast.Core;

namespace RichCoast.Gameplay
{
    /// <summary>
    /// Everything a system needs that outlives it: the seam's bus and the resolved tuning tables.
    /// Handed to each system at construction so nothing reaches for a singleton or a static, which
    /// keeps systems constructible in a test without a scene.
    /// </summary>
    public sealed class GameContext
    {
        public GameContext(EventBus bus, Progression progression, TierTable tiers, float screenHeight)
        {
            Bus = bus;
            Progression = progression;
            Tiers = tiers;
            ScreenHeight = screenHeight;
        }

        public EventBus Bus { get; }
        public Progression Progression { get; }
        public TierTable Tiers { get; }

        /// <summary>Visible design-space height on this device — see <see cref="Layout"/>.</summary>
        public float ScreenHeight { get; }

        /// <summary>How far the camera pans between the A and B framings on this device.</summary>
        public float PanDistance => PhaseGeometry.PanDistanceFor(ScreenHeight);
    }
}
