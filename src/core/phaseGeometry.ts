/**
 * Camera framing for the two gameplay phases — pure numbers, Phaser-free.
 *
 * The world is taller than the 390×844 screen (Zone B's band ends at world y=1238) and the
 * game "pans" between two framings instead of ever moving world objects:
 *
 *  - A-phase (pan 0):   main camera scrollY 0; the arena camera's viewport shows Zone A's
 *    full 465px board band (HUD + board ≈ 60% of the screen). Zone B pokes in at the
 *    bottom, cropped by PAN_DISTANCE.
 *  - B-phase (pan 394): main camera scrollY 394; the arena viewport shrinks to 71px
 *    (HUD + board = a 113px sliver) — the arena camera keeps its floor-anchored centring
 *    (ArenaView), so Zone A is TOP-cropped at unchanged zoom. Zone B's full 687px band
 *    exactly fills the screen down to y=844.
 *
 * The single tween proxy `pan ∈ [0, PAN_DISTANCE]` drives BOTH cameras through
 * `framingForPan`, so the arena-bottom / Zone-C seam stays pixel-locked mid-pan.
 * Both PhaseDirector (the pan tween) and ArenaView (initial viewport + zoom recentring)
 * read from here — one source of truth, unit-testable.
 */
import * as Layout from './Layout';

/** Height of the HUD chrome bar; the arena viewport starts just below it (see HUD.BAND_H). */
export const HUD_H = 42;

/** Arena camera viewport height in the A-phase: the full Zone A board band. */
export const ARENA_VIEW_H_A = Layout.zoneA.height - HUD_H; // 465

/**
 * How far the main camera scrolls down between the phases: Zone B's world overhang
 * below the screen (1238−844), so the B-phase brings Zone B's bottom edge exactly
 * flush with the screen bottom. The arena viewport shrink equals it by construction
 * (asserted in tests).
 */
export const PAN_DISTANCE = Layout.zoneB.y + Layout.zoneB.height - Layout.HEIGHT; // 394

/** Arena camera viewport height in the B-phase: whatever the pan leaves of Zone A
 *  on screen (top-cropped) — HUD + this = the B-phase Zone A sliver. */
export const ARENA_VIEW_H_B = ARENA_VIEW_H_A - PAN_DISTANCE; // 71

export interface PhaseFraming {
  /** Main camera scrollY (Zone C, Zone B and the backdrop ride this). */
  scrollY: number;
  /** Arena camera viewport height (its y stays HUD_H; shrinking crops Zone A's top). */
  arenaViewportH: number;
}

/** Framing for an in-between pan value; pan 0 = A-phase, pan PAN_DISTANCE = B-phase. */
export function framingForPan(pan: number): PhaseFraming {
  const p = Math.round(Math.max(0, Math.min(PAN_DISTANCE, pan)));
  return { scrollY: p, arenaViewportH: ARENA_VIEW_H_A - p };
}

/**
 * Arena-camera world-space centre y for a given viewport height and arena scale `s`
 * (camera zoom is 1/s): the funnel floor stays pinned to the viewport's bottom edge,
 * so the visible world spans `viewportH·s` ending at the Zone A/C boundary.
 */
export function arenaCenterY(viewportH: number, s: number): number {
  const floorY = Layout.zoneA.y + Layout.zoneA.height;
  return floorY - (viewportH / 2) * s;
}
