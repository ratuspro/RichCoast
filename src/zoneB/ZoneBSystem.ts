import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { Sfx } from '../core/Sfx';
import { pickRandomLayout } from './zoneLayout';
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
const BAR_COLOR_BG = 0xe0d2b8; // a groove pressed into the paper (between paper and pine)
const BAR_COLOR_FILL = 0xc9973f; // Theme.brass — the bar fills with brass

/** How long the bar sits pinned full before it starts draining out. Tune by playtest. */
const CASH_IN_DWELL_MS = 600;
/** How long the drain-out tween (full -> 0) takes. Tune by playtest. */
const CASH_IN_DRAIN_MS = 400;

export class ZoneBSystem implements GameSystem {
  private scene?: Phaser.Scene;
  private readonly scoreBar = new ScoreBar();

  // One of the two layouts, chosen at random per run (this system is reconstructed on every
  // scene boot, including scene.restart()).
  private readonly gates: GateSystem;
  private readonly collectors: CollectorSystem;
  private readonly walls: WallSystem;

  private inFlight = 0;
  private total = 0;
  /** True only during the drain-out tween — see beginDrainOut(). Suppresses the normal
   *  fill-width recompute in updateBarVisual() so the tween's own width writes aren't
   *  immediately overwritten by a same-tick emitScoreBar() (e.g. from a ProgressionChanged
   *  listener firing inside the ScoreBarFilled emit below). */
  private draining = false;

  private barFill?: Phaser.GameObjects.Rectangle;
  private barLabel?: Phaser.GameObjects.Text;

  constructor(private readonly bus: EventBus) {
    const layout = pickRandomLayout();
    this.gates = new GateSystem(layout.gates, {
      onSplit: (img, multiplier) => this.handleSplit(img, multiplier),
    });
    this.collectors = new CollectorSystem(layout.collectors, {
      onDrain: (img, value, scoreMultiplier) => this.handleDrain(img, value, scoreMultiplier),
    });
    this.walls = new WallSystem(layout.walls);
  }

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
    if (multiplier > 1) Sfx.multiply(multiplier);

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
    Sfx.collect(value);
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
    if (filled) this.beginCashIn();
  }

  /**
   * The bar just crossed its target. Play the reward cue immediately, then hold the bar
   * full for a dwell beat before draining it out — the buffer refill (ScoreBarFilled) only
   * fires once the drain-out visual finishes, so filling the bar reads as an event instead
   * of an instant, invisible jump.
   */
  private beginCashIn(): void {
    Sfx.goal();
    this.scene?.time.delayedCall(CASH_IN_DWELL_MS, () => this.beginDrainOut());
  }

  /** Tween the bar's fill width from full to 0, then resolve the cash-in and hand off to Zone A. */
  private beginDrainOut(): void {
    if (!this.scene || !this.barFill) return;
    this.draining = true;
    const proxy = { w: Layout.zoneB.width };
    this.scene.tweens.add({
      targets: proxy,
      w: 0,
      duration: CASH_IN_DRAIN_MS,
      ease: 'Cubic.easeIn',
      onUpdate: () => {
        if (this.barFill) this.barFill.width = proxy.w;
      },
      onComplete: () => {
        this.draining = false;
        // Emit first: Zone A's ScoreBarFilled handler bumps the level and (synchronously,
        // via the ProgressionChanged listener below) may update this.scoreBar's target
        // before we resolve the cash-in — so a cascade check compares the banked overflow
        // against the new target, not the one that was current when the bar filled.
        this.bus.emit(GameEvent.ScoreBarFilled);
        const cascade = this.scoreBar.completeCashIn();
        this.emitScoreBar();
        if (cascade) this.beginCashIn();
      },
    });
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
        color: '#8a7a64', // Theme.inkSoft
      })
      .setOrigin(0.5, 1)
      .setDepth(12);
  }

  private updateBarVisual(): void {
    if (!this.barFill || this.draining) return;
    const { x, width } = Layout.zoneB;
    this.barFill.width = width * Math.min(1, this.scoreBar.getProgress());
    this.barFill.x = x;
    this.barLabel?.setText(
      `${this.scoreBar.getFilled()} / ${this.scoreBar.getTarget()}`,
    );
  }
}
