import Phaser from 'phaser';
import { UpperZone } from '../zones/UpperZone';
import { LowerZone } from '../zones/LowerZone';

export class GameScene extends Phaser.Scene {
  private balls!: Phaser.Physics.Arcade.Group;
  private walls!: Phaser.Physics.Arcade.StaticGroup;

  private upperZone!: UpperZone;
  private lowerZone!: LowerZone;

  private score = 0;
  private scoreText!: Phaser.GameObjects.Text;
  private isGameOver = false;

  constructor() {
    super({ key: 'GameScene' });
  }

  create() {
    this.physics.resume();
    this.isGameOver = false;
    this.score = 0;

    // Shared boundary walls — bodies inset so inner edge sits exactly on the boundary
    const WALL = 4;
    this.walls = this.physics.add.staticGroup();

    const addWall = (cx: number, cy: number, w: number, h: number) => {
      const wall = this.walls.create(cx, cy, '__DEFAULT') as Phaser.Physics.Arcade.Image;
      wall.setVisible(false);
      (wall.body as Phaser.Physics.Arcade.StaticBody).setSize(w, h);
      wall.refreshBody();
    };
    //                cx             cy              w    h
    addWall(195,          WALL / 2,      390, WALL); // top    (inner edge y=0)
    addWall(WALL / 2,     422,      WALL, 844);      // left   (inner edge x=0)
    addWall(390 - WALL/2, 422,      WALL, 844);      // right  (inner edge x=390)
    addWall(195,          844-WALL/2,   390, WALL);  // bottom (inner edge y=844)

    // Visual edges — glowing lines at the inner surface of each wall
    const g = this.add.graphics().setDepth(8);
    const drawEdge = (x1: number, y1: number, x2: number, y2: number) => {
      g.lineStyle(WALL + 4, 0x6a6aaa, 0.25);
      g.beginPath(); g.moveTo(x1, y1); g.lineTo(x2, y2); g.strokePath();
      g.lineStyle(WALL, 0x9090cc, 1);
      g.beginPath(); g.moveTo(x1, y1); g.lineTo(x2, y2); g.strokePath();
    };
    drawEdge(0,   0,   390, 0);   // top
    drawEdge(0,   0,   0,   844); // left
    drawEdge(390, 0,   390, 844); // right
    drawEdge(0,   844, 390, 844); // bottom

    // Shared ball group
    this.balls = this.physics.add.group();

    // Score UI
    this.scoreText = this.add.text(16, 16, 'Score: 0', {
      fontSize: '20px',
      color: '#ffffff',
      stroke: '#000000',
      strokeThickness: 3,
    }).setDepth(10).setScrollFactor(0);

    // Zones
    this.upperZone = new UpperZone(
      this,
      this.balls,
      this.walls,
      {
        onGameOver: () => this.triggerGameOver(),
        onBallPassedNet: () => {},
      },
      () => this.isGameOver,
    );

    this.lowerZone = new LowerZone(
      this,
      this.balls,
      this.walls,
      {
        onScore: (points: number) => {
          this.score += points;
          this.scoreText.setText(`Score: ${this.score}`);
        },
      },
    );

    this.upperZone.create();
    this.lowerZone.create();
  }

  update() {
    if (this.isGameOver) return;
    this.upperZone.update();
    this.lowerZone.update();
  }

  private triggerGameOver() {
    if (this.isGameOver) return;
    this.isGameOver = true;
    this.physics.pause();
    this.upperZone.clearAimLine();

    this.add.rectangle(195, 422, 300, 200, 0x000000, 0.8).setDepth(20);
    this.add.text(195, 360, 'GAME OVER', {
      fontSize: '32px',
      color: '#ff6b6b',
      stroke: '#000',
      strokeThickness: 4,
    }).setOrigin(0.5).setDepth(21);
    this.add.text(195, 410, `Score: ${this.score}`, {
      fontSize: '22px',
      color: '#ffffff',
    }).setOrigin(0.5).setDepth(21);

    const btn = this.add.text(195, 460, '[ Restart ]', {
      fontSize: '20px',
      color: '#4ecdc4',
      stroke: '#000',
      strokeThickness: 2,
    }).setOrigin(0.5).setDepth(21).setInteractive({ useHandCursor: true });

    btn.on('pointerdown', () => this.scene.start('GameScene'));

    this.playGameOverSound();
  }

  private playGameOverSound() {
    const ctx = (this.sound as Phaser.Sound.WebAudioSoundManager).context;
    if (!ctx) return;
    [440, 330, 220, 165].forEach((freq, i) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain); gain.connect(ctx.destination);
      osc.frequency.value = freq;
      gain.gain.setValueAtTime(0.25, ctx.currentTime + i * 0.18);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + i * 0.18 + 0.25);
      osc.start(ctx.currentTime + i * 0.18);
      osc.stop(ctx.currentTime + i * 0.18 + 0.3);
    });
  }
}
