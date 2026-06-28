/**
 * Combo pitch-rise: the pure timing rule shared by Zone A merges and Zone B
 * multiplications. Triggering a channel again within `windowMs` of the previous
 * hit continues a chain and steps the pitch up; a longer gap resets it.
 *
 * Kept Phaser/Web-Audio-free so it runs under Vitest — the audio engine just
 * feeds it `ctx.currentTime` and applies the returned multiplier to a frequency.
 */

export interface ComboState {
  /** Time (ms) of the last trigger, or -Infinity until the first one. */
  lastAt: number;
  /** Current step in the chain (0 = base pitch). */
  step: number;
}

export function newComboState(): ComboState {
  return { lastAt: -Infinity, step: 0 };
}

export interface ComboResult {
  step: number;
  /** Frequency multiplier: 2^(step/12) — one equal-tempered semitone per step. */
  mult: number;
}

/**
 * Advance a combo channel. Mutates `state`: increments the step if `now` is
 * within `windowMs` of the previous trigger, else resets to 0. The step is
 * capped at `maxStep` so a long chain never gets shrill.
 */
export function nextCombo(
  state: ComboState,
  now: number,
  windowMs: number,
  maxStep = 8,
): ComboResult {
  const within = now - state.lastAt < windowMs;
  state.step = within ? Math.min(state.step + 1, maxStep) : 0;
  state.lastAt = now;
  return { step: state.step, mult: 2 ** (state.step / 12) };
}
