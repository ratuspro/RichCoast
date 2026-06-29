import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { WIDTH } from './Layout';

/** Header band geometry: a solid chrome bar across the top that the HUD text sits on. */
const BAND_H = 42;
const BAND_CY = BAND_H / 2; // 21 — vertical midline everything aligns to

/**
 * Top-of-screen HUD. Draws a solid chrome header bar across the top, on which the
 * cumulative score (center, hero) and the current level (left) sit — the level advances
 * each time the Zone B progress bar fills. The ball-buffer count and next-ball preview
 * live in Zone A's queue row, which renders on top of the bar via depth. Pure consumer
 * of bus events — never computes anything itself.
 */
export class HUD implements GameSystem {
  private scoreText?: Phaser.GameObjects.Text;
  private levelText?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    // Chrome bar: above gameplay (depth 0) but below every HUD element (queue row 20,
    // text 1000), so it gives the numbers a surface without occluding them.
    scene.add
      .rectangle(WIDTH / 2, BAND_CY, WIDTH, BAND_H, 0x141a26, 1)
      .setOrigin(0.5)
      .setDepth(5);

    // Bottom edge: a 2px base rule with a thin cyan accent above it (the signature touch).
    scene.add
      .graphics()
      .setDepth(6)
      .fillStyle(0x2a3346, 1)
      .fillRect(0, BAND_H - 2, WIDTH, 2)
      .fillStyle(0x4cc9f0, 0.6)
      .fillRect(0, BAND_H - 3, WIDTH, 1);

    this.scoreText = scene.add
      .text(WIDTH / 2, BAND_CY, '0', {
        fontFamily: 'monospace',
        fontSize: '28px',
        color: '#ffffff',
      })
      .setOrigin(0.5, 0.5)
      .setDepth(1000);

    this.levelText = scene.add
      .text(14, BAND_CY, 'Lvl 1', {
        fontFamily: 'monospace',
        fontSize: '18px',
        color: '#e6ebf5',
      })
      .setOrigin(0, 0.5)
      .setDepth(1000);

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => {
      this.scoreText?.setText(String(total));
    });

    this.bus.on(GameEvent.ProgressionChanged, ({ level }) => {
      this.levelText?.setText(`Lvl ${level}`);
    });
  }

  update(_time: number, _delta: number): void {}
}
