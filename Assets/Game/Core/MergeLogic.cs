namespace RichCoast.Core
{
    /// <summary>
    /// Pure merge rules for Zone A. No state, no scene — unit-tested in isolation and safe to
    /// call from anywhere. Ported from <c>zoneA/MergeLogic.ts</c>.
    /// </summary>
    public static class MergeLogic
    {
        /// <summary>
        /// Two balls merge iff they share a tier (i.e. the same value). Uncapped — tiers climb
        /// forever.
        /// </summary>
        public static bool CanMerge(int tierA, int tierB) => tierA == tierB;

        /// <summary>The tier produced by merging two balls of <paramref name="tier"/> — one step up, no ceiling.</summary>
        public static int MergedTier(int tier) => tier + 1;
    }
}
