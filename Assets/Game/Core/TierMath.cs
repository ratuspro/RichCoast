using System;

namespace RichCoast.Core
{
    /// <summary>
    /// Tier ↔ value ladder. Ported from the Phaser <c>contracts.ts</c> — the design truth.
    /// Merges are UNCAPPED: <see cref="TierCount"/> is only the size of the hand-authored
    /// radius table, never a gameplay ceiling.
    /// </summary>
    public static class TierMath
    {
        /// <summary>Entries in the base radius table. Tiers climb past it forever.</summary>
        public const int TierCount = 10;

        /// <summary>
        /// Tier (1-based) → ball value: 3^(tier-1). Merging two equal balls yields
        /// 1.5×(V+V) = 3V, so the value ladder is powers of three. Double because values
        /// outgrow 64-bit integers in a long run (tier 41 is 3^40 ≈ 1.2e19).
        /// </summary>
        public static double ValueForTier(int tier) => Math.Pow(3, tier - 1);
    }

    /// <summary>Two balls merge iff they share a tier (i.e. the same value). Uncapped.</summary>
    public static class MergeLogic
    {
        public static bool CanMerge(int tierA, int tierB) => tierA == tierB;

        /// <summary>The tier produced by merging two balls of <paramref name="tier"/> — one step up, no ceiling.</summary>
        public static int MergedTier(int tier) => tier + 1;
    }
}
