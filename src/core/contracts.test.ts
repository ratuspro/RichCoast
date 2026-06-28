import { describe, expect, it } from 'vitest';
import { TIER_COUNT, tierToValue } from './contracts';

describe('tierToValue', () => {
  it('maps a tier to its power-of-two value (2^(tier-1))', () => {
    expect(tierToValue(1)).toBe(1);
    expect(tierToValue(2)).toBe(2);
    expect(tierToValue(3)).toBe(4);
    expect(tierToValue(4)).toBe(8); // SPEC example: a tier-4 ball is worth 8
    expect(tierToValue(5)).toBe(16);
  });

  it('covers the full tier range', () => {
    expect(tierToValue(TIER_COUNT)).toBe(2 ** (TIER_COUNT - 1));
  });
});
