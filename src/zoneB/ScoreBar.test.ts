import { describe, test, expect } from 'vitest';
import { ScoreBar } from './ScoreBar';

describe('ScoreBar', () => {
  test('accumulates points without filling', () => {
    const bar = new ScoreBar(10);
    expect(bar.add(4)).toBe(false);
    expect(bar.getFilled()).toBe(4);
  });

  test('returns true and pins filled at the over-target value on first fill', () => {
    const bar = new ScoreBar(10);
    expect(bar.add(7)).toBe(false);
    expect(bar.add(6)).toBe(true); // 13 >= 10
    expect(bar.getFilled()).toBe(13);
    expect(bar.isCashingIn()).toBe(true);
  });

  test('further points while cashing in are banked, not shown', () => {
    const bar = new ScoreBar(10);
    bar.add(10); // fills exactly, enters cash-in
    expect(bar.add(5)).toBe(false); // banked, no new fill event
    expect(bar.getFilled()).toBe(10); // unchanged while cashing in
  });

  test('completeCashIn resets to the banked overflow and exits cash-in when below target', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    bar.add(3); // banked overflow
    expect(bar.completeCashIn()).toBe(false);
    expect(bar.getFilled()).toBe(3);
    expect(bar.isCashingIn()).toBe(false);
  });

  test('completeCashIn cascades when the banked overflow alone reaches target', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    bar.add(12); // overflow alone already >= target
    expect(bar.completeCashIn()).toBe(true);
    expect(bar.getFilled()).toBe(12);
    expect(bar.isCashingIn()).toBe(true);
  });

  test('completeCashIn with no overflow resets to zero', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    expect(bar.completeCashIn()).toBe(false);
    expect(bar.getFilled()).toBe(0);
  });

  test('add works normally again after a non-cascading completeCashIn', () => {
    const bar = new ScoreBar(10);
    bar.add(10);
    bar.completeCashIn();
    expect(bar.add(9)).toBe(false);
    expect(bar.add(2)).toBe(true);
  });
});
