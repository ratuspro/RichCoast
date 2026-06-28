import { TIER_COUNT } from '../core/contracts';
import { FRICTION_BASE, FRICTION_MAX, FRICTION_STEP, RADII } from './tuning';

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

/** Visual/physics radius for a tier (1-based). Out-of-range tiers clamp to the ends. */
export function radiusForTier(tier: number): number {
  return RADII[clampTier(tier) - 1];
}

/** Surface friction for a tier — grows with tier, clamped to FRICTION_MAX. */
export function frictionForTier(tier: number): number {
  const raw = FRICTION_BASE + FRICTION_STEP * (clampTier(tier) - 1);
  return Math.min(raw, FRICTION_MAX);
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

/** Accumulate rest time: previous + delta while resting, else reset to 0. */
export function nextRestMs(prev: number, delta: number, resting: boolean): number {
  return resting ? prev + delta : 0;
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
