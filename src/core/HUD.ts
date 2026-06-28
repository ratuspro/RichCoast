import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { WIDTH } from './Layout';

/**
 * Top-of-screen HUD. Renders cumulative score and (Phase 3) ball buffer count.
 * Pure consumer of bus events — never computes anything itself.
 */
export class HUD implements GameSystem {
  private scoreText?: Phaser.GameObjects.Text;
  private bufferText?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scoreText = scene.add
      .text(WIDTH / 2, 16, '0', {
        fontFamily: 'monospace',
        fontSize: '28px',
        color: '#ffffff',
      })
      .setOrigin(0.5, 0)
      .setDepth(1000);

    // Phase 3: small ball-buffer count, top-right corner.
    this.bufferText = scene.add
      .text(WIDTH - 12, 16, '', {
        fontFamily: 'monospace',
        fontSize: '16px',
        color: '#88ccff',
      })
      .setOrigin(1, 0)
      .setDepth(1000)
      .setVisible(false);

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => {
      this.scoreText?.setText(String(total));
    });

    this.bus.on(GameEvent.BallBufferChanged, ({ count }) => {
      this.bufferText?.setText(`×${count}`).setVisible(true);
    });
  }

  update(_time: number, _delta: number): void {}
}
