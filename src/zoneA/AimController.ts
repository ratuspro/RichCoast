import Phaser from 'phaser';
import * as Layout from '../core/Layout';
import { clampSpawnX, radiusForTier } from './ballMath';
import { BallQueue } from './BallQueue';
import type { BallFactory } from './BallFactory';
import { DROP_COOLDOWN_MS, SPAWN_Y } from './tuning';

const PREVIEW_X = Layout.WIDTH - 34;
const PREVIEW_Y = 40;
const PREVIEW_SIZE = 44;

/**
 * Drag-to-aim input for Zone A. The current ball is a body-less sprite that tracks
 * the pointer's X along the spawn row; releasing drops a real physics ball through
 * `onDrop`. Owns the BallQueue (current + next) and draws the next-ball preview.
 *
 * Physics only begin on release — until then the ball is purely visual, so it's also
 * invisible to Zone C's body query (correct: you can't suck a ball that isn't dropped).
 */
export class AimController {
  private readonly queue = new BallQueue();
  private dragging = false;
  private disabled = false;
  private aimX = Layout.WIDTH / 2;
  private dropReadyAt = 0;

  private readonly aimImage: Phaser.GameObjects.Image;
  private readonly previewImage: Phaser.GameObjects.Image;
  private readonly previewLabel: Phaser.GameObjects.Text;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly factory: BallFactory,
    private readonly onDrop: (x: number, tier: number) => void,
  ) {
    this.aimImage = scene.add
      .image(this.aimX, SPAWN_Y, factory.ensureTexture(this.queue.peek()))
      .setDepth(10);

    this.previewLabel = scene.add
      .text(PREVIEW_X, PREVIEW_Y - PREVIEW_SIZE / 2 - 12, 'NEXT', {
        fontFamily: 'monospace',
        fontSize: '11px',
        color: '#566080',
      })
      .setOrigin(0.5)
      .setDepth(20);
    this.previewImage = scene.add
      .image(PREVIEW_X, PREVIEW_Y, factory.ensureTexture(this.queue.peekNext()))
      .setDisplaySize(PREVIEW_SIZE, PREVIEW_SIZE)
      .setDepth(20);

    scene.input.on(Phaser.Input.Events.POINTER_DOWN, this.onPointerDown);
    scene.input.on(Phaser.Input.Events.POINTER_MOVE, this.onPointerMove);
    scene.input.on(Phaser.Input.Events.POINTER_UP, this.onPointerUp);
    scene.input.on(Phaser.Input.Events.POINTER_UP_OUTSIDE, this.onPointerUp);

    this.moveAimTo(this.aimX);
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
