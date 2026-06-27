import Phaser from 'phaser';

export class BootScene extends Phaser.Scene {
  constructor() {
    super({ key: 'BootScene' });
  }

  preload() {
    const g = this.make.graphics({ x: 0, y: 0 });

    // Ball — red circle
    g.fillStyle(0xff6b6b);
    g.fillCircle(12, 12, 12);
    g.generateTexture('ball', 24, 24);
    g.clear();

    // Boulder — grey circle
    g.fillStyle(0x888888);
    g.fillCircle(20, 20, 20);
    g.fillStyle(0xaaaaaa);
    g.fillCircle(14, 14, 7); // highlight
    g.generateTexture('boulder', 40, 40);
    g.clear();

    // Net segment — white bar
    g.fillStyle(0xffffff);
    g.fillRect(0, 0, 390, 6);
    g.generateTexture('net-segment', 390, 6);
    g.clear();

    // Trap door — dark bar (visible when closed)
    g.fillStyle(0x555555);
    g.fillRect(0, 0, 60, 6);
    g.generateTexture('trapdoor', 60, 6);
    g.clear();

    // Gate ×2 — dumbbell (teal): two circles + bar
    g.fillStyle(0x4ecdc4);
    g.fillRect(8, 7, 44, 6);      // bar
    g.fillCircle(8, 10, 8);       // left weight
    g.fillCircle(52, 10, 8);      // right weight
    g.generateTexture('gate-double', 60, 20);
    g.clear();

    // Gate ×3 — hammer (gold): head + handle
    g.fillStyle(0xffd700);
    g.fillRect(0, 0, 50, 14);     // head
    g.fillRect(18, 14, 14, 26);   // handle
    g.generateTexture('gate-triple', 50, 40);
    g.clear();

    // Ramp — brown rectangle (rotated in scene)
    g.fillStyle(0x6b4c2a);
    g.fillRect(0, 0, 160, 8);
    g.generateTexture('ramp', 160, 8);
    g.clear();

    g.destroy();
  }

  create() {
    this.scene.start('GameScene');
  }
}
