import data from './progression.json';
import type { PaletteName } from './Theme';

export interface ProgressionStage {
  fromLevel: number;
  ballWindow: [number, number];
  scoreBarTarget: number;
  bufferBalls?: number[];
  /**
   * Milestone difficulty knob (default 1): multiplies the neutral arena-growth factor that
   * would keep apparent ball size constant. <1 = arena grows less than the balls = tighter/
   * harder; >1 = roomier breather. Only meaningful on stages whose ballWindow shifts.
   */
  tightness?: number;
  /**
   * Environment palette the milestone swaps to (a `PALETTES` key in `Theme.ts`). Authored
   * on the draw-window-shift stages; levels between/after authored stages hold the last
   * one (author-then-hold, resolved by `paletteNameForLevel`).
   */
  palette?: PaletteName;
}

/** Levels between arena zoom-out milestones (20, 40, 60, …). The draw-window *shift-ups* in
 *  `progression.json` MUST land on these same levels — the milestone reads the new window's floor
 *  (`stage.ballWindow[0]`) as its blacklist threshold, so a window that steps between milestones
 *  would desync the scale/blacklist from the spawn pool. */
export const MILESTONE_EVERY = 20;

/**
 * Fraction of the way from the last milestone to the next one, in [0, 1).
 * 0 the level a milestone lands (the HUD bar resets), climbing back toward 1 after.
 */
export function milestoneProgress(level: number): number {
  return (level % MILESTONE_EVERY) / MILESTONE_EVERY;
}

const stages: ProgressionStage[] = (data.stages as ProgressionStage[])
  .slice()
  .sort((a, b) => a.fromLevel - b.fromLevel);

/**
 * Balls the player is refilled to at a given internal level (1-based) — the single source of
 * truth for the ball-supply ramp, decoupled from the milestone stage table.
 *
 * The supply OSCILLATES instead of growing forever: a slowly-rising base (15 → 18 by level
 * 30, flat after) swings ±2 by level parity — even levels are roomy "harvest" refills, odd
 * levels are lean "pressure" refills that lean on balls carried over on the board. Milestone
 * levels always pay the harvest amount (a breather to enjoy the new draw window). The cap
 * (base 18 + 2 = 20 drops) is deliberate: difficulty comes from ball size vs arena room,
 * never from a long drop chore. Sequence: 8, 17, 13, 17, 13, … 18/14 … → 20/16.
 */
export function bufferForLevel(level: number): number {
  if (level <= 1) return 8; // tutorial start: window [1,1], a couple of merge chains' worth
  const base = Math.min(15 + Math.floor(level / 10), 18);
  if (level % MILESTONE_EVERY === 0) return base + 2; // milestones are always a breather
  return level % 2 === 0 ? base + 2 : base - 2;
}

/**
 * Extra refill balls per level crossed BEYOND the first in one cash-in cycle (one Zone B
 * session rolling the bar through several levels). Only the final level's refill runs after
 * a burst, so without this a 4-level burst pays exactly like a 1-level fill — the bonus
 * makes the roll-through jackpot tangible in the resource that matters.
 */
export const BURST_REFILL_BONUS = 2;

/**
 * The environment palette active at a given internal level: the last stage at-or-below
 * `level` that authors one, else the base `'workshop'`. Author-then-hold — past the last
 * authored palette the look stays put, mirroring how window shifts self-heal.
 */
export function paletteNameForLevel(level: number): PaletteName {
  let result: PaletteName = 'workshop';
  for (const stage of stages) {
    if (stage.fromLevel > level) break;
    if (stage.palette) result = stage.palette;
  }
  return result;
}

/**
 * Per-level growth of the score-bar target past the last authored stage. Ball values triple
 * per tier and (pre-tail) the draw window shifts +4 tiers every MILESTONE_EVERY levels, so
 * value magnitude — and the authored anchor curve tracking it (5K@20 → 2.7B@80 ≈ ×3¹² over
 * 60 levels) — grows ×3⁴ per milestone span. The tail continues that same rate so one good
 * drain keeps earning ~one level forever; a flat tail lets ever-tripling merged balls cross
 * a frozen target thousands of times in one drain (the "endless wrap" bug).
 */
export const TAIL_TARGET_GROWTH = 3 ** (4 / MILESTONE_EVERY);

/**
 * The score-bar target for a given internal level. The authored stages are ANCHORS, not
 * plateaus: between two anchors the target grows geometrically per level (the ratio of the
 * anchors spread evenly across the span), and past the last anchor it keeps growing at
 * TAIL_TARGET_GROWTH per level. Per-level growth matters because one Zone B drain can cross
 * several levels in a burst — each crossed level must immediately raise the next bar
 * (~×1.3–1.4) so a monster drain self-limits to a few levels instead of wrapping through a
 * flat plateau many times over.
 */
export function scoreBarTargetForLevel(level: number): number {
  const last = stages[stages.length - 1];
  if (level >= last.fromLevel) {
    return Math.round(last.scoreBarTarget * TAIL_TARGET_GROWTH ** (level - last.fromLevel));
  }
  let i = 0;
  while (i + 1 < stages.length && stages[i + 1].fromLevel <= level) i++;
  const a = stages[i];
  const b = stages[i + 1];
  const t = (level - a.fromLevel) / (b.fromLevel - a.fromLevel);
  return Math.round(a.scoreBarTarget * (b.scoreBarTarget / a.scoreBarTarget) ** t);
}

/** Returns the active stage for a given internal level (1-based). */
export function getStage(level: number): ProgressionStage {
  let result = stages[0];
  for (const stage of stages) {
    if (stage.fromLevel <= level) result = stage;
    else break;
  }
  return result;
}

/** True past the last authored stage — the endless TAIL, where windows/zooms follow the
 *  self-healing policies below instead of authored stage data. */
export function isTailLevel(level: number): boolean {
  return level > stages[stages.length - 1].fromLevel;
}

/** Draw-window step per TAIL milestone: half the authored +4, so the material ladder keeps
 *  advancing (wrapping past tier 20 with gold rings) but the endgame climbs gently. */
export const TAIL_WINDOW_STEP = 2;

/**
 * The live draw window for a given internal level. Authored `ballWindow` through the last
 * stage; past it the window keeps stepping up TAIL_WINDOW_STEP tiers at every milestone
 * ([17,20] → [19,22] @ the first tail milestone → [21,24] → …), so the supply's value keeps
 * growing (×3² per step) against the tail target's ×3⁴ — the gap, plus the under-neutral
 * tail arena zoom, is the designed endgame squeeze.
 */
export function windowForLevel(level: number): [number, number] {
  const window = getStage(level).ballWindow;
  if (!isTailLevel(level)) return [window[0], window[1]];
  const last = stages[stages.length - 1];
  const shifts = Math.floor((level - last.fromLevel) / MILESTONE_EVERY);
  return [window[0] + TAIL_WINDOW_STEP * shifts, window[1] + TAIL_WINDOW_STEP * shifts];
}
