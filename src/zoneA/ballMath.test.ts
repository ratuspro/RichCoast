import { describe, expect, it } from 'vitest';
import { TIER_COUNT } from '../core/contracts';
import { WIDTH } from '../core/Layout';
import {
  BLAST_RADIUS,
  BLAST_STRENGTH,
  DEATH_LINE_Y,
  FRICTION_MAX,
  RADII,
  SPAWN_Y,
  TIER_COLORS,
  WARN_BAND,
} from './tuning';
import {
  blastImpulse,
  clampSpawnX,
  frictionForTier,
  isNearDeath,
  isOverflow,
  isRestingAbove,
  midpoint,
  nextRestMs,
  radiusForTier,
} from './ballMath';

describe('tuning tables', () => {
  it('have exactly one entry per tier', () => {
    expect(RADII).toHaveLength(TIER_COUNT);
    expect(TIER_COLORS).toHaveLength(TIER_COUNT);
  });

  it('place the spawn row above the death line', () => {
    expect(SPAWN_Y).toBeLessThan(DEATH_LINE_Y);
  });

  it('let the largest spawnable tier (4) clear the y=0 ceiling at the spawn row', () => {
    expect(radiusForTier(4)).toBeLessThan(SPAWN_Y);
  });

  it('keep even the largest ball within the screen width', () => {
    expect(radiusForTier(TIER_COUNT) * 2).toBeLessThan(WIDTH);
  });
});

describe('radiusForTier', () => {
  it('is strictly increasing across the base table', () => {
    for (let t = 2; t <= TIER_COUNT; t++) {
      expect(radiusForTier(t)).toBeGreaterThan(radiusForTier(t - 1));
    }
  });

  it('keeps growing past the base table (tiers are unbounded)', () => {
    expect(radiusForTier(TIER_COUNT + 1)).toBeGreaterThan(radiusForTier(TIER_COUNT));
    expect(radiusForTier(TIER_COUNT + 5)).toBeGreaterThan(radiusForTier(TIER_COUNT + 4));
  });

  it('clamps tiers below 1 to the smallest', () => {
    expect(radiusForTier(0)).toBe(radiusForTier(1));
  });
});

describe('frictionForTier', () => {
  it('is non-decreasing and bounded by [0, FRICTION_MAX]', () => {
    for (let t = 1; t <= TIER_COUNT; t++) {
      const f = frictionForTier(t);
      expect(f).toBeGreaterThanOrEqual(0);
      expect(f).toBeLessThanOrEqual(FRICTION_MAX);
      if (t > 1) expect(f).toBeGreaterThanOrEqual(frictionForTier(t - 1));
    }
  });
});

describe('clampSpawnX', () => {
  it('keeps the ball fully inside the bounds', () => {
    expect(clampSpawnX(-100, 13, 0, WIDTH)).toBe(13);
    expect(clampSpawnX(10_000, 13, 0, WIDTH)).toBe(WIDTH - 13);
    expect(clampSpawnX(195, 13, 0, WIDTH)).toBe(195);
  });
});

describe('midpoint', () => {
  it('averages coordinates', () => {
    expect(midpoint({ x: 0, y: 0 }, { x: 10, y: 20 })).toEqual({ x: 5, y: 10 });
  });
});

describe('blastImpulse', () => {
  it('is zero at the origin and at/beyond the radius', () => {
    const origin = { x: 5, y: 5 };
    expect(blastImpulse(origin, origin, BLAST_RADIUS, BLAST_STRENGTH)).toEqual({ x: 0, y: 0 });
    expect(
      blastImpulse({ x: 5 + BLAST_RADIUS, y: 5 }, origin, BLAST_RADIUS, BLAST_STRENGTH),
    ).toEqual({ x: 0, y: 0 });
  });

  it('points away from the origin', () => {
    const v = blastImpulse({ x: 10, y: 0 }, { x: 0, y: 0 }, BLAST_RADIUS, BLAST_STRENGTH);
    expect(v.x).toBeGreaterThan(0);
    expect(v.y).toBe(0);
  });

  it('is stronger closer to the origin (linear falloff)', () => {
    const near = blastImpulse({ x: 10, y: 0 }, { x: 0, y: 0 }, BLAST_RADIUS, BLAST_STRENGTH);
    const far = blastImpulse({ x: 80, y: 0 }, { x: 0, y: 0 }, BLAST_RADIUS, BLAST_STRENGTH);
    expect(near.x).toBeGreaterThan(far.x);
  });
});

describe('rest / overflow', () => {
  it('accumulates rest time while resting and resets otherwise', () => {
    expect(nextRestMs(100, 16, true)).toBe(116);
    expect(nextRestMs(100, 16, false)).toBe(0);
  });

  it('detects a slow ball above the line only', () => {
    expect(isRestingAbove(50, 0.2, DEATH_LINE_Y, 0.8)).toBe(true); // above & slow
    expect(isRestingAbove(50, 2.0, DEATH_LINE_Y, 0.8)).toBe(false); // above but fast
    expect(isRestingAbove(150, 0.2, DEATH_LINE_Y, 0.8)).toBe(false); // slow but below
  });

  it('overflows once the rest threshold is reached', () => {
    expect(isOverflow(999, 1000)).toBe(false);
    expect(isOverflow(1000, 1000)).toBe(true);
  });
});

describe('isNearDeath', () => {
  const line = DEATH_LINE_Y;
  const band = WARN_BAND;
  const slow = 0.2;
  const fast = 2.0;

  it('flags a slow ball whose centre sits just below the line (warning band)', () => {
    expect(isNearDeath(line + 1, slow, line, band, 0.8)).toBe(true);
    expect(isNearDeath(line, slow, line, band, 0.8)).toBe(true); // on the line counts as near
  });

  it('does not flag a ball already over the line (that is overflow, not warning)', () => {
    expect(isNearDeath(line - 1, slow, line, band, 0.8)).toBe(false);
  });

  it('does not flag a ball below the band', () => {
    expect(isNearDeath(line + band, slow, line, band, 0.8)).toBe(false);
    expect(isNearDeath(line + band + 50, slow, line, band, 0.8)).toBe(false);
  });

  it('does not flag a fast ball even inside the band', () => {
    expect(isNearDeath(line + 1, fast, line, band, 0.8)).toBe(false);
  });
});
