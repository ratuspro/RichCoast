/** Points needed to fill the bar and trigger a buffer refill. Tunable. */
export const SCORE_BAR_TARGET = 10;

/**
 * Pure score-bar logic — no Phaser dependency, fully unit-testable.
 *
 * `add(points)` accumulates points and returns `true` the moment the bar
 * fills (it resets to zero and the caller should emit ScoreBarFilled).
 */
export class ScoreBar {
  private filled = 0;

  constructor(private readonly target = SCORE_BAR_TARGET) { }

  /** Returns true if this addition caused the bar to fill (and resets it). */
  add(points: number): boolean {
    this.filled += points;
    if (this.filled >= this.target) {
      this.filled = 0;
      return true;
    }
    return false;
  }

  getFilled(): number { return this.filled; }
  getTarget(): number { return this.target; }
  /** 0–1 progress fraction. */
  getProgress(): number { return this.filled / this.target; }
}
