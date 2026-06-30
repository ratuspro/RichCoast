import { describe, expect, it } from 'vitest';
import { TIER_COUNT } from '../core/contracts';
import { canMerge, mergedTier } from './MergeLogic';

describe('canMerge', () => {
  it('is true for equal tiers', () => {
    expect(canMerge(3, 3)).toBe(true);
  });

  it('is false for different tiers', () => {
    expect(canMerge(2, 3)).toBe(false);
  });

  it('still merges at and beyond the base table tier (no cap)', () => {
    expect(canMerge(TIER_COUNT, TIER_COUNT)).toBe(true);
    expect(canMerge(TIER_COUNT + 5, TIER_COUNT + 5)).toBe(true);
  });
});

describe('mergedTier', () => {
  it('steps up exactly one tier', () => {
    expect(mergedTier(1)).toBe(2);
    expect(mergedTier(5)).toBe(6);
  });

  it('keeps stepping up past the base table tier (no cap)', () => {
    expect(mergedTier(TIER_COUNT)).toBe(TIER_COUNT + 1);
    expect(mergedTier(TIER_COUNT + 7)).toBe(TIER_COUNT + 8);
  });
});
