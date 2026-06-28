import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { INITIAL_LAYOUT } from './zoneLayout';
import { GateSystem } from './GateSystem';
import { CollectorSystem } from './CollectorSystem';
import { WallSystem } from './WallSystem';
import { BallBuffer } from './BallBuffer';
import {
  createZoneBBall,
  destroyZoneBBall,
  getBallData,
} from './ZoneBBall';

export class ZoneBSystem implements GameSystem {
  private scene?: Phaser.Scene;
  private readonly buffer = new BallBuffer();

  private readonly gates = new GateSystem(INITIAL_LAYOUT.gates, {
    onSplit: (img, multiplier) => this.handleSplit(img, multiplier),
  });
  private readonly collectors = new CollectorSystem(INITIAL_LAYOUT.collectors, {
    onDrain: (img, value, scoreMultiplier) => this.handleDrain(img, value, scoreMultiplier),
  });
  private readonly walls = new WallSystem(INITIAL_LAYOUT.walls);

  private inFlight = 0;
  private total = 0;
  private exhausted = false;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.gates.create(scene);
    this.collectors.create(scene);
    this.walls.create(scene);

    // Emit initial buffer state so the HUD shows the starting count.
    this.emitBuffer();

    this.bus.on(GameEvent.BallDropped, (ball) => {
      if (this.exhausted) return;
      this.buffer.spend();
      this.emitBuffer();

      const img = createZoneBBall(scene, ball.x, Layout.zoneBEntry.y, ball.value, ball.tier);
      this.onBallSpawned();
      void img; // img is live in the Matter world; lifecycle managed by destroy on drain/split
    });
  }

  update(time: number, delta: number): void {
    this.gates.update(time, delta);
    this.collectors.update(time, delta);
    this.walls.update(time, delta);
  }

  // --- Internal handlers ---------------------------------------------------

  private handleSplit(img: Phaser.Physics.Matter.Image, multiplier: number): void {
    const data = getBallData(img.body as MatterJS.BodyType);
    if (!data || !this.scene) return;

    const spawnX = img.x;
    const spawnY = img.y;
    destroyZoneBBall(img);
    this.replaceBall(multiplier);

    // Fan copies symmetrically around straight-down.
    // t ∈ [-0.5, 0.5] maps each copy across the spread; for 1 copy t=0 (straight down).
    const SPREAD = 0.8; // total fan width in radians (~46°)
    for (let i = 0; i < multiplier; i++) {
      const t = multiplier > 1 ? i / (multiplier - 1) - 0.5 : 0;
      const fanAngle = t * SPREAD; // negative = left, positive = right
      // Screen coords: x right, y down. fanAngle=0 → straight down.
      const ox = Math.sin(fanAngle) * 18;
      const oy = Math.cos(fanAngle) * 18;
      const copy = createZoneBBall(
        this.scene,
        spawnX + ox,
        spawnY + oy,
        data.value,
        data.tier,
        true, // fromSplit — temporary gate grace period
      );
      copy.setVelocity(Math.sin(fanAngle) * 3, Math.cos(fanAngle) * 3);
    }
  }

  private handleDrain(
    img: Phaser.Physics.Matter.Image,
    value: number,
    scoreMultiplier: number,
  ): void {
    destroyZoneBBall(img);
    this.addScore(value * scoreMultiplier);
    this.onBallDrained();
  }

  // --- Contract plumbing ---------------------------------------------------

  private onBallSpawned(): void {
    this.inFlight += 1;
    if (this.inFlight === 1) this.bus.emit(GameEvent.ZoneBBusy);
  }

  private onBallDrained(): void {
    if (this.inFlight === 0) return;
    this.inFlight -= 1;
    if (this.inFlight === 0) {
      this.bus.emit(GameEvent.ZoneBEmpty);
      if (this.buffer.isExhausted() && !this.exhausted) {
        this.exhausted = true;
        this.bus.emit(GameEvent.BufferExhausted);
        this.showGameOver();
      }
    }
  }

  /**
   * Called when a gate splits a ball: adjust inFlight by (multiplier - 1).
   * The original ball is already destroyed; multiplier copies will be spawned.
   */
  private replaceBall(multiplier: number): void {
    this.inFlight += multiplier - 1;
    // inFlight was >= 1 and multiplier >= 2, so still >= 2. No BUSY/EMPTY to emit.
  }

  private addScore(points: number): void {
    this.total += points;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });
    if (this.buffer.refillIfMilestone(this.total)) {
      this.emitBuffer();
    }
  }

  private emitBuffer(): void {
    this.bus.emit(GameEvent.BufferChanged, {
      count: this.buffer.getCount(),
      nextMilestone: this.buffer.getNextMilestone(),
    });
  }

  // --- Game-over overlay (self-contained in Zone B) -------------------------

  private showGameOver(): void {
    if (!this.scene) return;
    const { x, y, width, height } = Layout.zoneB;
    const cx = x + width / 2;
    const cy = y + height / 2;

    this.scene.add.rectangle(cx, cy, width, height, 0x000000, 0.75).setDepth(3000);
    this.scene.add
      .text(cx, cy - 24, 'GAME OVER', {
        fontFamily: 'monospace', fontSize: '28px', color: '#ff4466',
      })
      .setOrigin(0.5)
      .setDepth(3001);
    this.scene.add
      .text(cx, cy + 16, `Score: ${this.total}`, {
        fontFamily: 'monospace', fontSize: '18px', color: '#ffffff',
      })
      .setOrigin(0.5)
      .setDepth(3001);
    this.scene.add
      .text(cx, cy + 44, 'Ball buffer exhausted', {
        fontFamily: 'monospace', fontSize: '13px', color: '#aaaaaa',
      })
      .setOrigin(0.5)
      .setDepth(3001);
  }
}
