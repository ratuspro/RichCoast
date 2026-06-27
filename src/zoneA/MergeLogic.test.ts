import { describe, expect, it } from 'vitest';
import { TIER_COUNT } from '../core/contracts';
import { canMerge, mergedTier } from './MergeLogic';

describe('canMerge', () => {
  it('is true for equal tiers below the max', () => {
    expect(canMerge(3, 3)).toBe(true);
  });

  it('is false for different tiers', () => {
    expect(canMerge(2, 3)).toBe(false);
  });

  it('is false at the max tier (nothing higher to merge into)', () => {
    expect(canMerge(TIER_COUNT, TIER_COUNT)).toBe(false);
  });
});

describe('mergedTier', () => {
  it('steps up exactly one tier', () => {
    expect(mergedTier(1)).toBe(2);
    expect(mergedTier(5)).toBe(6);
  });

  it('caps at the max tier', () => {
    expect(mergedTier(TIER_COUNT)).toBe(TIER_COUNT);
  });
});
