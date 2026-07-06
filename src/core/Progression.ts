import data from './progression.json';

export interface ProgressionStage {
  fromLevel: number;
  ballWindow: [number, number];
  bufferCapacity: number;
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

/** Returns the active stage for a given internal level (1-based). */
export function getStage(level: number): ProgressionStage {
  let result = stages[0];
  for (const stage of stages) {
    if (stage.fromLevel <= level) result = stage;
    else break;
  }
  return result;
}
