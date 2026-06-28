import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { INITIAL_LAYOUT } from './zoneLayout';
import { GateSystem } from './GateSystem';
import { CollectorSystem } from './CollectorSystem';
import { WallSystem } from './WallSystem';
import { ScoreBar } from './ScoreBar';
import {
  createZoneBBall,
  destroyZoneBBall,
  getBallData,
} from './ZoneBBall';

const BAR_HEIGHT = 10;
const BAR_COLOR_BG = 0x1a2535;
const BAR_COLOR_FILL = 0x4488ff;

export class ZoneBSystem implements GameSystem {
  private scene?: Phaser.Scene;
  private readonly scoreBar = new ScoreBar();

  private readonly gates = new GateSystem(INITIAL_LAYOUT.gates, {
    onSplit: (img, multiplier) => this.handleSplit(img, multiplier),
  });
  private readonly collectors = new CollectorSystem(INITIAL_LAYOUT.collectors, {
    onDrain: (img, value, scoreMultiplier) => this.handleDrain(img, value, scoreMultiplier),
  });
  private readonly walls = new WallSystem(INITIAL_LAYOUT.walls);

  private inFlight = 0;
  private total = 0;

  private barFill?: Phaser.GameObjects.Rectangle;
  private barLabel?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.gates.create(scene);
    this.collectors.create(scene);
    this.walls.create(scene);

    this.buildScoreBar(scene);
    this.emitScoreBar();

    this.bus.on(GameEvent.ProgressionChanged, ({ scoreBarTarget }) => {
      this.scoreBar.setTarget(scoreBarTarget);
      this.emitScoreBar();
    });

    this.bus.on(GameEvent.BallDropped, (ball) => {
      const img = createZoneBBall(scene, ball.x, Layout.zoneBEntry.y, ball.value, ball.tier);
      this.onBallSpawned();
      void img;
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

    const SPREAD = 0.8;
    for (let i = 0; i < multiplier; i++) {
      const t = multiplier > 1 ? i / (multiplier - 1) - 0.5 : 0;
      const fanAngle = t * SPREAD;
      const ox = Math.sin(fanAngle) * 18;
      const oy = Math.cos(fanAngle) * 18;
      const copy = createZoneBBall(
        this.scene,
        spawnX + ox,
        spawnY + oy,
        data.value,
        data.tier,
        true,
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
    }
  }

  private replaceBall(multiplier: number): void {
    this.inFlight += multiplier - 1;
  }

  private addScore(points: number): void {
    this.total += points;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });

    const filled = this.scoreBar.add(points);
    this.emitScoreBar();
    if (filled) {
      this.bus.emit(GameEvent.ScoreBarFilled);
    }
  }

  private emitScoreBar(): void {
    this.bus.emit(GameEvent.ScoreBarChanged, {
      filled: this.scoreBar.getFilled(),
      target: this.scoreBar.getTarget(),
    });
    this.updateBarVisual();
  }

  // --- Score bar visual ----------------------------------------------------

  private buildScoreBar(scene: Phaser.Scene): void {
    const { x, y, width, height } = Layout.zoneB;
    const barY = y + height - BAR_HEIGHT;

    scene.add
      .rectangle(x + width / 2, barY + BAR_HEIGHT / 2, width, BAR_HEIGHT, BAR_COLOR_BG)
      .setDepth(10)
      .setOrigin(0.5);

    this.barFill = scene.add
      .rectangle(x, barY + BAR_HEIGHT / 2, 0, BAR_HEIGHT, BAR_COLOR_FILL)
      .setDepth(11)
      .setOrigin(0, 0.5);

    this.barLabel = scene.add
      .text(x + width / 2, barY - 4, '', {
        fontFamily: 'monospace',
        fontSize: '11px',
        color: '#8899bb',
      })
      .setOrigin(0.5, 1)
      .setDepth(12);
  }

  private updateBarVisual(): void {
    if (!this.barFill) return;
    const { x, width } = Layout.zoneB;
    this.barFill.width = width * this.scoreBar.getProgress();
    this.barFill.x = x;
    this.barLabel?.setText(
      `${this.scoreBar.getFilled()} / ${this.scoreBar.getTarget()}`,
    );
  }
}
