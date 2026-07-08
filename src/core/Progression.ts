import data from './progression.json';

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
}

/** Levels between arena zoom-out milestones (50, 100, 150, …). The draw-window *shift-ups* in
 *  `progression.json` MUST land on these same levels — the milestone reads the new window's floor
 *  (`stage.ballWindow[0]`) as its blacklist threshold, so a window that steps between milestones
 *  would desync the scale/blacklist from the spawn pool. */
export const MILESTONE_EVERY = 50;

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

/** Returns the active stage for a given internal level (1-based). */
export function getStage(level: number): ProgressionStage {
  let result = stages[0];
  for (const stage of stages) {
    if (stage.fromLevel <= level) result = stage;
    else break;
  }
  return result;
}
