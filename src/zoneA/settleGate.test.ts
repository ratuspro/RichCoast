import { describe, expect, it } from 'vitest';
import {
  SETTLE_HOLD_MS,
  SETTLE_TIMEOUT_MS,
  advanceSettleGate,
  initialSettleGate,
  type SettleGateState,
} from './settleGate';

/** Drive the gate with fixed-delta frames; returns state + whether any frame fired. */
function run(
  state: SettleGateState,
  frames: Array<{ ms: number; settled: boolean }>,
): { state: SettleGateState; fired: boolean } {
  let fired = false;
  for (const f of frames) {
    const step = advanceSettleGate(state, f.ms, f.settled);
    state = step.state;
    fired ||= step.fire;
  }
  return { state, fired };
}

describe('settleGate', () => {
  it('fires after a contiguous settled hold', () => {
    const frames = Array.from({ length: 25 }, () => ({ ms: 16, settled: true })); // 400ms
    expect(run(initialSettleGate(), frames).fired).toBe(true);
  });

  it('does not fire before the hold completes', () => {
    const frames = Array.from({ length: 10 }, () => ({ ms: 16, settled: true })); // 160ms
    expect(run(initialSettleGate(), frames).fired).toBe(false);
  });

  it('resets the hold when motion resumes', () => {
    const { state, fired } = run(initialSettleGate(), [
      { ms: SETTLE_HOLD_MS - 10, settled: true },
      { ms: 16, settled: false }, // a merge kicked balls around — start over
    ]);
    expect(fired).toBe(false);
    expect(state.holdMs).toBe(0);

    // Needs the full hold again after the reset.
    expect(run(state, [{ ms: SETTLE_HOLD_MS - 10, settled: true }]).fired).toBe(false);
    expect(run(state, [{ ms: SETTLE_HOLD_MS, settled: true }]).fired).toBe(true);
  });

  it('fires at the hard timeout even if the board never settles', () => {
    const frames = Array.from({ length: Math.ceil(SETTLE_TIMEOUT_MS / 16) + 1 }, () => ({
      ms: 16,
      settled: false,
    }));
    expect(run(initialSettleGate(), frames).fired).toBe(true);
  });

  it('keeps totalMs accumulating across hold resets', () => {
    const { state } = run(initialSettleGate(), [
      { ms: 100, settled: true },
      { ms: 100, settled: false },
      { ms: 100, settled: true },
    ]);
    expect(state.totalMs).toBe(300);
    expect(state.holdMs).toBe(100);
  });
});
