import { describe, expect, it } from 'vitest';
import { TIER_COUNT } from '../core/contracts';
import { WIDTH } from '../core/Layout';
import { getStage, windowForLevel } from '../core/Progression';
import { MATERIAL_COUNT, materialForTier } from '../core/Materials';
import {
  BLAST_RADIUS,
  BLAST_STRENGTH,
  DEATH_LINE_Y,
  DENSITY,
  DENSITY_TAPER_TIER,
  FRICTION_MAX,
  MAX_BALL_SPEED,
  RADII,
  SPAWN_Y,
  WARN_BAND,
} from './tuning';
import {
  blastImpulse,
  clampSpawnX,
  clampSpeed,
  densityForTier,
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
  TAIL_MILESTONE_ZOOM,
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
    expect(neutralGrowth(4, 8)).toBeCloseTo(71 / 34, 10);
  });

  it('converges to RADIUS_GROWTH^4 past the table ([9,12] → [13,16])', () => {
    expect(neutralGrowth(12, 16)).toBeCloseTo(1.18 ** 4, 10);
  });

  it('is exactly 1 when the window does not shift (post-tail milestones)', () => {
    expect(neutralGrowth(20, 20)).toBe(1);
  });
});

describe('progression milestone wiring', () => {
  // Mirrors Progression.MILESTONE_EVERY (kept literal so a cadence change is a conscious edit).
  const MILESTONE_EVERY = 20;

  it('only shifts the window floor on milestone levels (the blacklist/zoom coupling)', () => {
    for (let level = 2; level <= 400; level++) {
      const prev = windowForLevel(level - 1);
      const window = windowForLevel(level);
      if (window[0] !== prev[0]) {
        expect(level % MILESTONE_EVERY, `floor shift at level ${level}`).toBe(0);
      }
    }
  });

  it('grows the level-20 milestone by the neutral ball match × its authored tightness', () => {
    const prev = getStage(19);
    const stage = getStage(20);
    const factor = neutralGrowth(prev.ballWindow[1], stage.ballWindow[1]) * (stage.tightness ?? 1);
    expect(factor).toBeCloseTo((71 / 34) * 0.92, 10);
  });

  it('keeps stepping the window +2 per TAIL milestone, zooming a flat ×1.2', () => {
    expect(windowForLevel(99)).toEqual([17, 20]);
    expect(windowForLevel(100)).toEqual([19, 22]);
    expect(windowForLevel(119)).toEqual([19, 22]); // holds between tail milestones
    expect(windowForLevel(120)).toEqual([21, 24]);
    const zoom = milestoneZoomFactor(100, windowForLevel(99), windowForLevel(100), undefined, true);
    expect(zoom).toBe(TAIL_MILESTONE_ZOOM);
    // The flat tail zoom sits BELOW the neutral match for a +2 shift, so apparent ball
    // size creeps up each tail milestone — the endgame's mounting squeeze.
    expect(TAIL_MILESTONE_ZOOM).toBeLessThan(neutralGrowth(20, 22));
    expect(TAIL_MILESTONE_ZOOM).toBeGreaterThan(1);
  });
});

describe('milestoneZoomFactor', () => {
  it('is 1 on a non-milestone level, even if the windows differ', () => {
    expect(milestoneZoomFactor(21, [1, 4], [5, 8], 0.92)).toBe(1);
  });

  it('is 1 on a milestone whose window did not shift', () => {
    expect(milestoneZoomFactor(100, [17, 20], [17, 20], 1.05)).toBe(1);
  });

  it('is the neutral growth × tightness on a shifted milestone', () => {
    expect(milestoneZoomFactor(20, [1, 4], [5, 8], 0.92)).toBeCloseTo((71 / 34) * 0.92, 10);
  });

  it('defaults tightness to 1', () => {
    expect(milestoneZoomFactor(20, [1, 4], [5, 8], undefined)).toBeCloseTo(71 / 34, 10);
  });

  it('regression: a roll-through that overshoots a milestone still zooms (factors compose)', () => {
    // One Zone B drain rolls the bar through levels 39 → 40 → 41: level 40 is a shifted
    // milestone, but the burst's FINAL level (41) is not. Folding the per-level factors
    // into a product — what ZoneASystem's pendingCashIn does — must preserve the zoom.
    const factors = [
      milestoneZoomFactor(40, getStage(39).ballWindow, getStage(40).ballWindow, getStage(40).tightness),
      milestoneZoomFactor(41, getStage(40).ballWindow, getStage(41).ballWindow, getStage(41).tightness),
    ];
    const product = factors.reduce((acc, f) => acc * f, 1);
    const s40 = getStage(40);
    expect(product).toBeCloseTo(
      neutralGrowth(getStage(39).ballWindow[1], s40.ballWindow[1]) * (s40.tightness ?? 1),
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

describe('densityForTier', () => {
  it('keeps the flat base density at and below the taper tier', () => {
    for (let t = 1; t <= DENSITY_TAPER_TIER; t++) {
      expect(densityForTier(t)).toBe(DENSITY);
    }
  });

  it('reduces density above the taper tier and never raises it', () => {
    expect(densityForTier(DENSITY_TAPER_TIER + 1)).toBeLessThan(DENSITY);
    for (let t = DENSITY_TAPER_TIER + 2; t <= 24; t++) {
      expect(densityForTier(t)).toBeLessThan(densityForTier(t - 1));
      expect(densityForTier(t)).toBeLessThan(DENSITY);
    }
  });

  it('makes mass grow ~linearly with radius above the taper (DENSITY_MASS_EXP = 1)', () => {
    // mass ∝ density · radius²; with the exp-1 taper it should track radius^1, so mass/radius
    // is constant above the taper tier (up to fp).
    const massPerRadius = (t: number) => (densityForTier(t) * radiusForTier(t) ** 2) / radiusForTier(t);
    const ref = massPerRadius(DENSITY_TAPER_TIER + 1);
    for (let t = DENSITY_TAPER_TIER + 2; t <= 24; t++) {
      expect(massPerRadius(t)).toBeCloseTo(ref, 6);
    }
  });
});

describe('clampSpeed', () => {
  it('leaves a velocity under the cap unchanged', () => {
    const v = { x: 3, y: 4 }; // speed 5
    expect(clampSpeed(v, 16)).toBe(v);
  });

  it('scales an over-cap velocity to exactly the cap, preserving direction', () => {
    const capped = clampSpeed({ x: 30, y: 40 }, 10); // speed 50 → ×0.2
    expect(Math.hypot(capped.x, capped.y)).toBeCloseTo(10, 10);
    expect(capped.x / capped.y).toBeCloseTo(30 / 40, 10); // direction held
  });

  it('is a no-op on the zero vector (no divide-by-zero)', () => {
    expect(clampSpeed({ x: 0, y: 0 }, 16)).toEqual({ x: 0, y: 0 });
  });

  it('anti-tunnel invariant: the base cap sits below the base wall thickness', () => {
    // Mirrors ArenaView's WALL_T (kept literal so a change there is a conscious edit). Both the
    // cap and the walls scale ×s, so a per-step speed under the cap can never cross a wall.
    const WALL_T = 40;
    expect(MAX_BALL_SPEED).toBeLessThan(WALL_T);
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
