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

/**
 * Size of the base tier table — the radius table and colour palette have this many
 * entries. It is NOT a gameplay ceiling: merges are uncapped, so tiers climb past this,
 * with balls cycling colours (modulo) and growing by formula beyond the table. Tier 1 is
 * the smallest ball.
 */
export const TIER_COUNT = 10;

/**
 * Tier (1-based) → ball value: 3^(tier-1). tier 1→1, 2→3, 3→9, 4→27, …
 * Merges only join two equal balls and yield 1.5*(V+V) = 3V, so each merge triples the
 * value — making the value ladder powers of three.
 */
export function tierToValue(tier: number): number {
  return 3 ** (tier - 1);
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
  /** Zone A → Zone C: arena zoom-out (milestone) is animating; lock input while `active`. */
  ArenaZoom: 'ARENA_ZOOM',
  /** PhaseDirector → all: the gameplay phase (or pan transition) changed. Zone A aims only
   *  in 'A', Zone C's trap-door arms only in 'B'; both stay locked through the pan states. */
  PhaseChanged: 'PHASE_CHANGED',
  /** Zone A → PhaseDirector: ball buffer empty AND the board has settled — begin the pan
   *  down into the Zone B phase. */
  ZoneADepleted: 'ZONE_A_DEPLETED',
  /** ThemeDirector → all: the active `Theme` palette was mutated — re-read `Theme` and
   *  restyle any colour baked at create(). Emitted once per tween tick while a milestone
   *  palette cross-fade runs, so handlers must be cheap re-style calls (setFillStyle /
   *  setColor / a small Graphics redraw), never rebuilds. No payload: `Theme` IS the data. */
  ThemeChanged: 'THEME_CHANGED',
} as const;

export type GameEventName = (typeof GameEvent)[keyof typeof GameEvent];

export interface BallDroppedPayload extends BallSpec {
  /**
   * Horizontal entry into Zone B.
   *
   * Chosen by the player: Zone C runs a marker that sweeps left↔right across the
   * trap-door band, and a tap freezes it — the marker's column at that instant is
   * the entry `x`. It lands in `[Layout.zoneB.x + margin, zoneB.x + width - margin]`
   * (inset so a ball never spawns into a side wall). Zone B spawns at exactly this
   * `x`, making WHERE a ball enters a timing skill rather than a fixed column.
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

export interface ArenaZoomPayload {
  /** True while the zoom-out tween is running (input should stay locked); false when it lands. */
  active: boolean;
}

/**
 * The two exclusive play phases and the pan transitions between them.
 * 'A' = drop balls in Zone A; 'B' = tap Zone C's trap-door to feed Zone B;
 * 'A_TO_B' / 'B_TO_A' = the camera pan is animating and ALL input is locked.
 */
export type GamePhase = 'A' | 'A_TO_B' | 'B' | 'B_TO_A';

export interface PhaseChangedPayload {
  phase: GamePhase;
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
  [GameEvent.ArenaZoom]: ArenaZoomPayload;
  [GameEvent.PhaseChanged]: PhaseChangedPayload;
  [GameEvent.ZoneADepleted]: void;
  [GameEvent.ThemeChanged]: void;
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
