/** Points needed to fill the bar and trigger a buffer refill. Tunable. */
export const SCORE_BAR_TARGET = 10;

/**
 * Pure score-bar logic — no Phaser dependency, fully unit-testable.
 *
 * Deliberately minimal: it holds the running `filled` toward the current `target`. Points
 * accumulate through `add()`; whenever `crossedTarget()` is true the caller consumes one
 * level with `consumeLevel()` (subtracting the current target) and — via
 * SCORE_BAR_FILLED → PROGRESSION_CHANGED → `setTarget()` — may raise the target for the next
 * level, then re-checks. Because levels are consumed one at a time against their own target,
 * a single big `add()` can roll through several levels and land the exact remainder.
 *
 * No pinning / cash-in state: the bar wraps LIVE as balls drain (ZoneBSystem drives the
 * fill/empty animation), so the logic never has to hold a "full" state.
 */
export class ScoreBar {
  private filled = 0;

  constructor(private target = SCORE_BAR_TARGET) {}

  /** Accumulate drained score into the current level's fill. */
  add(points: number): void {
    this.filled += points;
  }

  /** True while the current fill has reached the target — the caller should consume a level. */
  crossedTarget(): boolean {
    return this.filled >= this.target;
  }

  /** Consume one level's worth: subtract the current target from `filled`. */
  consumeLevel(): void {
    this.filled -= this.target;
  }

  /** Safety valve: discard any fill at/above the target, leaving a nearly-full (99%) bar.
   *  The caller invokes this when a freak drain hits its levels-per-cash-in cap — the
   *  excess is forfeited so the crossing loop terminates instead of banking thousands of
   *  owed wraps. No-op below the target. */
  forfeitOverflow(): void {
    this.filled = Math.min(this.filled, this.target * 0.99);
  }

  setTarget(target: number): void { this.target = target; }
  getFilled(): number { return this.filled; }
  getTarget(): number { return this.target; }
  getProgress(): number { return this.filled / this.target; }
}
