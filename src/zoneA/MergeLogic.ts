import { TIER_COUNT } from '../core/contracts';

/**
 * Pure merge rules for Zone A. No Phaser, no state — unit-tested in isolation and
 * safe to call from anywhere in Dev 1's half.
 */

/** Two balls merge iff they share a tier and that tier isn't already the max. */
export function canMerge(tierA: number, tierB: number): boolean {
  return tierA === tierB && tierA < TIER_COUNT;
}

/** The tier produced by merging two balls of `tier` — one step up, capped at the max. */
export function mergedTier(tier: number): number {
  return Math.min(tier + 1, TIER_COUNT);
}
