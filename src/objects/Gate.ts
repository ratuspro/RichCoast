import Phaser from 'phaser';

export class Gate {
  readonly sprite: Phaser.Physics.Arcade.Image;
  readonly multiplier: 2 | 3;
  private cooldown = false;

  constructor(
    scene: Phaser.Scene,
    x: number,
    y: number,
    multiplier: 2 | 3,
    angle = 0,
  ) {
    const key = multiplier === 2 ? 'gate-double' : 'gate-triple';
    this.sprite = scene.physics.add.image(x, y, key);
    this.sprite.setAngle(angle);
    (this.sprite.body as Phaser.Physics.Arcade.Body).setImmovable(true);
    (this.sprite.body as Phaser.Physics.Arcade.Body).allowGravity = false;
    this.multiplier = multiplier;
  }

  triggerCooldown(scene: Phaser.Scene) {
    this.cooldown = true;
    this.sprite.setAlpha(0.4);
    scene.time.delayedCall(800, () => {
      this.cooldown = false;
      this.sprite.setAlpha(1);
    });
  }

  get isReady() {
    return !this.cooldown;
  }
}
