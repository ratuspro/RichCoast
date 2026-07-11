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

/** Levels between arena zoom-out milestones (25, 50, 75, …). The draw-window *shift-ups* in
 *  `progression.json` MUST land on these same levels — the milestone reads the new window's floor
 *  (`stage.ballWindow[0]`) as its blacklist threshold, so a window that steps between milestones
 *  would desync the scale/blacklist from the spawn pool. */
export const MILESTONE_EVERY = 25;

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
 * truth for the ball-supply ramp, decoupled from the milestone stage table. The sequence by
 * level is 5, 5, 7, 9, 11, 12, 13, 14, … : the run starts with 5 and the first bar-fill also
 * lands 5, the next three fills add +2 each (7, 9, 11), and every fill after that adds +1.
 */
export function bufferForLevel(level: number): number {
  if (level <= 2) return 5; // start (L1) and the first fill (L2)
  if (level <= 5) return 5 + 2 * (level - 2); // three +2 fills: L3=7, L4=9, L5=11
  return 11 + (level - 5); // +1 per fill thereafter: L6=12, L7=13, …
}

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
 * value magnitude — and the authored target curve tracking it (105K@50 → 670M@100 ≈ ×3⁸ over
 * 50 levels) — grows ×3⁴ per milestone span. The tail continues that same rate so one good
 * drain keeps earning ~one level forever; a flat tail lets ever-tripling merged balls cross
 * a frozen target thousands of times in one drain (the "endless wrap" bug).
 */
export const TAIL_TARGET_GROWTH = 3 ** (4 / MILESTONE_EVERY);

/**
 * The score-bar target for a given internal level: the authored stage value through the
 * last authored stage, then geometric self-healing at TAIL_TARGET_GROWTH per level —
 * mirroring how the draw window and palette self-heal, but growing instead of holding.
 */
export function scoreBarTargetForLevel(level: number): number {
  const last = stages[stages.length - 1];
  if (level <= last.fromLevel) return getStage(level).scoreBarTarget;
  return Math.round(last.scoreBarTarget * TAIL_TARGET_GROWTH ** (level - last.fromLevel));
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
