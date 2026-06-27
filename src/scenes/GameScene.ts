import Phaser from 'phaser';
import { TrapDoor } from '../objects/TrapDoor';
import { Gate } from '../objects/Gate';

const NET_Y = 380;
const FIRE_SPEED = 600;
const GAME_OVER_Y = 80;

export class GameScene extends Phaser.Scene {
  private balls!: Phaser.Physics.Arcade.Group;
  private boulders!: Phaser.Physics.Arcade.StaticGroup;
  private netSegments!: Phaser.Physics.Arcade.StaticGroup;
  private trapdoors: TrapDoor[] = [];
  private gates: Gate[] = [];
  private activeBall: Phaser.Physics.Arcade.Image | null = null;
  private score = 0;
  private scoreText!: Phaser.GameObjects.Text;
  private aimLine!: Phaser.GameObjects.Graphics;
  private dragStart: Phaser.Math.Vector2 | null = null;
  private isGameOver = false;
  private awaitingNextBall = false;

  constructor() {
    super({ key: 'GameScene' });
  }

  create() {
    this.physics.resume();
    this.isGameOver = false;
    this.awaitingNextBall = false;
    this.score = 0;
    this.trapdoors = [];
    this.gates = [];
    this.dragStart = null;
    this.activeBall = null;

    // --- Background gradient hint ---
    const topBg = this.add.rectangle(195, NET_Y / 2, 390, NET_Y, 0x1a1a2e);
    const botBg = this.add.rectangle(195, NET_Y + (844 - NET_Y) / 2, 390, 844 - NET_Y, 0x0d0d1a);
    topBg.setDepth(0);
    botBg.setDepth(0);

    // --- Boulders (top zone) ---
    this.boulders = this.physics.add.staticGroup();
    [[80, 280], [310, 260], [190, 170]].forEach(([x, y]) => {
      const b = this.boulders.create(x, y, 'boulder') as Phaser.Physics.Arcade.Image;
      b.setCircle(20);
      b.refreshBody();
    });

    // --- Net ---
    this.netSegments = this.physics.add.staticGroup();
    // Left segment: 0–120
    this.createNetSegment(60, NET_Y, 120);
    // Middle segment: 180–260
    this.createNetSegment(220, NET_Y, 80);
    // Right segment: 320–390
    this.createNetSegment(355, NET_Y, 70);

    // --- Trap doors (gaps in net) ---
    // Gap 1: x=120–180 (width 60), starts closed
    this.trapdoors.push(new TrapDoor(this, 120, NET_Y, 2500, 1500, false));
    // Gap 2: x=260–320 (width 60), starts open (offset timing)
    this.trapdoors.push(new TrapDoor(this, 260, NET_Y, 2000, 2000, true));

    // Add trapdoor sprites to a static group for collider
    // (handled manually in collider check — see update)

    // --- Ramps (bottom zone) ---
    const ramps = this.physics.add.staticGroup();
    const r1 = ramps.create(70, 580, 'ramp') as Phaser.Physics.Arcade.Image;
    r1.setAngle(-30);
    r1.refreshBody();
    const r2 = ramps.create(320, 560, 'ramp') as Phaser.Physics.Arcade.Image;
    r2.setAngle(30);
    r2.refreshBody();
    const r3 = ramps.create(100, 740, 'ramp') as Phaser.Physics.Arcade.Image;
    r3.setAngle(-20);
    r3.refreshBody();
    const r4 = ramps.create(290, 760, 'ramp') as Phaser.Physics.Arcade.Image;
    r4.setAngle(20);
    r4.refreshBody();

    // --- Multiplier gates (bottom zone) ---
    this.gates.push(new Gate(this, 130, 510, 2, -20));
    this.gates.push(new Gate(this, 265, 590, 2, 15));
    this.gates.push(new Gate(this, 175, 680, 3, -10));
    this.gates.push(new Gate(this, 295, 740, 3, 5));

    // --- Gate multiplier labels ---
    this.gates.forEach(gate => {
      this.add.text(gate.sprite.x, gate.sprite.y - 18, `×${gate.multiplier}`, {
        fontSize: '12px',
        color: gate.multiplier === 2 ? '#4ecdc4' : '#ffd700',
        stroke: '#000',
        strokeThickness: 2,
      }).setOrigin(0.5).setDepth(2);
    });

    // --- Ball group ---
    this.balls = this.physics.add.group();

    // --- Ball vs boulders collider ---
    this.physics.add.collider(this.balls, this.boulders, (_ball) => {
      const b = _ball as Phaser.Physics.Arcade.Image;
      b.setBounce(0.7);
    });

    // --- Ball vs net collider ---
    this.physics.add.collider(this.balls, this.netSegments);

    // --- Ball vs ramps ---
    this.physics.add.collider(this.balls, ramps);

    // --- Ball vs gate overlap ---
    this.gates.forEach(gate => {
      this.physics.add.overlap(this.balls, gate.sprite, (ballObj) => {
        if (!gate.isReady) return;
        gate.triggerCooldown(this);
        this.multiplyBall(ballObj as Phaser.Physics.Arcade.Image, gate.multiplier);
      });
    });

    // --- Aim line ---
    this.aimLine = this.add.graphics().setDepth(5);

    // --- Input ---
    this.input.on('pointerdown', (p: Phaser.Input.Pointer) => {
      if (this.isGameOver || !this.activeBall || this.awaitingNextBall) return;
      this.dragStart = new Phaser.Math.Vector2(p.x / this.scale.displayScale.x, p.y / this.scale.displayScale.y);
    });

    this.input.on('pointermove', (p: Phaser.Input.Pointer) => {
      if (!this.dragStart || !this.activeBall || this.isGameOver) return;
      const px = p.x / this.scale.displayScale.x;
      const py = p.y / this.scale.displayScale.y;
      this.drawAimLine(px, py);
    });

    this.input.on('pointerup', (p: Phaser.Input.Pointer) => {
      if (!this.dragStart || !this.activeBall || this.isGameOver) return;
      const px = p.x / this.scale.displayScale.x;
      const py = p.y / this.scale.displayScale.y;
      this.fireBall(px, py);
      this.dragStart = null;
      this.aimLine.clear();
    });

    // --- Score UI ---
    this.scoreText = this.add.text(16, 16, 'Score: 0', {
      fontSize: '20px',
      color: '#ffffff',
      stroke: '#000000',
      strokeThickness: 3,
    }).setDepth(10).setScrollFactor(0);

    // --- Net label ---
    this.add.text(195, NET_Y - 14, '— NET —', {
      fontSize: '11px',
      color: '#aaaaaa',
    }).setOrigin(0.5).setDepth(3);

    // --- Spawn first ball ---
    this.spawnBallForPlayer();
  }

  update() {
    if (this.isGameOver) return;

    // Trap door colliders (manual, since TrapDoor sprites toggle active)
    this.trapdoors.forEach(td => {
      if (!td.isOpen) {
        this.physics.overlap(this.balls, td.sprite, undefined, () => false);
        this.physics.collide(this.balls, td.sprite);
      }
    });

    // Score balls that exit bottom
    this.balls.getChildren().forEach((obj) => {
      const ball = obj as Phaser.Physics.Arcade.Image;
      if (ball.y > 860) {
        this.score += 10;
        this.scoreText.setText(`Score: ${this.score}`);
        this.playScoreSound();
        ball.destroy();
      }
    });

    // Check lose condition: a fired ball (gravity enabled) has stacked near the ceiling
    const topBalls = this.balls.getChildren().filter((obj) => {
      const b = obj as Phaser.Physics.Arcade.Image;
      return (
        b.active &&
        b !== this.activeBall &&                               // ignore the waiting ball
        (b.body as Phaser.Physics.Arcade.Body).allowGravity && // only fired balls
        b.y < NET_Y
      );
    });
    if (topBalls.some((obj) => (obj as Phaser.Physics.Arcade.Image).y < GAME_OVER_Y)) {
      this.triggerGameOver();
    }

    // If active ball has drifted below NET_Y, it's no longer in top zone — spawn next
    if (
      this.activeBall &&
      this.activeBall.active &&
      this.activeBall.y > NET_Y + 20 &&
      !this.awaitingNextBall
    ) {
      this.activeBall = null;
      this.scheduleNextBall();
    }
  }

  // --- Helpers ---

  private createNetSegment(cx: number, y: number, width: number) {
    const seg = this.netSegments.create(cx, y, 'net-segment') as Phaser.Physics.Arcade.Image;
    seg.setDisplaySize(width, 6);
    seg.refreshBody();
  }

  private spawnBallForPlayer() {
    if (this.isGameOver) return;
    this.awaitingNextBall = false;
    const ball = this.physics.add.image(195, 50, 'ball') as Phaser.Physics.Arcade.Image;
    ball.setCircle(12);
    ball.setBounce(0.6);
    ball.setCollideWorldBounds(true);
    ball.setImmovable(false);
    (ball.body as Phaser.Physics.Arcade.Body).allowGravity = false; // float until fired
    this.balls.add(ball);
    this.activeBall = ball;
  }

  private scheduleNextBall() {
    this.awaitingNextBall = true;
    this.time.delayedCall(600, () => this.spawnBallForPlayer());
  }

  private fireBall(px: number, py: number) {
    if (!this.activeBall) return;
    const ball = this.activeBall;
    // Direction: from drag start toward release point reversed (aim opposite to drag)
    const dx = this.dragStart!.x - px;
    const dy = this.dragStart!.y - py;
    const len = Math.sqrt(dx * dx + dy * dy);
    if (len < 5) return; // too small a drag, ignore

    const nx = dx / len;
    const ny = dy / len;

    // Clamp to downward half (dy > 0 in game coords going down)
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

    // Draw dashed line
    for (let i = 0; i < 8; i++) {
      const t0 = i * 20;
      const t1 = t0 + 10;
      this.aimLine.beginPath();
      this.aimLine.moveTo(bx + nx * t0, by + ny * t0);
      this.aimLine.lineTo(bx + nx * t1, by + ny * t1);
      this.aimLine.strokePath();
    }
  }

  private multiplyBall(ball: Phaser.Physics.Arcade.Image, n: number) {
    const bx = ball.x;
    const by = ball.y;
    const vx = (ball.body as Phaser.Physics.Arcade.Body).velocity.x;
    const vy = (ball.body as Phaser.Physics.Arcade.Body).velocity.y;

    ball.destroy();

    for (let i = 0; i < n; i++) {
      const spread = (i - (n - 1) / 2) * 60;
      const nb = this.physics.add.image(bx + spread * 0.3, by, 'ball') as Phaser.Physics.Arcade.Image;
      nb.setCircle(12);
      nb.setBounce(0.5);
      nb.setCollideWorldBounds(true);
      nb.setVelocity(vx + spread, vy + 50);
      this.balls.add(nb);

      // Wire new ball to gate overlaps
      this.gates.forEach(gate => {
        this.physics.add.overlap(nb, gate.sprite, () => {
          if (!gate.isReady) return;
          gate.triggerCooldown(this);
          this.multiplyBall(nb, gate.multiplier);
        });
      });
    }

    this.playSplitSound();

    // If this was the active ball, schedule next
    if (ball === this.activeBall && !this.awaitingNextBall) {
      this.activeBall = null;
      this.scheduleNextBall();
    }
  }

  private triggerGameOver() {
    if (this.isGameOver) return;
    this.isGameOver = true;
    this.physics.pause();
    this.aimLine.clear();

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

    btn.on('pointerdown', () => {
      this.physics.resume();
      this.scene.start('GameScene');
    });

    this.playGameOverSound();
  }

  // --- Synthesized sounds ---

  private getAudioCtx(): AudioContext | null {
    return (this.sound as Phaser.Sound.WebAudioSoundManager).context ?? null;
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

  private playGameOverSound() {
    const ctx = this.getAudioCtx();
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
