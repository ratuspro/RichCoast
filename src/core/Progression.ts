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
