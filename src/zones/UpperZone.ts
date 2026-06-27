import Phaser from 'phaser';
import { TrapDoor } from '../objects/TrapDoor';

export const NET_Y = 380;
const FIRE_SPEED = 600;
const GAME_OVER_Y = 80;

export interface UpperZoneCallbacks {
  onGameOver: () => void;
  onBallPassedNet: () => void;
}

export class UpperZone {
  private scene: Phaser.Scene;
  private balls: Phaser.Physics.Arcade.Group;
  private walls: Phaser.Physics.Arcade.StaticGroup;
  private callbacks: UpperZoneCallbacks;
  private getIsGameOver: () => boolean;

  private boulders!: Phaser.Physics.Arcade.StaticGroup;
  netSegments!: Phaser.Physics.Arcade.StaticGroup;
  trapdoors: TrapDoor[] = [];

  private activeBall: Phaser.Physics.Arcade.Image | null = null;
  private aimLine!: Phaser.GameObjects.Graphics;
  private dragStart: Phaser.Math.Vector2 | null = null;
  private awaitingNextBall = false;

  constructor(
    scene: Phaser.Scene,
    balls: Phaser.Physics.Arcade.Group,
    walls: Phaser.Physics.Arcade.StaticGroup,
    callbacks: UpperZoneCallbacks,
    getIsGameOver: () => boolean,
  ) {
    this.scene = scene;
    this.balls = balls;
    this.walls = walls;
    this.callbacks = callbacks;
    this.getIsGameOver = getIsGameOver;
  }

  create() {
    // Background
    this.scene.add.rectangle(195, NET_Y / 2, 390, NET_Y, 0x1a1a2e).setDepth(0);

    // Boulders
    this.boulders = this.scene.physics.add.staticGroup();
    [[80, 280], [310, 260], [190, 170]].forEach(([x, y]) => {
      const b = this.boulders.create(x, y, 'boulder') as Phaser.Physics.Arcade.Image;
      b.setCircle(20);
      b.refreshBody();
    });

    // Net segments
    this.netSegments = this.scene.physics.add.staticGroup();
    this.createNetSegment(60, NET_Y, 120);   // left:   0–120
    this.createNetSegment(220, NET_Y, 80);   // middle: 180–260
    this.createNetSegment(355, NET_Y, 70);   // right:  320–390

    // Net label
    this.scene.add.text(195, NET_Y - 14, '— NET —', {
      fontSize: '11px',
      color: '#aaaaaa',
    }).setOrigin(0.5).setDepth(3);

    // Trapdoors (gaps in net)
    this.trapdoors.push(new TrapDoor(this.scene, 120, NET_Y, 2500, 1500, false)); // gap 120–180
    this.trapdoors.push(new TrapDoor(this.scene, 260, NET_Y, 2000, 2000, true));  // gap 260–320

    // Colliders
    this.scene.physics.add.collider(this.balls, this.boulders, (_ball) => {
      (_ball as Phaser.Physics.Arcade.Image).setBounce(0.7);
    });
    this.scene.physics.add.collider(this.balls, this.netSegments);
    this.scene.physics.add.collider(this.balls, this.walls);

    // Aim line
    this.aimLine = this.scene.add.graphics().setDepth(5);

    // Input
    this.scene.input.on('pointerdown', (p: Phaser.Input.Pointer) => {
      if (this.getIsGameOver() || !this.activeBall || this.awaitingNextBall) return;
      this.dragStart = new Phaser.Math.Vector2(
        p.x / this.scene.scale.displayScale.x,
        p.y / this.scene.scale.displayScale.y,
      );
    });

    this.scene.input.on('pointermove', (p: Phaser.Input.Pointer) => {
      if (!this.dragStart || !this.activeBall || this.getIsGameOver()) return;
      this.drawAimLine(
        p.x / this.scene.scale.displayScale.x,
        p.y / this.scene.scale.displayScale.y,
      );
    });

    this.scene.input.on('pointerup', (p: Phaser.Input.Pointer) => {
      if (!this.dragStart || !this.activeBall || this.getIsGameOver()) return;
      this.fireBall(
        p.x / this.scene.scale.displayScale.x,
        p.y / this.scene.scale.displayScale.y,
      );
      this.dragStart = null;
      this.aimLine.clear();
    });

    this.spawnBallForPlayer();
  }

  update() {
    if (this.getIsGameOver()) return;

    // Trapdoor colliders (manual — TrapDoor sprites toggle active)
    this.trapdoors.forEach(td => {
      if (!td.isOpen) {
        this.scene.physics.overlap(this.balls, td.sprite, undefined, () => false);
        this.scene.physics.collide(this.balls, td.sprite);
      }
    });

    // Game-over: a fired ball has stacked near the ceiling
    const firedBallsInTopZone = this.balls.getChildren().filter((obj) => {
      const b = obj as Phaser.Physics.Arcade.Image;
      return (
        b.active &&
        b !== this.activeBall &&
        (b.body as Phaser.Physics.Arcade.Body).allowGravity &&
        b.y < NET_Y
      );
    });
    if (firedBallsInTopZone.some(obj => (obj as Phaser.Physics.Arcade.Image).y < GAME_OVER_Y)) {
      this.callbacks.onGameOver();
    }

    // Active ball crossed the net → schedule next
    if (
      this.activeBall &&
      this.activeBall.active &&
      this.activeBall.y > NET_Y + 20 &&
      !this.awaitingNextBall
    ) {
      this.activeBall = null;
      this.callbacks.onBallPassedNet();
      this.scheduleNextBall();
    }
  }

  clearAimLine() {
    this.aimLine.clear();
  }

  // ---- Private helpers ----

  private createNetSegment(cx: number, y: number, width: number) {
    const seg = this.netSegments.create(cx, y, 'net-segment') as Phaser.Physics.Arcade.Image;
    seg.setDisplaySize(width, 6);
    seg.refreshBody();
  }

  private spawnBallForPlayer() {
    if (this.getIsGameOver()) return;
    this.awaitingNextBall = false;
    const ball = this.scene.physics.add.image(195, 50, 'ball') as Phaser.Physics.Arcade.Image;
    ball.setCircle(12);
    ball.setBounce(0.6);
    ball.setCollideWorldBounds(true);
    ball.setImmovable(false);
    (ball.body as Phaser.Physics.Arcade.Body).allowGravity = false;
    this.balls.add(ball);
    this.activeBall = ball;
  }

  private scheduleNextBall() {
    this.awaitingNextBall = true;
    this.scene.time.delayedCall(600, () => this.spawnBallForPlayer());
  }

  private fireBall(px: number, py: number) {
    if (!this.activeBall) return;
    const ball = this.activeBall;
    const dx = this.dragStart!.x - px;
    const dy = this.dragStart!.y - py;
    const len = Math.sqrt(dx * dx + dy * dy);
    if (len < 5) return;

    const nx = dx / len;
    const ny = dy / len;
    const finalNy = Math.abs(ny) < 0.1 ? 0.3 : ny;

    (ball.body as Phaser.Physics.Arcade.Body).allowGravity = true;
    ball.setVelocity(nx * FIRE_SPEED, finalNy * FIRE_SPEED);
    ball.setBounce(0.6);

    this.playFireSound();
  }

  private drawAimLine(px: number, py: number) {
    if (!this.activeBall || !this.dragStart) return;
    const dx = this.dragStart.x - px;
    const dy = this.dragStart.y - py;
    const len = Math.sqrt(dx * dx + dy * dy);
    if (len < 5) return;

    this.aimLine.clear();
    this.aimLine.lineStyle(2, 0xffffff, 0.5);

    const bx = this.activeBall.x;
    const by = this.activeBall.y;
    const nx = dx / len;
    const ny = dy / len;

    for (let i = 0; i < 8; i++) {
      const t0 = i * 20;
      const t1 = t0 + 10;
      this.aimLine.beginPath();
      this.aimLine.moveTo(bx + nx * t0, by + ny * t0);
      this.aimLine.lineTo(bx + nx * t1, by + ny * t1);
      this.aimLine.strokePath();
    }
  }

  // ---- Sounds ----

  private getAudioCtx(): AudioContext | null {
    return (this.scene.sound as Phaser.Sound.WebAudioSoundManager).context ?? null;
  }

  private playFireSound() {
    const ctx = this.getAudioCtx();
    if (!ctx) return;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain); gain.connect(ctx.destination);
    osc.frequency.setValueAtTime(400, ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(150, ctx.currentTime + 0.12);
    gain.gain.setValueAtTime(0.2, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.15);
    osc.start(ctx.currentTime); osc.stop(ctx.currentTime + 0.15);
  }
}
