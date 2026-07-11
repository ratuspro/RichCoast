import { describe, test, expect } from 'vitest';
import { ScoreBar } from './ScoreBar';

/** Drives ScoreBar the way ZoneBSystem does live: add points, then consume one level per
 *  crossing, updating the target between crossings (as Zone A does via ProgressionChanged).
 *  Returns how many levels were crossed. */
function consumeCrossings(bar: ScoreBar, nextTarget: () => number): number {
  let levels = 0;
  while (bar.crossedTarget()) {
    bar.consumeLevel();
    levels += 1;
    bar.setTarget(nextTarget());
  }
  return levels;
}

describe('ScoreBar', () => {
  test('accumulates points without crossing', () => {
    const bar = new ScoreBar(10);
    bar.add(4);
    expect(bar.getFilled()).toBe(4);
    expect(bar.crossedTarget()).toBe(false);
  });

  test('crossedTarget is true at or above the target', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    expect(bar.crossedTarget()).toBe(true);
  });

  test('consumeLevel subtracts the current target from filled', () => {
    const bar = new ScoreBar(10);
    bar.add(13);
    bar.consumeLevel();
    expect(bar.getFilled()).toBe(3);
  });

  test('a single add can cross several levels, landing the exact remainder', () => {
    const bar = new ScoreBar(4); // level-1 target
    bar.add(50);
    const targets = [30, 40, 55]; // successive level targets
    let i = 0;
    const levels = consumeCrossings(bar, () => targets[i++]);
    // 50 - 4 = 46 (L2); 46 - 30 = 16 (L3); 16 < 40 -> stop.
    expect(levels).toBe(2);
    expect(bar.getFilled()).toBe(16);
    expect(bar.getTarget()).toBe(40);
  });

  test('exact fill lands empty on the next level', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    const levels = consumeCrossings(bar, () => 30);
    expect(levels).toBe(1);
    expect(bar.getFilled()).toBe(0);
    expect(bar.crossedTarget()).toBe(false);
  });

  test('progress reflects the current fill against the current target', () => {
    const bar = new ScoreBar(20);
    bar.add(5);
    expect(bar.getProgress()).toBeCloseTo(0.25);
  });

  test('forfeitOverflow drops an over-target fill to just under full', () => {
    // The safety valve for a freak monster drain: the caller stops consuming levels at its
    // cap and forfeits the rest, leaving a nearly-full bar (not a crossed one).
    const bar = new ScoreBar(100);
    bar.add(1_000_000);
    bar.forfeitOverflow();
    expect(bar.crossedTarget()).toBe(false);
    expect(bar.getProgress()).toBeCloseTo(0.99);
  });

  test('forfeitOverflow is a no-op below the target', () => {
    const bar = new ScoreBar(100);
    bar.add(42);
    bar.forfeitOverflow();
    expect(bar.getFilled()).toBe(42);
  });
});
