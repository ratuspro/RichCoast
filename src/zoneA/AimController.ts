import Phaser from 'phaser';
import * as Layout from '../core/Layout';
import { clampSpawnX, radiusForTier } from './ballMath';
import { BallQueue } from './BallQueue';
import type { BallFactory } from './BallFactory';
import { DROP_COOLDOWN_MS, SPAWN_Y } from './tuning';

// One coherent top-right queue row: `NEXT (o)  N left`. The preview ball is the icon;
// the count sits on the far right, clear of the centred score. Shared type treatment.
const ROW_Y = 30;
const LABEL_X = Layout.WIDTH - 152;
const PREVIEW_X = Layout.WIDTH - 100;
const PREVIEW_SIZE = 36;
const COUNT_X = Layout.WIDTH - 14;
const LABEL_COLOR = '#7c8aa6'; // muted slate for the "NEXT" / unit text
const COUNT_COLOR = '#e6ebf5'; // brighter for the live number

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
  private readonly previewImage: Phaser.GameObjects.Image;
  private readonly previewLabel: Phaser.GameObjects.Text;
  private readonly countText: Phaser.GameObjects.Text;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly factory: BallFactory,
    private readonly onDrop: (x: number, tier: number) => void,
    queue?: BallQueue,
  ) {
    this.queue = queue ?? new BallQueue();
    this.aimImage = scene.add
      .image(this.aimX, SPAWN_Y, factory.ensureTexture(this.queue.peek()))
      .setDepth(10);

    this.previewLabel = scene.add
      .text(LABEL_X, ROW_Y, 'NEXT', {
        fontFamily: 'monospace',
        fontSize: '12px',
        color: LABEL_COLOR,
      })
      .setOrigin(0, 0.5)
      .setDepth(20);
    this.previewImage = scene.add
      .image(PREVIEW_X, ROW_Y, factory.ensureTexture(this.queue.peekNext()))
      .setDisplaySize(PREVIEW_SIZE, PREVIEW_SIZE)
      .setDepth(20);
    // Balls-left-to-drop count, on the same row. Hidden until the first value arrives.
    this.countText = scene.add
      .text(COUNT_X, ROW_Y, '', {
        fontFamily: 'monospace',
        fontSize: '14px',
        color: COUNT_COLOR,
      })
      .setOrigin(1, 0.5)
      .setDepth(20)
      .setVisible(false);

    scene.input.on(Phaser.Input.Events.POINTER_DOWN, this.onPointerDown);
    scene.input.on(Phaser.Input.Events.POINTER_MOVE, this.onPointerMove);
    scene.input.on(Phaser.Input.Events.POINTER_UP, this.onPointerUp);
    scene.input.on(Phaser.Input.Events.POINTER_UP_OUTSIDE, this.onPointerUp);

    this.moveAimTo(this.aimX);
  }

  /** Update the balls-left-to-drop count in the queue row (revealed on first call). */
  setBallsLeft(count: number): void {
    this.countText.setText(`${count} left`).setVisible(true);
  }

  /** Freeze input and hide the aim ball (called on game over). */
  disable(): void {
    this.disabled = true;
    this.dragging = false;
    this.aimImage.setVisible(false);
  }

  /**
   * Soft-lock: block drops without hiding the aim ball or disabling aiming.
   * Used when the ball buffer is empty but Zone B may still save the run.
   */
  setDropLocked(locked: boolean): void {
    this.dropLocked = locked;
  }

  private dropLocked = false;

  destroy(): void {
    this.scene.input.off(Phaser.Input.Events.POINTER_DOWN, this.onPointerDown);
    this.scene.input.off(Phaser.Input.Events.POINTER_MOVE, this.onPointerMove);
    this.scene.input.off(Phaser.Input.Events.POINTER_UP, this.onPointerUp);
    this.scene.input.off(Phaser.Input.Events.POINTER_UP_OUTSIDE, this.onPointerUp);
    this.aimImage.destroy();
    this.previewImage.destroy();
    this.previewLabel.destroy();
    this.countText.destroy();
  }

  private onPointerDown = (pointer: Phaser.Input.Pointer): void => {
    if (this.disabled) return;
    if (pointer.y > Layout.zoneA.height) return; // a tap on Zone C's door / Zone B, not us
    if (this.scene.time.now < this.dropReadyAt) return; // brief post-drop cooldown
    this.dragging = true;
    this.moveAimTo(pointer.x);
  };

  private onPointerMove = (pointer: Phaser.Input.Pointer): void => {
    if (!this.dragging) return;
    this.moveAimTo(pointer.x);
  };

  private onPointerUp = (): void => {
    if (!this.dragging) return;
    this.dragging = false;
    if (this.dropLocked) return; // buffer empty — aim is still live, drop is not
    this.onDrop(this.aimX, this.queue.peek());
    this.advanceQueue();
    this.dropReadyAt = this.scene.time.now + DROP_COOLDOWN_MS;
  };

  private moveAimTo(x: number): void {
    this.aimX = clampSpawnX(x, radiusForTier(this.queue.peek()), 0, Layout.WIDTH);
    this.aimImage.setX(this.aimX);
  }

  private advanceQueue(): void {
    this.queue.pop();
    const nextTier = this.queue.peek();
    const nextDiameter = radiusForTier(nextTier) * 2;
    this.aimImage
      .setTexture(this.factory.ensureTexture(nextTier))
      .setDisplaySize(nextDiameter, nextDiameter);
    this.previewImage
      .setTexture(this.factory.ensureTexture(this.queue.peekNext()))
      .setDisplaySize(PREVIEW_SIZE, PREVIEW_SIZE);
    this.moveAimTo(this.aimX); // re-clamp for the new tier's radius
  }
}
