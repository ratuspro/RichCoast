/**
 * Pure state machine for the two-phase flow — Phaser-free so it unit-tests in plain Node.
 *
 * PhaseDirector feeds it three inputs and acts on the returned commands:
 *  - 'depleted'  (Zone A's buffer hit 0 and the board settled)  → A begins panning down.
 *  - 'barFilled' (Zone B's score bar cashed in)                 → B begins panning up.
 *  - 'panDone'   (the camera tween landed)                      → arrive at the target phase.
 *
 * Quirks encoded here (and covered by tests):
 *  - 'barFilled' in the A phase is IGNORED as a pan trigger: a cash-in can happen while
 *    already in A (e.g. milestone-drain balls fill the bar) — Zone A just refills in place.
 *  - 'barFilled' during A_TO_B can't turn the pan around mid-tween; it queues, and the
 *    machine bounces straight back (B → B_TO_A) the moment the downward pan lands.
 */
import type { GamePhase } from './contracts';

export interface PhaseState {
  phase: GamePhase;
  /** A cash-in arrived while panning down; bounce back up as soon as we land in B. */
  refillQueued: boolean;
}

export interface PhaseStep {
  state: PhaseState;
  /** Start the camera pan toward this phase (undefined = no new pan). */
  startPan?: 'B' | 'A';
  /** The phase changed — broadcast PHASE_CHANGED with the new state.phase. */
  changed: boolean;
}

export type PhaseInput = 'depleted' | 'barFilled' | 'panDone';

export function initialPhaseState(): PhaseState {
  return { phase: 'A', refillQueued: false };
}

export function stepPhase(state: PhaseState, input: PhaseInput): PhaseStep {
  const { phase, refillQueued } = state;

  switch (input) {
    case 'depleted':
      if (phase !== 'A') return { state, changed: false };
      return { state: { phase: 'A_TO_B', refillQueued }, startPan: 'B', changed: true };

    case 'barFilled':
      if (phase === 'B') {
        return { state: { phase: 'B_TO_A', refillQueued: false }, startPan: 'A', changed: true };
      }
      if (phase === 'A_TO_B') {
        return { state: { phase, refillQueued: true }, changed: false };
      }
      return { state, changed: false }; // in A (refill in place) or already panning up

    case 'panDone':
      if (phase === 'A_TO_B') {
        if (refillQueued) {
          // Landed in B with a refill already banked — turn straight around. B is never
          // observable here; the pan up starts the same tick.
          return { state: { phase: 'B_TO_A', refillQueued: false }, startPan: 'A', changed: true };
        }
        return { state: { phase: 'B', refillQueued }, changed: true };
      }
      if (phase === 'B_TO_A') {
        return { state: { phase: 'A', refillQueued }, changed: true };
      }
      return { state, changed: false };
  }
}
