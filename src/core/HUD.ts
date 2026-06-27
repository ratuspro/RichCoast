import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { WIDTH } from './Layout';

/**
 * Top-of-screen score readout. Part of the shell (Dev 1).
 *
 * Pure consumer of `SCORE_CHANGED` — it never computes score (Zone B owns that),
 * it just renders whatever total the bus reports.
 */
export class HUD implements GameSystem {
  private text?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.text = scene.add
      .text(WIDTH / 2, 16, '0', {
        fontFamily: 'monospace',
        fontSize: '28px',
        color: '#ffffff',
      })
      .setOrigin(0.5, 0)
      .setDepth(1000); // always above the zones

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => {
      this.text?.setText(String(total));
    });
  }

  update(_time: number, _delta: number): void {
    // Score is event-driven; nothing to do per frame.
  }
}
