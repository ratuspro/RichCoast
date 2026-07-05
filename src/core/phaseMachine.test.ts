import { describe, expect, it } from 'vitest';
import { initialPhaseState, stepPhase, type PhaseState } from './phaseMachine';

const at = (phase: PhaseState['phase'], refillQueued = false): PhaseState => ({
  phase,
  refillQueued,
});

describe('phaseMachine', () => {
  it('starts in the A phase with no queued refill', () => {
    expect(initialPhaseState()).toEqual({ phase: 'A', refillQueued: false });
  });

  it('runs the full loop: A → A_TO_B → B → B_TO_A → A', () => {
    let s = initialPhaseState();

    let step = stepPhase(s, 'depleted');
    expect(step).toMatchObject({ startPan: 'B', changed: true });
    expect(step.state.phase).toBe('A_TO_B');
    s = step.state;

    step = stepPhase(s, 'panDone');
    expect(step.changed).toBe(true);
    expect(step.startPan).toBeUndefined();
    expect(step.state.phase).toBe('B');
    s = step.state;

    step = stepPhase(s, 'barFilled');
    expect(step).toMatchObject({ startPan: 'A', changed: true });
    expect(step.state.phase).toBe('B_TO_A');
    s = step.state;

    step = stepPhase(s, 'panDone');
    expect(step.changed).toBe(true);
    expect(step.state.phase).toBe('A');
  });

  it('ignores depleted outside the A phase', () => {
    for (const phase of ['A_TO_B', 'B', 'B_TO_A'] as const) {
      const step = stepPhase(at(phase), 'depleted');
      expect(step.changed).toBe(false);
      expect(step.startPan).toBeUndefined();
      expect(step.state.phase).toBe(phase);
    }
  });

  it('ignores barFilled in A (refill in place, no pan) and while already panning up', () => {
    for (const phase of ['A', 'B_TO_A'] as const) {
      const step = stepPhase(at(phase), 'barFilled');
      expect(step.changed).toBe(false);
      expect(step.startPan).toBeUndefined();
      expect(step.state.phase).toBe(phase);
    }
  });

  it('queues a barFilled during the downward pan and turns straight around on landing', () => {
    const queued = stepPhase(at('A_TO_B'), 'barFilled');
    expect(queued.changed).toBe(false);
    expect(queued.state).toEqual({ phase: 'A_TO_B', refillQueued: true });

    const landed = stepPhase(queued.state, 'panDone');
    expect(landed).toMatchObject({ startPan: 'A', changed: true });
    expect(landed.state).toEqual({ phase: 'B_TO_A', refillQueued: false });
  });

  it('ignores panDone in the settled phases', () => {
    for (const phase of ['A', 'B'] as const) {
      const step = stepPhase(at(phase), 'panDone');
      expect(step.changed).toBe(false);
      expect(step.state.phase).toBe(phase);
    }
  });
});
