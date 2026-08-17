using RichCoast.Core;

namespace RichCoast.Data
{
    /// <summary>
    /// The authored progression stage table, ported verbatim from <c>core/progression.json</c> —
    /// the single source both the ScriptableObject and the tests seed from.
    ///
    /// Stages are ANCHORS: the score-bar target interpolates geometrically between them and the
    /// draw window holds until the next one. Window shift-ups must land on multiples of
    /// <see cref="Progression.MilestoneEvery"/>, since the milestone reads the new window's floor
    /// as its blacklist threshold.
    /// </summary>
    public static class DefaultProgression
    {
        public static ProgressionStage[] CreateStages() => new[]
        {
            // Opening ramp: the window widens by ceiling only (no blacklist) over the first levels.
            new ProgressionStage(1, new TierWindow(1, 1), 20),
            new ProgressionStage(2, new TierWindow(1, 2), 80),
            new ProgressionStage(3, new TierWindow(1, 3), 130),
            new ProgressionStage(4, new TierWindow(1, 4), 200),

            // Milestones: the window jumps a whole material family and the arena grows.
            // Tightness alternates squeeze → breathe, with deepening squeezes over time.
            new ProgressionStage(20, new TierWindow(5, 8), 5_000, 0.92f, "dusk"),
            new ProgressionStage(40, new TierWindow(9, 12), 400_000, 1.05f, "night"),
            new ProgressionStage(60, new TierWindow(13, 16), 33_000_000, 0.85f, "dawn"),
            new ProgressionStage(80, new TierWindow(17, 20), 2_700_000_000, 1.05f, "gilded"),
        };

        /// <summary>The authored progression, ready to query.</summary>
        public static Progression Create() => new Progression(CreateStages());
    }
}
