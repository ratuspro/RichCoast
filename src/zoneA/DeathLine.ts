import type Phaser from 'phaser';
import { Theme } from '../core/Theme';
import type { ArenaView } from './ArenaView';

const LINE_THICKNESS = 2;
const PULSE_MIN_ALPHA = 0.25;
const PULSE_MAX_ALPHA = 0.9;
const PULSE_MS = 500;

/**
 * The red overflow threshold line across Zone A at `DEATH_LINE_Y`.
 *
 * Hidden by default — it only surfaces as a warning when a ball is resting close
 * to the line (driven by Board's danger detection). `setDanger` is idempotent so
 * the pulse tween isn't restarted on every frame the danger persists.
 */
export class DeathLine {
  private readonly line: Phaser.GameObjects.Rectangle;
  private pulse?: Phaser.Tweens.Tween;
  private danger = false;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly arena: ArenaView,
  ) {
    this.line = scene.add
      .rectangle(0, 0, 1, LINE_THICKNESS, Theme.danger)
      .setOrigin(0.5)
      .setDepth(50)
      .setVisible(false);
    arena.claim(this.line); // zooms with the arena via the dedicated camera
    this.reposition();
  }

  /** Re-apply the active Theme's danger colour (milestone palette swap). */
  restyle(): void {
    this.line.setFillStyle(Theme.danger);
  }

  /** Move/resize the line to span the (possibly grown) arena at its scaled death-line y. */
  reposition(): void {
    const w = this.arena.maxX - this.arena.minX;
    this.line.setPosition((this.arena.minX + this.arena.maxX) / 2, this.arena.deathLineY);
    this.line.setSize(w, LINE_THICKNESS * this.arena.scale);
  }

  /** Show the pulsing red line (true) or hide it (false). No-op if unchanged. */
  setDanger(on: boolean): void {
    if (on === this.danger) return;
    this.danger = on;
    if (on) {
      this.line.setVisible(true).setAlpha(PULSE_MAX_ALPHA);
      this.pulse = this.scene.tweens.add({
        targets: this.line,
        alpha: { from: PULSE_MAX_ALPHA, to: PULSE_MIN_ALPHA },
        duration: PULSE_MS,
        yoyo: true,
        repeat: -1,
      });
    } else {
      this.pulse?.remove();
      this.pulse = undefined;
      this.line.setVisible(false);
    }
  }

  destroy(): void {
    this.pulse?.remove();
    this.line.destroy();
  }
}
