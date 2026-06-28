import { describe, test, expect } from 'vitest';
import { newComboState, nextCombo } from './comboPitch';

const WINDOW = 500;

describe('comboPitch', () => {
  test('first trigger starts at step 0, unity pitch', () => {
    const s = newComboState();
    const r = nextCombo(s, 1000, WINDOW);
    expect(r.step).toBe(0);
    expect(r.mult).toBe(1);
  });

  test('a second trigger within the window steps the pitch up', () => {
    const s = newComboState();
    nextCombo(s, 1000, WINDOW);
    const r = nextCombo(s, 1000 + WINDOW - 1, WINDOW); // just inside
    expect(r.step).toBe(1);
    expect(r.mult).toBeCloseTo(2 ** (1 / 12));
  });

  test('the step keeps climbing while the chain stays inside the window', () => {
    const s = newComboState();
    let now = 0;
    for (let i = 0; i < 4; i++) {
      nextCombo(s, now, WINDOW);
      now += 100;
    }
    expect(s.step).toBe(3);
  });

  test('a gap at or beyond the window resets the chain', () => {
    const s = newComboState();
    nextCombo(s, 1000, WINDOW);
    nextCombo(s, 1100, WINDOW); // step 1
    const r = nextCombo(s, 1100 + WINDOW, WINDOW); // exactly window away → reset
    expect(r.step).toBe(0);
    expect(r.mult).toBe(1);
  });

  test('the step is capped so long chains do not get shrill', () => {
    const s = newComboState();
    let now = 0;
    for (let i = 0; i < 50; i++) {
      nextCombo(s, now, WINDOW, 8);
      now += 50;
    }
    expect(s.step).toBe(8);
  });
});
