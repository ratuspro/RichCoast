import { describe, expect, it } from 'vitest';
import { TIER_COUNT, tierToValue } from './contracts';

describe('tierToValue', () => {
  it('maps a tier to its power-of-three value (3^(tier-1))', () => {
    // Merges always join two equal balls, so a merge yields 1.5*(V+V) = 3V — the
    // value ladder is powers of three.
    expect(tierToValue(1)).toBe(1);
    expect(tierToValue(2)).toBe(3);
    expect(tierToValue(3)).toBe(9);
    expect(tierToValue(4)).toBe(27);
    expect(tierToValue(5)).toBe(81);
  });

  it('keeps climbing past the base table (tiers are unbounded)', () => {
    expect(tierToValue(TIER_COUNT)).toBe(3 ** (TIER_COUNT - 1));
    expect(tierToValue(TIER_COUNT + 3)).toBe(3 ** (TIER_COUNT + 2));
  });
});
