import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { WIDTH } from './Layout';

/**
 * Top-of-screen HUD. Renders the cumulative score. The ball-buffer count lives in
 * Zone A's queue row (next ball + balls-left), so the HUD stays score-only.
 * Pure consumer of bus events — never computes anything itself.
 */
export class HUD implements GameSystem {
  private scoreText?: Phaser.GameObjects.Text;

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

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => {
      this.scoreText?.setText(String(total));
    });
  }

  update(_time: number, _delta: number): void {}
}
