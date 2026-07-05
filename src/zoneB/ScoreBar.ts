/** Points needed to fill the bar and trigger a buffer refill. Tunable. */
export const SCORE_BAR_TARGET = 10;

/**
 * Pure score-bar logic — no Phaser dependency, fully unit-testable.
 *
 * `add(points)` accumulates points and returns `true` the moment the bar first
 * crosses `target`. Crossing does NOT reset the bar immediately — it enters a
 * "cashing in" state where `filled` stays pinned at its (possibly over-target)
 * value, so a caller can visually dwell on a full bar, and any further points
 * are banked into `overflow` instead of shown. Call `completeCashIn()` once
 * that dwell/drain-out sequence finishes to actually reset the bar and carry
 * the banked overflow into the next cycle.
 */
export class ScoreBar {
  private filled = 0;
  private overflow = 0;
  private cashingIn = false;

  constructor(private target = SCORE_BAR_TARGET) {}

  /** Returns true the moment this addition first crosses the target (enters cash-in). */
  add(points: number): boolean {
    if (this.cashingIn) {
      this.overflow += points;
      return false;
    }
    this.filled += points;
    if (this.filled >= this.target) {
      this.cashingIn = true;
      return true;
    }
    return false;
  }

  /**
   * Resolve a cash-in: reset `filled` to the banked `overflow` (0 if none) and
   * clear it. If the carried-over amount alone already reaches `target`, stays
   * in cash-in state and returns true (the caller should immediately begin
   * another dwell/drain-out cycle); otherwise clears cash-in state and returns
   * false.
   */
  completeCashIn(): boolean {
    this.filled = this.overflow;
    this.overflow = 0;
    this.cashingIn = this.filled >= this.target;
    return this.cashingIn;
  }

  isCashingIn(): boolean { return this.cashingIn; }
  setTarget(target: number): void { this.target = target; }
  getFilled(): number { return this.filled; }
  getTarget(): number { return this.target; }
  getProgress(): number { return this.filled / this.target; }
}
