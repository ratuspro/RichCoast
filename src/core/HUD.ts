import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from './contracts';
import type { EventBus } from './EventBus';
import { compactValue, hexColor } from './Materials';
import { milestoneProgress } from './Progression';
import { Theme } from './Theme';
import { WIDTH } from './Layout';
import { OVERLAY_SCENE_KEY } from './OverlayScene';
import { launchOverlayFlyer, type OverlayFlyer } from './overlayFx';

/** Landing count-up + scale pop of the score total when a harvest arrives. */
const HARVEST_COUNTUP_MS = 420;

/** Header band geometry: a solid chrome bar across the top that the HUD text sits on. */
const BAND_H = 42;
const BAND_CY = BAND_H / 2; // 21 — vertical midline everything aligns to

/** Milestone progress bar (left slot, where the level counter used to be). */
const MILESTONE_BAR_X = 14;
const MILESTONE_BAR_W = 113; // 72 + 30%, then +20%
const MILESTONE_BAR_H = 20; // 8 + 150%

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
  private chromeBar?: Phaser.GameObjects.Rectangle;
  private baseRule?: Phaser.GameObjects.Graphics;
  private milestoneTrack?: Phaser.GameObjects.Rectangle;

  private scene?: Phaser.Scene;
  /** The number the HUD actually shows. Driven ONLY by harvest landings (not the live
   *  SCORE_CHANGED), so it stays frozen through a B round and jumps once the haul flies up. */
  private shownTotal = 0;
  /** In-flight harvest flyers, tracked so destroy() can discard them: their number tokens live
   *  on the overlay scene, which outlives a GameScene restart. */
  private readonly harvestFlyers = new Set<OverlayFlyer>();

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.shownTotal = 0;
    // Chrome bar: above gameplay (depth 0) but below every HUD element (queue row 20,
    // text 1000), so it gives the numbers a surface without occluding them.
    // Everything here is scrollFactor(0): the main camera scrolls between the two phase
    // framings (see core/phaseGeometry.ts), and the HUD must stay pinned to the screen top.
    this.chromeBar = scene.add
      .rectangle(WIDTH / 2, BAND_CY, WIDTH, BAND_H, Theme.cream, 1)
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(5);

    this.baseRule = scene.add.graphics().setDepth(6).setScrollFactor(0);

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
    this.milestoneTrack = scene.add
      .rectangle(MILESTONE_BAR_X, BAND_CY, MILESTONE_BAR_W, MILESTONE_BAR_H, Theme.pineShadow, 1)
      .setOrigin(0, 0.5)
      .setScrollFactor(0)
      .setDepth(1000);
    this.milestoneFill = scene.add
      .rectangle(MILESTONE_BAR_X + 1, BAND_CY, 0, MILESTONE_BAR_H - 2, Theme.brassBright, 1)
      .setOrigin(0, 0.5)
      .setScrollFactor(0)
      .setDepth(1001);

    this.restyle();

    // The shown total is driven by harvest landings, NOT the live SCORE_CHANGED — Zone B's
    // running total ticks up per drained ball, but the HUD holds frozen until the round's whole
    // haul flies up and lands, so the number jumps once, in sync with the pan back to Zone A.
    this.bus.on(GameEvent.ScoreHarvested, ({ amount, x, y }) => this.flyHarvest(amount, x, y));

    this.bus.on(GameEvent.ProgressionChanged, ({ level }) => {
      if (this.milestoneFill) {
        this.milestoneFill.width = (MILESTONE_BAR_W - 2) * milestoneProgress(level);
      }
    });

    this.bus.on(GameEvent.ThemeChanged, () => this.restyle());
  }

  /** (Re-)apply every Theme colour — run at create and per THEME_CHANGED tick. */
  private restyle(): void {
    this.chromeBar?.setFillStyle(Theme.cream, 1);
    // Bottom edge: a 2px wood base rule with a thin brass accent above it (the signature touch).
    this.baseRule
      ?.clear()
      .fillStyle(Theme.pineDark, 1)
      .fillRect(0, BAND_H - 2, WIDTH, 2)
      .fillStyle(Theme.brass, 0.8)
      .fillRect(0, BAND_H - 3, WIDTH, 1);
    this.scoreText?.setColor(hexColor(Theme.ink));
    this.milestoneTrack?.setFillStyle(Theme.pineShadow, 1).setStrokeStyle(1, Theme.ink, 1);
    this.milestoneFill?.setFillStyle(Theme.brassBright, 1);
  }

  update(_time: number, _delta: number): void {}

  /** Fly a brass number token from Zone B's haul spot up to the score total, mirroring the
   *  buffer-refill particles. On landing, the total counts up by `amount`. Falls back to an
   *  instant land if the overlay scene or score text isn't available. */
  private flyHarvest(amount: number, x: number, y: number): void {
    if (amount <= 0) return;
    const scene = this.scene;
    const text = this.scoreText;
    const overlay = scene?.scene.get(OVERLAY_SCENE_KEY);
    if (!scene || !text || !overlay) {
      this.landHarvest(amount);
      return;
    }

    const token = overlay.add
      .text(x, y, `+${compactValue(amount)}`, {
        fontFamily: 'monospace',
        fontSize: '20px',
        fontStyle: 'bold',
        color: hexColor(Theme.brassBright),
      })
      .setOrigin(0.5)
      .setStroke(hexColor(Theme.ink), 4)
      .setDepth(955);

    const flyer = launchOverlayFlyer({
      gameScene: scene,
      overlay,
      object: token,
      start: { x, y },
      end: { x: text.x, y: text.y },
      durationMs: 620,
      bowJitter: 40,
      scaleFrom: 1,
      scaleTo: 0.55,
      trailColor: Theme.brassBright,
      onArrive: () => {
        this.harvestFlyers.delete(flyer);
        this.landHarvest(amount);
      },
    });
    this.harvestFlyers.add(flyer);
  }

  /** The haul reached the top: count the shown total up to its new value with a scale pop. */
  private landHarvest(amount: number): void {
    const from = this.shownTotal;
    this.shownTotal += amount;
    const to = this.shownTotal;
    const text = this.scoreText;
    const scene = this.scene;
    if (!text || !scene) {
      text?.setText(compactValue(to));
      return;
    }
    const proxy = { v: from };
    scene.tweens.add({
      targets: proxy,
      v: to,
      duration: HARVEST_COUNTUP_MS,
      ease: 'Cubic.easeOut',
      onUpdate: () => text.setText(compactValue(Math.round(proxy.v))),
      onComplete: () => text.setText(compactValue(to)),
    });
    scene.tweens.add({
      targets: text,
      scale: 1.35,
      duration: 160,
      yoyo: true,
      ease: 'Sine.InOut',
    });
  }

  /** Discard any in-flight harvest tokens — they live on the overlay scene, which survives a
   *  GameScene restart, so they'd leak otherwise. */
  destroy(): void {
    for (const flyer of this.harvestFlyers) flyer.discard();
    this.harvestFlyers.clear();
  }
}
