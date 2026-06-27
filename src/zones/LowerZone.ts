import Phaser from 'phaser';
import { Gate } from '../objects/Gate';
import { NET_Y } from './UpperZone';

export interface LowerZoneCallbacks {
  onScore: (points: number) => void;
}

export class LowerZone {
  private scene: Phaser.Scene;
  private balls: Phaser.Physics.Arcade.Group;
  private walls: Phaser.Physics.Arcade.StaticGroup;
  private callbacks: LowerZoneCallbacks;

  private gates: Gate[] = [];
  private bottomSensor!: Phaser.Physics.Arcade.Image;

  constructor(
    scene: Phaser.Scene,
    balls: Phaser.Physics.Arcade.Group,
    walls: Phaser.Physics.Arcade.StaticGroup,
    callbacks: LowerZoneCallbacks,
  ) {
    this.scene = scene;
    this.balls = balls;
    this.walls = walls;
    this.callbacks = callbacks;
  }

  create() {
    // Background
    this.scene.add.rectangle(
      195, NET_Y + (844 - NET_Y) / 2, 390, 844 - NET_Y, 0x0d0d1a,
    ).setDepth(0);

    // Bottom scoring sensor (slightly above bottom wall)
    this.bottomSensor = this.scene.physics.add.image(195, 838, '__DEFAULT') as Phaser.Physics.Arcade.Image;
    this.bottomSensor.setVisible(false);
    (this.bottomSensor.body as Phaser.Physics.Arcade.Body).setSize(390, 10);
    (this.bottomSensor.body as Phaser.Physics.Arcade.Body).allowGravity = false;
    (this.bottomSensor.body as Phaser.Physics.Arcade.Body).immovable = true;

    // Ramps
    const ramps = this.scene.physics.add.staticGroup();
    const rampDefs: [number, number, number][] = [
      [70, 580, -30],
      [320, 560, 30],
      [100, 740, -20],
      [290, 760, 20],
    ];
    rampDefs.forEach(([x, y, angle]) => {
      const r = ramps.create(x, y, 'ramp') as Phaser.Physics.Arcade.Image;
      r.setAngle(angle);
      r.refreshBody();
    });

    // Multiplier gates
    const gateDefs: [number, number, 2 | 3, number][] = [
      [130, 510, 2, -20],
      [265, 590, 2, 15],
      [175, 680, 3, -10],
      [295, 740, 3, 5],
    ];
    gateDefs.forEach(([x, y, mult, angle]) => {
      const gate = new Gate(this.scene, x, y, mult, angle);
      this.gates.push(gate);
      this.scene.add.text(gate.sprite.x, gate.sprite.y - 18, `×${gate.multiplier}`, {
        fontSize: '12px',
        color: gate.multiplier === 2 ? '#4ecdc4' : '#ffd700',
        stroke: '#000',
        strokeThickness: 2,
      }).setOrigin(0.5).setDepth(2);
    });

    // Colliders
    this.scene.physics.add.collider(this.balls, ramps);
    this.scene.physics.add.collider(this.balls, this.walls);

    // Bottom sensor overlap
    this.scene.physics.add.overlap(this.balls, this.bottomSensor, (ballObj) => {
      const b = ballObj as Phaser.Physics.Arcade.Image;
      if (!b.active) return;
      b.destroy();
      this.callbacks.onScore(10);
      this.playScoreSound();
    });

    // Gate overlaps for the initial ball group
    this.gates.forEach(gate => this.wireGateOverlap(gate, this.balls));
  }

  update() {
    // Catch balls that slip past the bottom sensor
    this.balls.getChildren().forEach((obj) => {
      const ball = obj as Phaser.Physics.Arcade.Image;
      if (ball.y > 860) {
        ball.destroy();
        this.callbacks.onScore(10);
        this.playScoreSound();
      }
    });
  }

  // Wire gate overlap for a specific ball (called for balls spawned by multiplyBall)
  wireGateOverlap(
    gate: Gate,
    target: Phaser.Physics.Arcade.Image | Phaser.Physics.Arcade.Group,
  ) {
    this.scene.physics.add.overlap(target, gate.sprite, (ballObj) => {
      if (!gate.isReady) return;
      gate.triggerCooldown(this.scene);
      this.multiplyBall(ballObj as Phaser.Physics.Arcade.Image, gate.multiplier);
    });
  }

  // ---- Private helpers ----

  private multiplyBall(ball: Phaser.Physics.Arcade.Image, n: number) {
    const bx = ball.x;
    const by = ball.y;
    const vx = (ball.body as Phaser.Physics.Arcade.Body).velocity.x;
    const vy = (ball.body as Phaser.Physics.Arcade.Body).velocity.y;

    ball.destroy();

    for (let i = 0; i < n; i++) {
      const spread = (i - (n - 1) / 2) * 60;
      const nb = this.scene.physics.add.image(bx + spread * 0.3, by, 'ball') as Phaser.Physics.Arcade.Image;
      nb.setCircle(12);
      nb.setBounce(0.5);
      nb.setVelocity(vx + spread, vy + 50);
      this.balls.add(nb);

      // Wire new ball to all gate overlaps
      this.gates.forEach(gate => this.wireGateOverlap(gate, nb));
    }

    this.playSplitSound();
  }

  // ---- Sounds ----

  private getAudioCtx(): AudioContext | null {
    return (this.scene.sound as Phaser.Sound.WebAudioSoundManager).context ?? null;
  }

  private playSplitSound() {
    const ctx = this.getAudioCtx();
    if (!ctx) return;
    [440, 554, 659].forEach((freq, i) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain); gain.connect(ctx.destination);
      osc.type = 'sine';
      osc.frequency.value = freq;
      gain.gain.setValueAtTime(0, ctx.currentTime + i * 0.04);
      gain.gain.linearRampToValueAtTime(0.2, ctx.currentTime + i * 0.04 + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + i * 0.04 + 0.15);
      osc.start(ctx.currentTime + i * 0.04);
      osc.stop(ctx.currentTime + i * 0.04 + 0.15);
    });
  }

  private playScoreSound() {
    const ctx = this.getAudioCtx();
    if (!ctx) return;
    [523, 659, 784].forEach((freq, i) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain); gain.connect(ctx.destination);
      osc.frequency.value = freq;
      gain.gain.setValueAtTime(0.15, ctx.currentTime + i * 0.06);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + i * 0.06 + 0.1);
      osc.start(ctx.currentTime + i * 0.06);
      osc.stop(ctx.currentTime + i * 0.06 + 0.12);
    });
  }
}
