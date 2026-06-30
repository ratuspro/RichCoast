/**
 * Pure merge rules for Zone A. No Phaser, no state — unit-tested in isolation and
 * safe to call from anywhere in Dev 1's half.
 */

/** Two balls merge iff they share a tier (i.e. the same value). Uncapped — tiers climb forever. */
export function canMerge(tierA: number, tierB: number): boolean {
  return tierA === tierB;
}

/** The tier produced by merging two balls of `tier` — one step up, no ceiling. */
export function mergedTier(tier: number): number {
  return tier + 1;
}
