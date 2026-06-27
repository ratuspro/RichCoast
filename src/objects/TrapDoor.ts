import Phaser from 'phaser';

export class TrapDoor {
  readonly sprite: Phaser.Physics.Arcade.Image;
  isOpen: boolean;

  constructor(
    scene: Phaser.Scene,
    x: number,
    y: number,
    openDuration: number,
    closedDuration: number,
    startOpen = false,
  ) {
    // Anchor at left edge so x is the left boundary of the gap
    this.sprite = scene.physics.add.image(x + 30, y, 'trapdoor');
    (this.sprite.body as Phaser.Physics.Arcade.Body).setImmovable(true);
    (this.sprite.body as Phaser.Physics.Arcade.Body).allowGravity = false;

    this.isOpen = startOpen;
    this.applyState();

    const cycle = () => {
      this.isOpen = !this.isOpen;
      this.applyState();
      scene.time.delayedCall(this.isOpen ? openDuration : closedDuration, cycle);
    };

    scene.time.delayedCall(startOpen ? openDuration : closedDuration, cycle);
  }

  private applyState() {
    this.sprite.setVisible(!this.isOpen);
    this.sprite.setActive(!this.isOpen);
    if (this.sprite.body) {
      (this.sprite.body as Phaser.Physics.Arcade.Body).enable = !this.isOpen;
    }
  }
}
