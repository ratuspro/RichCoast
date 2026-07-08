import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { compactValue } from '../core/Materials';
import { Theme } from '../core/Theme';
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

const BAR_HEIGHT = 16; // tall enough to seat the label inside the groove
const BAR_COLOR_BG = 0xe0d2b8; // a groove pressed into the paper (between paper and pine)
const BAR_COLOR_FILL = 0xc9973f; // Theme.brass — the bar fills with brass
const BAR_STROKE_COLOR = 0x3f3428; // Theme.ink — the bar's dark outline
const BAR_STROKE_WIDTH = 2;
const BAR_LABEL_COLOR = '#3f3428'; // Theme.ink — reads on both the groove and the brass fill

/** Per-16ms fraction the shown fill closes toward the logical value, so the bar
 *  glides up instead of snapping on every drained ball. */
const FILL_LERP = 0.2;

/** How long the bar sits pinned full before the cash-in resolves. Tune by playtest. */
const CASH_IN_DWELL_MS = 600;

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
  /** True once the bar has crossed target but Zone B still has balls in flight — the dwell
   *  timer doesn't start until onBallDrained() sees inFlight reach 0 and clears this. */
  private pendingCashIn = false;

  private barBg?: Phaser.GameObjects.Rectangle;
  private barFill?: Phaser.GameObjects.Rectangle;
  private barLabel?: Phaser.GameObjects.Text;
  private barGeom = { x: 0, width: 0, fillX: 0, fillW: 0, midY: 0 };
  /** The fill value currently shown; eased toward `scoreBar.getFilled()` each frame. */
  private displayFilled = 0;
  /** Guards the fill celebration (pulse + sparkle) to once per fill cycle. */
  private celebrated = false;

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
    this.animateBar(delta);
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
      if (this.pendingCashIn) {
        this.pendingCashIn = false;
        this.armCashInTimer();
      }
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
    if (filled) this.onBarFilled();
  }

  /**
   * The bar just crossed its target. Play the reward cue immediately — that always happens
   * on the instant of crossing, whether or not Zone B is still busy. If Zone B has no balls
   * in flight right now, arm the dwell timer immediately; otherwise wait — onBallDrained()
   * will arm it the moment Zone B actually empties, so the bar stays pinned full and static
   * for as long as balls are still cascading through gates and collectors.
   */
  private onBarFilled(): void {
    Sfx.goal();
    if (this.inFlight === 0) {
      this.armCashInTimer();
    } else {
      this.pendingCashIn = true;
    }
  }

  /** Hold the bar full for a dwell beat, then resolve the cash-in. Only ever called once
   *  Zone B is confirmed empty (immediately in onBarFilled, or deferred via pendingCashIn). */
  private armCashInTimer(): void {
    this.scene?.time.delayedCall(CASH_IN_DWELL_MS, () => this.resolveCashIn());
  }

  /**
   * Resolve the cash-in: hand off to Zone A, then snap the bar back to its carried-over
   * value. No drain-out tween — the reward beat is Zone A's particle flight up to the
   * queue-row count (launched off ScoreBarFilled), and the bar resetting the instant the
   * particles leave reads as its energy departing. The bar itself only ever animates
   * upward, as balls drain in Zone B.
   */
  private resolveCashIn(): void {
    // Emit first: Zone A's ScoreBarFilled handler bumps the level and (synchronously,
    // via the ProgressionChanged listener below) may update this.scoreBar's target
    // before we resolve the cash-in — so a cascade check compares the banked overflow
    // against the new target, not the one that was current when the bar filled.
    this.bus.emit(GameEvent.ScoreBarFilled);
    const cascade = this.scoreBar.completeCashIn();
    this.emitScoreBar();
    if (cascade) this.onBarFilled();
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
    // Flush with the bottom of the screen; the label sits centred inside the bar.
    const barTop = y + height - BAR_HEIGHT;
    const midY = barTop + BAR_HEIGHT / 2;
    // The fill sits inside the outlined groove, inset by the stroke on every side.
    const fillX = x + BAR_STROKE_WIDTH;
    const fillW = width - 2 * BAR_STROKE_WIDTH;
    this.barGeom = { x, width, fillX, fillW, midY };

    this.barBg = scene.add
      .rectangle(x + width / 2, midY, width, BAR_HEIGHT, BAR_COLOR_BG)
      .setStrokeStyle(BAR_STROKE_WIDTH, BAR_STROKE_COLOR)
      .setDepth(10)
      .setOrigin(0.5);

    this.barFill = scene.add
      .rectangle(fillX, midY, 0, BAR_HEIGHT - 2 * BAR_STROKE_WIDTH, BAR_COLOR_FILL)
      .setDepth(11)
      .setOrigin(0, 0.5);

    this.barLabel = scene.add
      .text(x + width / 2, midY, '', {
        fontFamily: 'monospace',
        fontSize: '11px',
        fontStyle: 'bold',
        color: BAR_LABEL_COLOR,
      })
      .setOrigin(0.5, 0.5)
      .setDepth(12);
  }

  /**
   * Ease the shown fill toward the logical value every frame so both the brass fill and the
   * X/Y label climb smoothly as balls drain — including right up to a full bar. A cash-in
   * reset (logical value drops below what's shown) snaps down instantly: the bar only ever
   * animates upward. The instant the shown fill reaches a full bar, fire the celebration.
   */
  private animateBar(delta: number): void {
    if (!this.barFill) return;
    const target = this.scoreBar.getFilled();
    if (target < this.displayFilled - 1e-3) {
      this.displayFilled = target; // reset — snap, never animate downward
      this.celebrated = false;
    } else {
      const k = 1 - Math.pow(1 - FILL_LERP, delta / 16.67);
      this.displayFilled += (target - this.displayFilled) * k;
      if (target - this.displayFilled < 0.02) this.displayFilled = target;
    }
    this.renderBar();

    if (!this.celebrated && this.displayFilled >= this.scoreBar.getTarget()) {
      this.celebrated = true;
      this.celebrateFull();
    }
  }

  /** Paint the bar + label from `displayFilled`. Cheap; safe to call every frame. */
  private renderBar(): void {
    if (!this.barFill || !this.barLabel) return;
    const target = this.scoreBar.getTarget();
    this.barFill.width = this.barGeom.fillW * Math.min(1, this.displayFilled / target);
    this.barLabel.setText(
      `${compactValue(Math.round(this.displayFilled))} / ${compactValue(target)}`,
    );
  }

  private updateBarVisual(): void {
    // The per-frame animator owns the fill width + label; this just refreshes on discrete
    // events (initial build, target change) so the bar isn't blank until the next tick.
    this.renderBar();
  }

  /** A full bar throbs and throws off a rising brass sparkle — the "you filled it" beat. */
  private celebrateFull(): void {
    const scene = this.scene;
    if (!scene) return;

    // Vertical throb of the groove + fill (both centred on midY, so it puffs in place).
    scene.tweens.add({
      targets: [this.barBg, this.barFill],
      scaleY: 1.6,
      duration: 130,
      yoyo: true,
      ease: 'Sine.InOut',
    });
    scene.tweens.add({
      targets: this.barLabel,
      scale: 1.3,
      duration: 130,
      yoyo: true,
      ease: 'Sine.InOut',
    });

    // Brass sparkle rising off the full bar.
    const { x, width, midY } = this.barGeom;
    for (let i = 0; i < 16; i++) {
      const px = x + Phaser.Math.Between(6, width - 6);
      const p = scene.add
        .circle(px, midY, Phaser.Math.Between(2, 4), Theme.brassBright)
        .setDepth(13);
      scene.tweens.add({
        targets: p,
        y: midY - Phaser.Math.Between(26, 60),
        x: px + Phaser.Math.Between(-14, 14),
        alpha: 0,
        scale: 0,
        duration: Phaser.Math.Between(420, 720),
        ease: 'Quad.Out',
        onComplete: () => p.destroy(),
      });
    }
  }
}
