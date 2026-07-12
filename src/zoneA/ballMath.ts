import { TIER_COUNT } from '../core/contracts';
import { materialForTier } from '../core/Materials';
import { MILESTONE_EVERY } from '../core/Progression';
import {
  DENSITY,
  DENSITY_MASS_EXP,
  DENSITY_TAPER_TIER,
  FRICTION_BASE,
  FRICTION_MAX,
  FRICTION_STEP,
  RADII,
  RADIUS_GROWTH,
} from './tuning';

/**
 * Pure Zone A math — no Phaser, no state. Turns the `tuning.ts` tables into the
 * per-tier and per-frame values the system needs, so the fiddly arithmetic is
 * unit-tested in plain Node (mirrors MergeLogic).
 */

export interface Vec2 {
  x: number;
  y: number;
}

/** Clamp an arbitrary tier into the valid 1..TIER_COUNT integer range. */
function clampTier(tier: number): number {
  if (tier < 1) return 1;
  if (tier > TIER_COUNT) return TIER_COUNT;
  return Math.floor(tier);
}

/**
 * Visual/physics radius for a tier (1-based). Tiers within the base table read it directly;
 * since merges are uncapped, tiers past the table keep growing geometrically (no clamp), so
 * ever-larger balls render — the expanding arena (see ArenaView) is what makes room for them.
 * Tiers below 1 clamp to the smallest.
 */
export function radiusForTier(tier: number): number {
  if (tier <= RADII.length) return RADII[clampTier(tier) - 1];
  return RADII[RADII.length - 1] * RADIUS_GROWTH ** (tier - RADII.length);
}

/**
 * Arena growth factor for a milestone whose draw window's *max* tier moved
 * `oldMaxTier → newMaxTier`. Growing the arena by exactly this keeps the window-max ball's
 * apparent on-screen size constant (the camera zoom is 1/scale), so a per-milestone
 * `tightness` multiplier applied on top is the precise change in worst-case headroom
 * (<1 = tighter/harder, >1 = roomier). An unshifted window yields 1 — no growth.
 */
export function neutralGrowth(oldMaxTier: number, newMaxTier: number): number {
  return radiusForTier(newMaxTier) / radiusForTier(oldMaxTier);
}

/** Arena growth per TAIL milestone: a flat factor, deliberately BELOW the neutral match for
 *  a TAIL_WINDOW_STEP shift (RADIUS_GROWTH² ≈ 1.39), so apparent ball size creeps up ~16%
 *  per tail milestone — the endgame's mounting board squeeze. */
export const TAIL_MILESTONE_ZOOM = 1.2;

/**
 * Arena zoom-out factor owed by ONE level-up: `neutralGrowth × tightness` when `level` is a
 * shifted-window milestone, else exactly 1 (non-milestone levels, and unshifted windows).
 * TAIL milestones (past the last authored stage; the window still steps up there — see
 * `windowForLevel`) ignore the neutral match and grow by the flat TAIL_MILESTONE_ZOOM.
 * Because a shifted window always zooms (radii strictly increase, tightness never inverts
 * that, and the tail factor is > 1), 1 doubles as the "no zoom" sentinel, and factors from
 * a multi-level score-bar roll-through compose by product — so a burst that overshoots a
 * milestone level still carries its zoom.
 */
export function milestoneZoomFactor(
  level: number,
  prevWindow: readonly [number, number],
  window: readonly [number, number],
  tightness: number | undefined,
  tail = false,
): number {
  if (level % MILESTONE_EVERY !== 0) return 1;
  const shifted = window[0] !== prevWindow[0] || window[1] !== prevWindow[1];
  if (!shifted) return 1;
  if (tail) return TAIL_MILESTONE_ZOOM;
  return neutralGrowth(prevWindow[1], window[1]) * (tightness ?? 1);
}

/** Surface friction for a tier: the size ramp (grows with tier, clamped to
 *  FRICTION_MAX) shaped by the tier's material feel — metals slide, gems slip. */
export function frictionForTier(tier: number): number {
  const raw = FRICTION_BASE + FRICTION_STEP * (clampTier(tier) - 1);
  return Math.min(raw, FRICTION_MAX) * materialForTier(tier).def.physics.frictionMult;
}

/**
 * Density (before the material `densityMult`) for a tier. Small tiers (≤ DENSITY_TAPER_TIER)
 * keep the flat DENSITY; larger balls taper so that mass (∝ density·radius²) grows like
 * radius^DENSITY_MASS_EXP instead of radius² — keeping big-ball collision momentum in check
 * so late-milestone shoves stay gentle. The taper only ever reduces density (bigger radius →
 * smaller factor, ≤1), never raises it.
 */
export function densityForTier(tier: number): number {
  if (tier <= DENSITY_TAPER_TIER) return DENSITY;
  const ratio = radiusForTier(DENSITY_TAPER_TIER) / radiusForTier(tier); // ≤ 1
  return DENSITY * ratio ** (2 - DENSITY_MASS_EXP);
}

/** Clamp a spawn X so a ball of `radius` stays fully within [minX, maxX]. */
export function clampSpawnX(x: number, radius: number, minX: number, maxX: number): number {
  const lo = minX + radius;
  const hi = maxX - radius;
  if (x < lo) return lo;
  if (x > hi) return hi;
  return x;
}

/** Midpoint of two points — where a merged ball is born. */
export function midpoint(a: Vec2, b: Vec2): Vec2 {
  return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
}

/**
 * Outward velocity kick applied to `target` by a blast at `origin`.
 * Zero when target sits exactly on the origin or at/beyond `radius`; otherwise it
 * points away from the origin with linear falloff: magnitude = strength*(1 - dist/radius).
 */
export function blastImpulse(target: Vec2, origin: Vec2, radius: number, strength: number): Vec2 {
  const dx = target.x - origin.x;
  const dy = target.y - origin.y;
  const dist = Math.hypot(dx, dy);
  if (dist === 0 || dist >= radius) return { x: 0, y: 0 };
  const mag = strength * (1 - dist / radius);
  return { x: (dx / dist) * mag, y: (dy / dist) * mag };
}

/**
 * Cap a velocity's magnitude at `maxSpeed`, preserving direction. Below the cap (or a zero
 * vector) it's returned unchanged. The anti-tunnel backstop: Board clamps every ball's speed
 * to MAX_BALL_SPEED×s each step, so no single-step displacement can exceed a wall's thickness.
 */
export function clampSpeed(v: Vec2, maxSpeed: number): Vec2 {
  const speed = Math.hypot(v.x, v.y);
  if (speed <= maxSpeed || speed === 0) return v;
  const k = maxSpeed / speed;
  return { x: v.x * k, y: v.y * k };
}

/** Accumulate rest time: previous + delta while resting, else reset to 0. */
export function nextRestMs(prev: number, delta: number, resting: boolean): number {
  return resting ? prev + delta : 0;
}

/** Minimal structural view of a Matter body for door-target selection: its position,
 *  optional `circleRadius` (edge distance), and a truthy `ballData` tag marking it as a
 *  ball. Both real Matter bodies and test fakes satisfy it. */
export interface DoorBallBody {
  position: Vec2;
  circleRadius?: number;
  ballData?: unknown;
}

/**
 * The ball a trap-door tap would grab: nearest the fixed door mouth (`mouthX`, `doorY`) by
 * EDGE distance (centre-to-mouth minus `circleRadius`, so a bigger ball whose edge reaches
 * nearer wins), among bodies tagged with `ballData` and still above the door
 * (`position.y <= doorY`). `undefined` when no ball qualifies. Pure so it can be unit-tested
 * and shared by both Zone C's grab and Zone A's candidate highlight — one source of truth,
 * so the two can never disagree about which ball is next.
 */
export function nearestDoorBall<T extends DoorBallBody>(
  bodies: Iterable<T>,
  mouthX: number,
  doorY: number,
): T | undefined {
  let best: T | undefined;
  let bestDist = Infinity;
  for (const body of bodies) {
    if (!body.ballData) continue;
    if (body.position.y > doorY) continue;
    const dx = body.position.x - mouthX;
    const dy = body.position.y - doorY;
    const dist = Math.hypot(dx, dy) - (body.circleRadius ?? 0);
    if (dist < bestDist) {
      bestDist = dist;
      best = body;
    }
  }
  return best;
}

/** A ball "rests above the line" when its centre is above lineY AND it's slow. */
export function isRestingAbove(
  centerY: number,
  speed: number,
  lineY: number,
  speedThreshold: number,
): boolean {
  return centerY < lineY && speed < speedThreshold;
}

/** Overflow (game over) once accumulated rest time reaches the threshold. */
export function isOverflow(restMs: number, thresholdMs: number): boolean {
  return restMs >= thresholdMs;
}

/**
 * A slow ball whose centre sits just below the line — inside the warning band
 * `[lineY, lineY + band)` — but not yet over it. Drives the red death-line warning.
 */
export function isNearDeath(
  centerY: number,
  speed: number,
  lineY: number,
  band: number,
  speedThreshold: number,
): boolean {
  return speed < speedThreshold && centerY >= lineY && centerY < lineY + band;
}
