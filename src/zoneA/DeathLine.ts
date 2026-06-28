import type Phaser from 'phaser';
import { WIDTH } from '../core/Layout';
import { DEATH_LINE_Y } from './tuning';

const LINE_COLOR = 0xff4d4d;
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

  constructor(private readonly scene: Phaser.Scene) {
    this.line = scene.add
      .rectangle(WIDTH / 2, DEATH_LINE_Y, WIDTH, LINE_THICKNESS, LINE_COLOR)
      .setOrigin(0.5)
      .setDepth(50)
      .setVisible(false);
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
