import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { compactValue, hexColor } from './Materials';
import { milestoneProgress } from './Progression';
import { Theme } from './Theme';
import { WIDTH } from './Layout';

/** Header band geometry: a solid chrome bar across the top that the HUD text sits on. */
const BAND_H = 42;
const BAND_CY = BAND_H / 2; // 21 — vertical midline everything aligns to

/** Milestone progress bar (left slot, where the level counter used to be). */
const MILESTONE_BAR_X = 14;
const MILESTONE_BAR_W = 72;
const MILESTONE_BAR_H = 8;

/**
 * Top-of-screen HUD. Draws a solid chrome header bar across the top, on which the
 * cumulative score (center, hero, compact-formatted) and a numberless milestone
 * progress bar (left) sit — the bar fills as levels advance toward the next arena
 * milestone and resets when one lands. The ball-buffer count and next-ball preview
 * live in Zone A's queue row, which renders on top of the bar via depth. Pure consumer
 * of bus events — never computes anything itself beyond display formatting.
 */
export class HUD implements GameSystem {
  private scoreText?: Phaser.GameObjects.Text;
  private milestoneFill?: Phaser.GameObjects.Rectangle;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    // Chrome bar: above gameplay (depth 0) but below every HUD element (queue row 20,
    // text 1000), so it gives the numbers a surface without occluding them.
    // Everything here is scrollFactor(0): the main camera scrolls between the two phase
    // framings (see core/phaseGeometry.ts), and the HUD must stay pinned to the screen top.
    scene.add
      .rectangle(WIDTH / 2, BAND_CY, WIDTH, BAND_H, Theme.cream, 1)
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(5);

    // Bottom edge: a 2px wood base rule with a thin brass accent above it (the signature touch).
    scene.add
      .graphics()
      .setDepth(6)
      .setScrollFactor(0)
      .fillStyle(Theme.pineDark, 1)
      .fillRect(0, BAND_H - 2, WIDTH, 2)
      .fillStyle(Theme.brass, 0.8)
      .fillRect(0, BAND_H - 3, WIDTH, 1);

    this.scoreText = scene.add
      .text(WIDTH / 2, BAND_CY, '0', {
        fontFamily: 'monospace',
        fontSize: '28px',
        color: hexColor(Theme.ink),
      })
      .setOrigin(0.5, 0.5)
      .setScrollFactor(0)
      .setDepth(1000);

    // Milestone bar: pine track with a brass fill, no numbers — progress toward the
    // next arena zoom-out milestone. Track drawn once; only the fill width changes.
    scene.add
      .rectangle(MILESTONE_BAR_X, BAND_CY, MILESTONE_BAR_W, MILESTONE_BAR_H, Theme.pineDark, 1)
      .setOrigin(0, 0.5)
      .setScrollFactor(0)
      .setDepth(1000);
    this.milestoneFill = scene.add
      .rectangle(MILESTONE_BAR_X + 1, BAND_CY, 0, MILESTONE_BAR_H - 2, Theme.brass, 1)
      .setOrigin(0, 0.5)
      .setScrollFactor(0)
      .setDepth(1001);

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => {
      this.scoreText?.setText(compactValue(total));
    });

    this.bus.on(GameEvent.ProgressionChanged, ({ level }) => {
      if (this.milestoneFill) {
        this.milestoneFill.width = (MILESTONE_BAR_W - 2) * milestoneProgress(level);
      }
    });
  }

  update(_time: number, _delta: number): void {}
}
