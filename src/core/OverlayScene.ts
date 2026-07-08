import Phaser from 'phaser';

/** Scene key for the always-on, top-most UI overlay. */
export const OVERLAY_SCENE_KEY = 'OverlayScene';

/**
 * A paper-thin scene stacked ON TOP of GameScene, holding only cross-zone screen-space
 * effects — currently the score-bar cash-in particles that fly from Zone B up into Zone A's
 * queue-row count.
 *
 * Why a separate scene rather than a camera: those particles span the whole screen (Zone B's
 * bottom → through Zone A → the HUD), and Zone A is drawn by its own camera that paints an
 * opaque band over the main camera. A same-scene overlay camera would (a) re-draw everything
 * it isn't explicitly told to ignore and (b) offset screen-space objects by its viewport top.
 * A dedicated scene sidesteps both: its display list contains ONLY the particles, its camera
 * is full-screen at scroll 0 (so coordinates are 1:1 with the design screen, matching the
 * GameScene chrome the particles target), and it renders last, above every GameScene camera.
 * It never pans, so the particles are immune to GameScene's phase pans too.
 *
 * It owns no game logic and no input — GameScene keeps all of that. Callers add objects with
 * `scene.add.*` and drive them with `scene.tweens` exactly as they would in GameScene.
 */
export class OverlayScene extends Phaser.Scene {
  constructor() {
    super({ key: OVERLAY_SCENE_KEY, active: true });
  }

  create(): void {
    // Transparent so GameScene shows through, and inert to input so pointer events fall
    // straight through to GameScene (aim + trap-door tap).
    this.cameras.main.transparent = true;
    this.input.enabled = false;
  }
}
