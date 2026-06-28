/**
 * THE SEAM between the two halves of the game.
 *
 * Dev 1 (Zone A/C + shell) and Dev 2 (Zone B) only ever couple through the event
 * names, payloads and `GameSystem` interface declared here. Changing anything in
 * this file is a both-devs-must-agree action — treat it as frozen otherwise.
 *
 * Keep this file Phaser-free at runtime: the only Phaser reference is a type-only
 * import (erased at compile), so the seam can be unit-tested in plain Node.
 */
import type Phaser from 'phaser';

// ---------------------------------------------------------------------------
// Tiers & values
// ---------------------------------------------------------------------------

/** Number of distinct merge tiers (powers of two). Tier 1 is the smallest ball. */
export const TIER_COUNT = 10;

/** Tier (1-based) → ball value: 2^(tier-1). tier 1→1, 2→2, 3→4, 4→8, … */
export function tierToValue(tier: number): number {
  return 2 ** (tier - 1);
}

/**
 * A ball's identity, independent of where it lives. Shared by the drop payload
 * and by the `{ value, tier }` data carried on every ball's Matter body — so any
 * system can read a ball's identity straight off a physics query.
 */
export interface BallSpec {
  value: number;
  tier: number;
}

/** Data attached to a ball's Matter body (`body.gameObject` / `body.plugin`). */
export type BallBodyData = BallSpec;

// ---------------------------------------------------------------------------
// Cross-zone events
// ---------------------------------------------------------------------------

/**
 * Canonical event names. Use these constants everywhere — never raw strings —
 * so a rename is a single edit and the typed map below stays in lock-step.
 */
export const GameEvent = {
  /** Zone C → Zone B: trap-door fired; Zone B spawns one ball at entry `x`. */
  BallDropped: 'BALL_DROPPED',
  /** Zone B → Zone C: ≥1 ball in flight; Zone C locks the trap-door. */
  ZoneBBusy: 'ZONE_B_BUSY',
  /** Zone B → Zone C: no balls in flight; Zone C may re-arm the trap-door. */
  ZoneBEmpty: 'ZONE_B_EMPTY',
  /** Zone B → HUD: running cumulative score total changed. */
  ScoreChanged: 'SCORE_CHANGED',
  /** Zone B → all: score bar filled and reset; Zone A should refill its ball buffer. */
  ScoreBarFilled: 'SCORE_BAR_FILLED',
  /** Zone B → HUD: score bar progress changed (for the fill bar visual). */
  ScoreBarChanged: 'SCORE_BAR_CHANGED',
  /** Zone A → HUD: ball buffer count changed. */
  BallBufferChanged: 'BALL_BUFFER_CHANGED',
  /** Zone A → all: internal level advanced; carries the new stage parameters. */
  ProgressionChanged: 'PROGRESSION_CHANGED',
} as const;

export type GameEventName = (typeof GameEvent)[keyof typeof GameEvent];

export interface BallDroppedPayload extends BallSpec {
  /**
   * Horizontal entry into Zone B.
   *
   * FROZEN DECISION: Zone C always sends the fixed Zone B entry column
   * (`Layout.zoneBEntry.x`). The field stays in the payload for honesty/future
   * flexibility, but keeping it constant is deliberate — it makes the arena's
   * outcomes layout-driven and readable instead of depending on where the ball
   * happened to sit in Zone A.
   */
  x: number;
}

export interface ScoreChangedPayload {
  total: number;
}

export interface ScoreBarChangedPayload {
  filled: number;
  target: number;
}

export interface BallBufferChangedPayload {
  count: number;
}

export interface ProgressionChangedPayload {
  level: number;
  minTier: number;
  maxTier: number;
  bufferCapacity: number;
  scoreBarTarget: number;
}

/** Event name → payload type. `void` = a signal with no data. */
export interface GameEventMap {
  [GameEvent.BallDropped]: BallDroppedPayload;
  [GameEvent.ZoneBBusy]: void;
  [GameEvent.ZoneBEmpty]: void;
  [GameEvent.ScoreChanged]: ScoreChangedPayload;
  [GameEvent.ScoreBarFilled]: void;
  [GameEvent.ScoreBarChanged]: ScoreBarChangedPayload;
  [GameEvent.BallBufferChanged]: BallBufferChangedPayload;
  [GameEvent.ProgressionChanged]: ProgressionChangedPayload;
}

// ---------------------------------------------------------------------------
// System interface
// ---------------------------------------------------------------------------

/**
 * Every zone (and stub/harness) implements this so `GameScene` can wire them all
 * identically: build, tick, tear down. A system talks to other systems ONLY via
 * the event bus — never by importing or calling another zone's code.
 */
export interface GameSystem {
  create(scene: Phaser.Scene): void;
  update(time: number, delta: number): void;
  /** Optional cleanup on scene shutdown / `?zone=` mode switch. */
  destroy?(): void;
}
