/**
 * Pure accumulator for the "board has settled" gate that arms the A→B phase pan.
 *
 * The buffer hits 0 the instant the last ball is RELEASED — but that ball is still
 * falling and may cascade merges, and the pan shouldn't yank the camera mid-action.
 * So the gate requires a contiguous hold of settled frames before firing, resets the
 * hold whenever motion resumes, and carries a hard timeout so a jittering board
 * (balls trembling just above the rest-speed threshold) can never wedge the flow.
 *
 * Phaser-free; ZoneASystem drives it once per update() with Board.isSettled().
 */

/** Contiguous settled time required before the gate fires (ms). */
export const SETTLE_HOLD_MS = 350;
/** Hard fallback: fire regardless of motion once this much time has passed (ms). */
export const SETTLE_TIMEOUT_MS = 4000;

export interface SettleGateState {
  /** Contiguous settled time so far; resets to 0 on motion. */
  holdMs: number;
  /** Total time since the gate was armed; never resets. */
  totalMs: number;
}

export function initialSettleGate(): SettleGateState {
  return { holdMs: 0, totalMs: 0 };
}

export interface SettleGateStep {
  state: SettleGateState;
  fire: boolean;
}

export function advanceSettleGate(
  state: SettleGateState,
  deltaMs: number,
  settledNow: boolean,
  holdMs: number = SETTLE_HOLD_MS,
  timeoutMs: number = SETTLE_TIMEOUT_MS,
): SettleGateStep {
  const next: SettleGateState = {
    holdMs: settledNow ? state.holdMs + deltaMs : 0,
    totalMs: state.totalMs + deltaMs,
  };
  return { state: next, fire: next.holdMs >= holdMs || next.totalMs >= timeoutMs };
}
