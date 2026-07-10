import type Phaser from 'phaser';
import { type ComboState, newComboState, nextCombo } from './comboPitch';

/**
 * Procedural sound engine — the game's only audio. Soft synth bells/marimba,
 * synthesised live with the Web Audio API (no asset files, matching the
 * procedural-textures rule). A singleton, mirroring the one shared scene/bus:
 * each zone calls into it at its own local hook point.
 *
 * Disabled-until-init and self-silencing: every cue is a no-op until `init`
 * finds a Web Audio context, so importing it never makes noise and HTML5/NoAudio
 * fallbacks degrade cleanly.
 */

/** Overall output level — one knob for the whole mix (0–1). */
const MASTER_GAIN = 0.6;
/** Merges/multiplications closer together than this (ms) climb in pitch. */
const COMBO_WINDOW_MS = 1500;

interface ToneOpts {
  freq: number;
  type?: OscillatorType;
  /** Fade-in, seconds. */
  attack?: number;
  /** Fade-out after the attack, seconds. */
  decay?: number;
  /** Peak gain before the master bus. */
  gain?: number;
  /** If set, glide the pitch to this frequency over the tone's life. */
  glideTo?: number;
  /** Start offset from now, seconds (for arpeggios). */
  delay?: number;
}

class SfxEngine {
  private ctx?: AudioContext;
  private master?: GainNode;
  private muted = false;

  // Independent chains: a merge streak and a multiply streak escalate separately.
  private readonly mergeCombo: ComboState = newComboState();
  private readonly multiplyCombo: ComboState = newComboState();

  /**
   * Reuse Phaser's AudioContext so its mobile autoplay-unlock gesture covers us.
   * Safe to call repeatedly; only the first wins.
   */
  init(scene: Phaser.Scene): void {
    if (this.ctx) return;
    const ctx = (scene.sound as unknown as { context?: AudioContext }).context;
    if (!ctx) return; // HTML5 / NoAudio manager — stay a no-op
    this.ctx = ctx;
    this.master = ctx.createGain();
    this.master.gain.value = MASTER_GAIN;
    this.master.connect(ctx.destination);
  }

  setMuted(muted: boolean): void {
    this.muted = muted;
    if (this.master) this.master.gain.value = muted ? 0 : MASTER_GAIN;
  }

  toggleMute(): void {
    this.setMuted(!this.muted);
  }

  // --- Cues, tuned by relevance (rarer/more important = louder) -------------

  /** Zone A: a ball is released. Soft low thunk. */
  drop(): void {
    this.tone({ freq: 180, type: 'triangle', attack: 0.004, decay: 0.12, gain: 0.18, glideTo: 120 });
  }

  /** Zone A: two balls merge. Bell ping that climbs through a fast chain. */
  merge(): void {
    const mult = this.combo(this.mergeCombo);
    if (mult === undefined) return;
    const base = 523.25; // C5
    this.tone({ freq: base * mult, type: 'sine', attack: 0.004, decay: 0.22, gain: 0.26 });
    this.tone({ freq: base * mult * 2, type: 'sine', attack: 0.004, decay: 0.12, gain: 0.08 });
  }

  /** Zone C: a ball is sucked through the trap-door. Short descending whoosh. */
  transition(): void {
    this.tone({ freq: 520, type: 'triangle', attack: 0.004, decay: 0.2, gain: 0.22, glideTo: 180 });
  }

  /** Zone B: a ball splits into `n` copies. Bright pluck, same chain rule as merge. */
  multiply(n: number): void {
    if (n <= 1) return;
    const mult = this.combo(this.multiplyCombo);
    if (mult === undefined) return;
    const base = 392.0; // G4
    this.tone({ freq: base * mult, type: 'triangle', attack: 0.003, decay: 0.16, gain: 0.2 });
  }

  /** Zone B: a ball drains. Quiet coin tick — happens often, must not dominate. */
  collect(value: number): void {
    const semis = Math.min(12, Math.log2(Math.max(1, value))); // tint up with value
    this.tone({ freq: 880 * 2 ** (semis / 12), type: 'sine', attack: 0.002, decay: 0.07, gain: 0.09 });
  }

  /** Zone A: one ball buffer slot arrives during a score-bar cash-in refill. Ascending
   *  blip, `index`-th in the refill sequence (0-based) — climbs a semitone per step like
   *  the merge/multiply combo chains, but driven directly by the caller's own counter since
   *  buffer ticks already run on a fixed cadence rather than player-timed hits. */
  bufferTick(index: number): void {
    const base = 784.0; // G5
    const mult = 2 ** (Math.min(index, 8) / 12);
    this.tone({ freq: base * mult, type: 'sine', attack: 0.003, decay: 0.1, gain: 0.16 });
  }

  /** Score bar filled. Rising major-triad arpeggio — the loudest, rarest cue. */
  goal(): void {
    const notes = [659.25, 830.61, 987.77]; // E5, G#5, B5
    notes.forEach((freq, i) => {
      this.tone({ freq, type: 'sine', attack: 0.005, decay: 0.28, gain: 0.5, delay: i * 0.09 });
    });
  }

  // --- Internals -----------------------------------------------------------

  /** Advance a combo chain; undefined when audio is off (skip the cue). */
  private combo(state: ComboState): number | undefined {
    if (!this.ctx) return undefined;
    return nextCombo(state, this.ctx.currentTime * 1000, COMBO_WINDOW_MS).mult;
  }

  /** One short voice: osc → gain (exp attack/decay) → master, auto-stopped. */
  private tone(o: ToneOpts): void {
    const ctx = this.ctx;
    const master = this.master;
    if (!ctx || !master || this.muted) return;

    const start = ctx.currentTime + (o.delay ?? 0);
    const attack = o.attack ?? 0.005;
    const decay = o.decay ?? 0.18;
    const peak = o.gain ?? 0.2;
    const end = start + attack + decay;

    const osc = ctx.createOscillator();
    osc.type = o.type ?? 'sine';
    osc.frequency.setValueAtTime(o.freq, start);
    if (o.glideTo !== undefined) {
      osc.frequency.exponentialRampToValueAtTime(Math.max(1, o.glideTo), end);
    }

    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, start); // exp ramps need a positive start
    g.gain.exponentialRampToValueAtTime(peak, start + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, end);

    osc.connect(g).connect(master);
    osc.start(start);
    osc.stop(end + 0.02);
  }
}

/** The shared procedural sound engine. */
export const Sfx = new SfxEngine();
