import { describe, test, expect } from 'vitest';
import { BallBuffer } from './BallBuffer';

describe('BallBuffer', () => {
  test('starts with initial count', () => {
    expect(new BallBuffer(20, 10, 50, 2.5).getCount()).toBe(20);
  });

  test('spend reduces count by 1 and returns true', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    expect(buf.spend()).toBe(true);
    expect(buf.getCount()).toBe(19);
  });

  test('spend returns false when exhausted', () => {
    const buf = new BallBuffer(1, 10, 50, 2.5);
    buf.spend();
    expect(buf.spend()).toBe(false);
    expect(buf.getCount()).toBe(0);
  });

  test('isExhausted when count reaches 0', () => {
    const buf = new BallBuffer(1, 10, 50, 2.5);
    buf.spend();
    expect(buf.isExhausted()).toBe(true);
  });

  test('refillIfMilestone returns false below milestone', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    expect(buf.refillIfMilestone(49)).toBe(false);
    expect(buf.getCount()).toBe(20);
  });

  test('refillIfMilestone adds refillAmount when milestone reached', () => {
    const buf = new BallBuffer(1, 10, 50, 2.5);
    buf.spend();
    expect(buf.refillIfMilestone(50)).toBe(true);
    expect(buf.getCount()).toBe(10);
  });

  test('milestone escalates after refill', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    buf.refillIfMilestone(50);
    expect(buf.getNextMilestone()).toBe(125);
  });

  test('does not refill twice for the same milestone', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    buf.refillIfMilestone(50);
    expect(buf.refillIfMilestone(50)).toBe(false);
  });

  test('milestone escalates again on second refill', () => {
    const buf = new BallBuffer(20, 10, 50, 2.5);
    buf.refillIfMilestone(50);
    buf.refillIfMilestone(125);
    expect(buf.getNextMilestone()).toBe(313);
  });
});
