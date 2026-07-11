import Phaser from 'phaser';
import * as Layout from '../core/Layout';
import { clampSpawnX, radiusForTier } from './ballMath';
import type { ArenaView } from './ArenaView';
import { BallQueue } from './BallQueue';
import type { BallFactory } from './BallFactory';
import { DROP_COOLDOWN_MS } from './tuning';
import { Theme } from '../core/Theme';
import { hexColor } from '../core/Materials';

// One coherent top-right queue row: `N left  (o)`. The balls-left count sits left,
// clear of the centred score; the small next-ball preview is the far-right icon.
const ROW_Y = 21;
const PREVIEW_SIZE = 24;
const PREVIEW_X = Layout.WIDTH - 14 - PREVIEW_SIZE / 2; // rightmost, ball edge at the margin
const COUNT_X = Layout.WIDTH - 46; // right-aligned, just left of the preview ball
// Text colour comes from the live Theme: ink (stronger) for the live count —
// re-applied in restyle() on palette swaps.

/**
 * Drag-to-aim input for Zone A. The current ball is a body-less sprite that tracks
 * the pointer's X along the spawn row; releasing drops a real physics ball through
 * `onDrop`. Owns the BallQueue (current + next) and draws the next-ball preview.
 *
 * Physics only begin on release — until then the ball is purely visual, so it's also
 * invisible to Zone C's body query (correct: you can't suck a ball that isn't dropped).
 */
export class AimController {
  private readonly queue: BallQueue;
  private dragging = false;
  private disabled = false;
  private aimX = Layout.WIDTH / 2;
  private dropReadyAt = 0;

  private readonly aimImage: Phaser.GameObjects.Image;
  private readonly guide: Phaser.GameObjects.Graphics;
  private readonly previewImage: Phaser.GameObjects.Image;
  private readonly countText: Phaser.GameObjects.Text;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly factory: BallFactory,
    private readonly arena: ArenaView,
    private readonly onDrop: (x: number, tier: number) => void,
    queue?: BallQueue,
  ) {
    this.queue = queue ?? new BallQueue();
    this.aimImage = scene.add
      .image(this.aimX, arena.spawnY, factory.ensureTexture(this.queue.peek()))
      .setDepth(10);
    arena.claim(this.aimImage); // zooms with the arena via the dedicated camera

    // Drop guide: a vertical dashed line from the aim ball's centre down to the funnel
    // ramp beneath it, so the player can read where the ball will fall. On the arena
    // layer (depth below the ball) so it zooms and scrolls with the playfield.
    this.guide = scene.add.graphics().setDepth(9);
    arena.claim(this.guide);

    // The queue row is screen-space chrome pinned to the HUD bar: scrollFactor(0) keeps it
    // put while the main camera pans between the phase framings.
    this.previewImage = scene.add
      .image(PREVIEW_X, ROW_Y, factory.ensureTexture(this.queue.peekNext()))
      .setDisplaySize(PREVIEW_SIZE, PREVIEW_SIZE)
      .setScrollFactor(0)
      .setDepth(20);
    // Balls-left-to-drop count, on the same row. Hidden until the first value arrives.
    this.countText = scene.add
      .text(COUNT_X, ROW_Y, '', {
        fontFamily: 'monospace',
        fontSize: '14px',
        color: hexColor(Theme.ink),
      })
      .setOrigin(1, 0.5)
      .setScrollFactor(0)
      .setDepth(20)
      .setVisible(false);

    // The queue row is screen-space chrome — keep it out of the zoomed arena camera so it
    // doesn't render tiny inside the playfield once the arena grows.
    arena.ignoreOnArenaCamera([this.previewImage, this.countText]);

    scene.input.on(Phaser.Input.Events.POINTER_DOWN, this.onPointerDown);
    scene.input.on(Phaser.Input.Events.POINTER_MOVE, this.onPointerMove);
    scene.input.on(Phaser.Input.Events.POINTER_UP, this.onPointerUp);
    scene.input.on(Phaser.Input.Events.POINTER_UP_OUTSIDE, this.onPointerUp);

    this.moveAimTo(this.aimX);
  }

  /**
   * Update the balls-left-to-drop count in the queue row (revealed on first call). Pops the
   * number briefly on every change — called once per tick during a score-bar cash-in refill,
   * this is what makes the buffer visibly "arrive" one slot at a time instead of snapping.
   */
  setBallsLeft(count: number): void {
    this.countText.setText(`${count} left`).setVisible(true);
    this.scene.tweens.killTweensOf(this.countText);
    this.countText.setScale(1.35);
    this.scene.tweens.add({
      targets: this.countText,
      scale: 1,
      duration: 160,
      ease: 'Back.easeOut',
    });
  }

  /** Screen-space centre of the balls-left count — the landing point for the score-bar
   *  cash-in particles (ZoneASystem flies one brass mote here per refilled slot). */
  countAnchor(): { x: number; y: number } {
    return { x: this.countText.x - this.countText.displayWidth / 2, y: this.countText.y };
  }

  /** Freeze input and hide the aim ball (called on game over). */
  disable(): void {
    this.disabled = true;
    this.dragging = false;
    this.aimImage.setVisible(false);
    this.guide.setVisible(false);
  }

  /**
   * Reversible freeze for the milestone zoom-out: block aiming AND dropping and hide the aim
   * ball, then restore it — unlike `disable()`, which is the permanent game-over lock.
   */
  setFrozen(on: boolean): void {
    this.frozen = on;
    this.dragging = false;
    this.aimImage.setVisible(!on);
    this.guide.setVisible(!on);
  }

  /** Re-clamp the aim ball to the (possibly grown) arena and re-seat it on the new spawn row. */
  syncToArena(): void {
    this.moveAimTo(this.aimX);
  }

  /** Re-apply the active Theme's text colours + guide tint (milestone palette swap). */
  restyle(): void {
    this.countText.setColor(hexColor(Theme.ink));
    this.drawGuide(); // guide colour is baked per redraw — refresh it in place
  }

  /**
   * Re-read the queue's current + next tiers into the aim ball and preview. Called after a
   * milestone re-rolls the queue (the in-hand/preview balls may have been blacklisted), so the
   * row shows valid tiers when input returns. Does not pop — only refreshes visuals.
   */
  refreshQueue(): void {
    this.syncQueueVisuals();
  }

  /**
   * Soft-lock: block drops without hiding the aim ball or disabling aiming.
   * Used when the ball buffer is empty but Zone B may still save the run.
   */
  setDropLocked(locked: boolean): void {
    this.dropLocked = locked;
  }

  private dropLocked = false;
  private frozen = false;

  destroy(): void {
    this.scene.tweens.killTweensOf(this.countText);
    this.scene.input.off(Phaser.Input.Events.POINTER_DOWN, this.onPointerDown);
    this.scene.input.off(Phaser.Input.Events.POINTER_MOVE, this.onPointerMove);
    this.scene.input.off(Phaser.Input.Events.POINTER_UP, this.onPointerUp);
    this.scene.input.off(Phaser.Input.Events.POINTER_UP_OUTSIDE, this.onPointerUp);
    this.aimImage.destroy();
    this.guide.destroy();
    this.previewImage.destroy();
    this.countText.destroy();
  }

  private onPointerDown = (pointer: Phaser.Input.Pointer): void => {
    if (this.disabled || this.frozen) return;
    // No y-guard: aiming works from anywhere on screen — the phase freeze (`frozen`)
    // already blocks this handler outside the A phase, when taps belong to Zone C's door.
    if (this.scene.time.now < this.dropReadyAt) return; // brief post-drop cooldown
    this.dragging = true;
    this.moveAimTo(this.aimXFromPointer(pointer));
  };

  private onPointerMove = (pointer: Phaser.Input.Pointer): void => {
    if (!this.dragging) return;
    this.moveAimTo(this.aimXFromPointer(pointer));
  };

  private onPointerUp = (): void => {
    if (!this.dragging) return;
    this.dragging = false;
    if (this.dropLocked) return; // buffer empty — aim is still live, drop is not
    this.onDrop(this.aimX, this.queue.peek());
    this.advanceQueue();
    this.dropReadyAt = this.scene.time.now + DROP_COOLDOWN_MS;
  };

  /** Map a screen pointer to an arena-world x — correct under the zoomed/scrolled camera. */
  private aimXFromPointer(pointer: Phaser.Input.Pointer): number {
    return this.arena.worldPoint(pointer.x, pointer.y).x;
  }

  private moveAimTo(x: number): void {
    this.aimX = clampSpawnX(x, radiusForTier(this.queue.peek()), this.arena.minX, this.arena.maxX);
    this.aimImage.setPosition(this.aimX, this.arena.spawnY);
    this.drawGuide();
  }

  /** Redraw the dashed drop guide from the aim ball's centre down to the ramp under it. */
  private drawGuide(): void {
    const s = this.arena.scale;
    const g = this.guide.clear();
    g.lineStyle(3 * s, Theme.inkSoft, 0.55);
    const top = this.arena.spawnY;
    const bottom = this.arena.rampYAt(this.aimX);
    const dash = 14.4 * s;
    const gap = 12 * s;
    for (let y = top; y < bottom; y += dash + gap) {
      g.lineBetween(this.aimX, y, this.aimX, Math.min(y + dash, bottom));
    }
  }

  private advanceQueue(): void {
    this.queue.pop();
    this.syncQueueVisuals();
  }

  /** Point the aim ball + preview at the queue's current/next tiers and re-clamp for the radius. */
  private syncQueueVisuals(): void {
    const currentTier = this.queue.peek();
    const diameter = radiusForTier(currentTier) * 2;
    this.aimImage
      .setTexture(this.factory.ensureTexture(currentTier))
      .setDisplaySize(diameter, diameter);
    this.previewImage
      .setTexture(this.factory.ensureTexture(this.queue.peekNext()))
      .setDisplaySize(PREVIEW_SIZE, PREVIEW_SIZE);
    this.moveAimTo(this.aimX); // re-clamp for the current tier's radius
  }
}
