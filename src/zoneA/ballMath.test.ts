import { describe, expect, it } from 'vitest';
import { TIER_COUNT } from '../core/contracts';
import { WIDTH } from '../core/Layout';
import { getStage } from '../core/Progression';
import { MATERIAL_COUNT, materialForTier } from '../core/Materials';
import {
  BLAST_RADIUS,
  BLAST_STRENGTH,
  DEATH_LINE_Y,
  FRICTION_MAX,
  RADII,
  SPAWN_Y,
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
  milestoneZoomFactor,
  nearestDoorBall,
  neutralGrowth,
  nextRestMs,
  radiusForTier,
} from './ballMath';

describe('tuning tables', () => {
  it('have exactly one entry per tier', () => {
    expect(RADII).toHaveLength(TIER_COUNT);
    // The look ladder is longer than the radius table; it must at least cover it.
    expect(MATERIAL_COUNT).toBeGreaterThanOrEqual(TIER_COUNT);
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

describe('neutralGrowth', () => {
  it('matches the window-max radius ratio in the hand-table region ([1,4] → [5,8])', () => {
    expect(neutralGrowth(4, 8)).toBeCloseTo(56 / 26, 10);
  });

  it('converges to RADIUS_GROWTH^4 past the table ([9,12] → [13,16])', () => {
    expect(neutralGrowth(12, 16)).toBeCloseTo(1.18 ** 4, 10);
  });

  it('is exactly 1 when the window does not shift (post-tail milestones)', () => {
    expect(neutralGrowth(20, 20)).toBe(1);
  });
});

describe('progression milestone wiring', () => {
  // Mirrors ZoneASystem.MILESTONE_EVERY (not importable here — it pulls in Phaser).
  const MILESTONE_EVERY = 25;

  it('only shifts the window floor on milestone levels (the blacklist/zoom coupling)', () => {
    for (let level = 2; level <= 400; level++) {
      const prev = getStage(level - 1);
      const stage = getStage(level);
      if (stage.ballWindow[0] !== prev.ballWindow[0]) {
        expect(level % MILESTONE_EVERY, `floor shift at level ${level}`).toBe(0);
      }
    }
  });

  it('grows the level-25 milestone by the neutral ball match × its authored tightness', () => {
    const prev = getStage(24);
    const stage = getStage(25);
    const factor = neutralGrowth(prev.ballWindow[1], stage.ballWindow[1]) * (stage.tightness ?? 1);
    expect(factor).toBeCloseTo((56 / 26) * 0.92, 10);
  });

  it('self-heals past the last authored shift: window stops moving, growth is 1', () => {
    const prev = getStage(124);
    const stage = getStage(125);
    expect(stage.ballWindow).toEqual(prev.ballWindow);
    expect(neutralGrowth(prev.ballWindow[1], stage.ballWindow[1])).toBe(1);
  });
});

describe('milestoneZoomFactor', () => {
  it('is 1 on a non-milestone level, even if the windows differ', () => {
    expect(milestoneZoomFactor(26, [1, 4], [5, 8], 0.92)).toBe(1);
  });

  it('is 1 on a milestone whose window did not shift (past the last authored shift)', () => {
    expect(milestoneZoomFactor(125, [17, 20], [17, 20], 1.05)).toBe(1);
  });

  it('is the neutral growth × tightness on a shifted milestone', () => {
    expect(milestoneZoomFactor(25, [1, 4], [5, 8], 0.92)).toBeCloseTo((56 / 26) * 0.92, 10);
  });

  it('defaults tightness to 1', () => {
    expect(milestoneZoomFactor(25, [1, 4], [5, 8], undefined)).toBeCloseTo(56 / 26, 10);
  });

  it('regression: a roll-through that overshoots a milestone still zooms (factors compose)', () => {
    // One Zone B drain rolls the bar through levels 49 → 50 → 51: level 50 is a shifted
    // milestone, but the burst's FINAL level (51) is not. Folding the per-level factors
    // into a product — what ZoneASystem's pendingCashIn does — must preserve the zoom.
    const factors = [
      milestoneZoomFactor(50, getStage(49).ballWindow, getStage(50).ballWindow, getStage(50).tightness),
      milestoneZoomFactor(51, getStage(50).ballWindow, getStage(51).ballWindow, getStage(51).tightness),
    ];
    const product = factors.reduce((acc, f) => acc * f, 1);
    const s50 = getStage(50);
    expect(product).toBeCloseTo(
      neutralGrowth(getStage(49).ballWindow[1], s50.ballWindow[1]) * (s50.tightness ?? 1),
      10,
    );
    expect(product).not.toBe(1);
  });
});

describe('frictionForTier', () => {
  it('is the clamped size ramp shaped by the material feel multiplier', () => {
    for (let t = 1; t <= MATERIAL_COUNT; t++) {
      const mult = materialForTier(t).def.physics.frictionMult;
      const f = frictionForTier(t);
      expect(f).toBeGreaterThan(0);
      // The band stays subtle: never past the ramp cap × the largest material factor.
      expect(f).toBeLessThanOrEqual(FRICTION_MAX * 1.2);
      expect(f / mult).toBeLessThanOrEqual(FRICTION_MAX + 1e-12);
    }
  });

  it('grows with tier within one material family (the size ramp survives)', () => {
    // Tiers 1–4 are all primitives with the same multiplier except wood (tier 1).
    expect(frictionForTier(3)).toBeGreaterThan(frictionForTier(2));
    expect(frictionForTier(4)).toBeGreaterThan(frictionForTier(3));
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

describe('nearestDoorBall', () => {
  // Door mouth for these cases: centred at x=195, balls count while above y=507.
  const mouthX = 195;
  const doorY = 507;
  const ball = (x: number, y: number, r: number) => ({
    position: { x, y }, circleRadius: r, ballData: { value: 1, tier: 1 },
  });

  it('returns undefined when the board is empty', () => {
    expect(nearestDoorBall([], mouthX, doorY)).toBeUndefined();
  });

  it('picks the ball nearest the mouth by centre distance when radii are equal', () => {
    const near = ball(195, 480, 13);
    const far = ball(100, 300, 13);
    expect(nearestDoorBall([far, near], mouthX, doorY)).toBe(near);
  });

  it('favours a bigger ball whose EDGE reaches nearer over a closer-centre small one', () => {
    const bigFarther = ball(195, 420, 90); // centre 87 away, edge -3
    const smallCloser = ball(195, 460, 13); // centre 47 away, edge 34
    expect(nearestDoorBall([smallCloser, bigFarther], mouthX, doorY)).toBe(bigFarther);
  });

  it('ignores bodies with no ballData tag (walls, funnel, Zone B balls)', () => {
    const wall = { position: { x: 195, y: 500 }, circleRadius: 5 };
    const real = ball(120, 400, 13);
    expect(nearestDoorBall([wall, real], mouthX, doorY)).toBe(real);
  });

  it('ignores balls that have already fallen past the door (below doorY)', () => {
    const belowDoor = ball(195, doorY + 1, 13);
    const inZoneA = ball(120, 400, 13);
    expect(nearestDoorBall([belowDoor, inZoneA], mouthX, doorY)).toBe(inZoneA);
    // A ball exactly on the door line still counts.
    expect(nearestDoorBall([ball(195, doorY, 13)], mouthX, doorY)).toBeDefined();
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
