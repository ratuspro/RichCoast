import Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { compactValue, hexColor } from '../core/Materials';
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

const BAR_HEIGHT = 32; // tall enough to seat the label inside the groove
const BAR_STROKE_WIDTH = 2;
// Bar colours come from the live Theme: `groove` for the pressed-in bg, `brass` for the
// fill, `ink` for the outline + label — re-applied in restyleBar() on palette swaps.

/** Per-16ms fraction the shown fill closes toward the logical value, so the bar
 *  glides up instead of snapping on every drained ball. */
const FILL_LERP = 0.2;

/** How long a full bar takes to sweep to full during a live wrap (ms), before it snaps to
 *  empty for the next level. Keeps the fill/empty readable when a level is crossed. */
const WRAP_FILL_MS = 150;

/** Brief beat the settled final bar holds after the last ball drains, before the pan up. */
const SETTLE_DWELL_MS = 350;

/** Hard ceiling on level-ups one cash-in cycle can bank. Targets are tuned (and, past the
 *  authored table, extrapolated) toward ~one level per drain, but merged ball values are
 *  unbounded — a freak monster ball could cross a target thousands of times in one drain,
 *  wedging the game behind hours of owed wraps (each is WRAP_FILL_MS + a celebration) and
 *  exploding Zone A's level/buffer. At the cap the rest of the fill is forfeited
 *  (ScoreBar.forfeitOverflow) and the cycle resolves normally. 10 wraps ≈ 1.5s of roll. */
const MAX_LEVELS_PER_CASHIN = 10;

/** Hovering haul label (above the score bar): its base scale grows with the running round haul
 *  — 1× at zero up to HAUL_MAX_SCALE — so the number visibly swells as the round earns more. */
const HAUL_MAX_SCALE = 1.9;
const HAUL_GROWTH = 0.13;
/** A short overshoot pop each time score is added, settling back to the magnitude base scale. */
const HAUL_POP_MS = 180;

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
  /** True once the bar has crossed a target this cycle (≥1 level earned) — so once Zone B
   *  drains, the cash-in resolves (pan up) rather than just re-arming the trap door. */
  private pendingCashIn = false;
  /** Owed display wraps: each live target crossing adds one, and animateBar() works them off
   *  by sweeping the bar to full and snapping it to empty — the visible fill/empty/fill roll. */
  private pendingWraps = 0;
  /** Level-ups banked this cash-in cycle, counted against MAX_LEVELS_PER_CASHIN and reset
   *  when the cycle resolves (updateResolve). */
  private cycleLevels = 0;
  /** Set when Zone B has drained with a cash-in owed; the pan waits (in update) for the bar to
   *  finish its wraps, then a short settle dwell, before ZONE_B_EMPTY + SCORE_BAR_CASHED_IN. */
  private resolveArmed = false;
  private resolveDwell = 0;

  private barBg?: Phaser.GameObjects.Rectangle;
  private barFill?: Phaser.GameObjects.Rectangle;
  private barLabel?: Phaser.GameObjects.Text;
  private barGeom = { x: 0, width: 0, fillX: 0, fillW: 0, midY: 0 };

  /** Running score earned THIS B round (reset at each cash-in, once the haul flies to the HUD).
   *  Sum of all round hauls == `total`; it's the amount the HUD's shown number jumps by. */
  private roundScore = 0;
  /** Hovering label above the bar showing the round haul; counts up + swells as balls drain. */
  private haulLabel?: Phaser.GameObjects.Text;
  private haulPop?: Phaser.Tweens.Tween;
  /** The fill fraction (0..1) currently shown: eased toward the logical value each frame, or
   *  swept to full while a live wrap works off an owed level (see pendingWraps). */
  private displayFraction = 0;

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

    // Milestone palette swap: fan the re-style out to every Zone B surface that baked a
    // Theme colour. Fired per tween tick during the cross-fade, so each is a cheap re-apply.
    this.bus.on(GameEvent.ThemeChanged, () => {
      this.walls.restyle();
      this.gates.restyle();
      this.collectors.restyle();
      this.restyleBar();
    });
  }

  update(time: number, delta: number): void {
    this.gates.update(time, delta);
    this.collectors.update(time, delta);
    this.walls.update(time, delta);
    this.animateBar(delta);
    this.updateResolve(delta);
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
    this.resolveArmed = false; // a fresh ball is in flight — not resolving a cash-in
    this.inFlight += 1;
    if (this.inFlight === 1) this.bus.emit(GameEvent.ZoneBBusy);
  }

  private onBallDrained(): void {
    if (this.inFlight === 0) return;
    this.inFlight -= 1;
    if (this.inFlight === 0) {
      if (this.pendingCashIn) {
        // A level was crossed this cycle: DON'T report empty yet. Keeping Zone B "busy"
        // leaves Zone C's trap door locked (no ball injected mid cash-in) while updateResolve
        // waits for the bar to finish its live wraps, then dwells, then pans up.
        this.resolveArmed = true;
        this.resolveDwell = SETTLE_DWELL_MS;
      } else {
        this.bus.emit(GameEvent.ZoneBEmpty);
      }
    }
  }

  private replaceBall(multiplier: number): void {
    this.inFlight += multiplier - 1;
  }

  /**
   * Score a drained ball and wrap the bar LIVE through every target this crossed. Each crossing
   * is a real level-up: SCORE_BAR_FILLED bumps Zone A's level and — synchronously, via its
   * PROGRESSION_CHANGED → setTarget — raises our target, so the next crossing is measured
   * against the new one. Every crossing also queues one display wrap (animateBar sweeps the bar
   * full then empties it), so the fill/empty/fill roll plays out as the balls keep draining —
   * no waiting for the drain to finish.
   */
  private addScore(points: number): void {
    this.total += points;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });

    this.roundScore += points;
    this.pumpHaulLabel();

    this.scoreBar.add(points);
    while (this.scoreBar.crossedTarget()) {
      if (this.cycleLevels >= MAX_LEVELS_PER_CASHIN) {
        // Safety valve: a freak drain out-earned the cap — forfeit the excess fill so the
        // loop terminates and the roll stays a short beat instead of an hours-long wedge.
        this.scoreBar.forfeitOverflow();
        break;
      }
      this.scoreBar.consumeLevel();
      this.cycleLevels += 1;
      this.bus.emit(GameEvent.ScoreBarFilled);
      this.pendingWraps += 1;
      this.pendingCashIn = true;
    }
    this.emitScoreBar();
  }

  /**
   * Once Zone B has drained with a cash-in owed, wait for the bar to finish its live wraps and
   * a short settle beat, then resolve: release Zone B (the ZONE_B_EMPTY deferred in
   * onBallDrained) and fire SCORE_BAR_CASHED_IN, the PhaseDirector's pan-up trigger — so the
   * camera stays on Zone B until the whole roll has played out.
   */
  private updateResolve(delta: number): void {
    if (!this.resolveArmed) return;
    if (this.pendingWraps > 0) return; // let the bar finish wrapping first
    this.resolveDwell -= delta;
    if (this.resolveDwell > 0) return;
    this.resolveArmed = false;
    this.pendingCashIn = false;
    this.cycleLevels = 0;
    this.harvestRound();
    this.bus.emit(GameEvent.ZoneBEmpty);
    this.bus.emit(GameEvent.ScoreBarCashedIn);
  }

  /** Bank this round's haul: fly its number up to the HUD (from the haul label's on-screen spot),
   *  then reset. The HUD adds it to the shown total when the flyer lands, so the top number stays
   *  frozen through the whole B round and jumps once, together with the pan up. */
  private harvestRound(): void {
    const label = this.haulLabel;
    if (this.roundScore > 0) {
      const scrollY = this.scene?.cameras.main.scrollY ?? 0;
      const worldX = label ? label.x : Layout.WIDTH / 2;
      const worldY = label ? label.y : Layout.zoneB.y + Layout.zoneB.height - BAR_HEIGHT - 16;
      // Convert the label's world position to screen space (the overlay/HUD coordinate space),
      // clamped just inside the bottom edge for the rare cash-in that resolves already in phase A
      // (a milestone drain feeds Zone B with scrollY=0, so the world y sits off-screen below).
      this.bus.emit(GameEvent.ScoreHarvested, {
        amount: this.roundScore,
        x: worldX,
        y: Math.min(worldY - scrollY, Layout.HEIGHT - 5),
      });
    }
    this.roundScore = 0;
    this.haulPop?.remove();
    this.haulPop = undefined;
    label?.setVisible(false).setScale(1);
  }

  /** Refresh the hovering haul label from `roundScore`: update the count, reveal it, set its base
   *  scale from the haul magnitude (bigger haul → bigger number, capped), and pop it. */
  private pumpHaulLabel(): void {
    const label = this.haulLabel;
    if (!label) return;
    label.setText(`+${compactValue(this.roundScore)}`).setVisible(true);
    const base = Math.min(HAUL_MAX_SCALE, 1 + Math.log10(this.roundScore + 1) * HAUL_GROWTH);
    this.haulPop?.remove();
    label.setScale(base * 1.18);
    this.haulPop = this.scene?.tweens.add({
      targets: label,
      scale: base,
      duration: HAUL_POP_MS,
      ease: 'Back.easeOut',
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
    // Flush with the bottom of the screen; the label sits centred inside the bar.
    const barTop = y + height - BAR_HEIGHT;
    const midY = barTop + BAR_HEIGHT / 2;
    // The fill sits inside the outlined groove, inset by the stroke on every side.
    const fillX = x + BAR_STROKE_WIDTH;
    const fillW = width - 2 * BAR_STROKE_WIDTH;
    this.barGeom = { x, width, fillX, fillW, midY };

    this.barBg = scene.add
      .rectangle(x + width / 2, midY, width, BAR_HEIGHT, Theme.groove)
      .setDepth(10)
      .setOrigin(0.5);

    this.barFill = scene.add
      .rectangle(fillX, midY, 0, BAR_HEIGHT - 2 * BAR_STROKE_WIDTH, Theme.brass)
      .setDepth(11)
      .setOrigin(0, 0.5);

    this.barLabel = scene.add
      .text(x + width / 2, midY, '', {
        fontFamily: 'monospace',
        fontSize: '11px',
        fontStyle: 'bold',
        color: hexColor(Theme.ink),
      })
      .setOrigin(0.5, 0.5)
      .setDepth(12);

    // Hovering haul label — centred just above the bar, hidden until the round earns its first
    // points. It counts the round's total (see pumpHaulLabel) and, at cash-in, hands its number
    // off to the HUD via SCORE_HARVESTED (the label itself never flies — a world-space object
    // would slide off-screen as the camera pans up).
    this.haulLabel = scene.add
      .text(x + width / 2, barTop - 16, '', {
        fontFamily: 'monospace',
        fontSize: '18px',
        fontStyle: 'bold',
        color: hexColor(Theme.brassBright),
      })
      .setOrigin(0.5, 1)
      .setStroke(hexColor(Theme.ink), 4)
      .setDepth(40)
      .setVisible(false);

    this.restyleBar();
  }

  /** Re-apply the active Theme to the bar's groove/fill/outline/label (milestone swap). */
  private restyleBar(): void {
    this.barBg?.setFillStyle(Theme.groove).setStrokeStyle(BAR_STROKE_WIDTH, Theme.ink);
    this.barFill?.setFillStyle(Theme.brass);
    this.barLabel?.setColor(hexColor(Theme.ink));
    this.haulLabel?.setColor(hexColor(Theme.brassBright)).setStroke(hexColor(Theme.ink), 4);
  }

  /**
   * Drive the shown fill every frame. With owed wraps (a target was just crossed), sweep the
   * bar up to full at a steady pace, then celebrate + snap it to empty and clear one wrap —
   * the visible fill → empty → fill roll, live, as balls keep draining. With none owed, ease
   * toward the current level's fill so the bar and X/Y label glide up smoothly.
   */
  private animateBar(delta: number): void {
    if (!this.barFill) return;
    if (this.pendingWraps > 0) {
      this.displayFraction += delta / WRAP_FILL_MS;
      if (this.displayFraction >= 1) {
        this.celebrateFull();
        this.displayFraction = 0;
        this.pendingWraps -= 1;
      }
    } else {
      const target = Math.min(1, this.scoreBar.getFilled() / this.scoreBar.getTarget());
      if (target < this.displayFraction - 1e-3) {
        this.displayFraction = target; // snap down (defensive; live fill only rises)
      } else {
        const k = 1 - Math.pow(1 - FILL_LERP, delta / 16.67);
        this.displayFraction += (target - this.displayFraction) * k;
        if (target - this.displayFraction < 0.005) this.displayFraction = target;
      }
    }
    this.renderBar();
  }

  /** Paint the bar + label from `displayFraction`. The label counts the current level's
   *  progress (fraction × the live target), so it resets to 0 and climbs again on each wrap.
   *  Cheap; safe to call every frame. */
  private renderBar(): void {
    if (!this.barFill || !this.barLabel) return;
    const target = this.scoreBar.getTarget();
    this.barFill.width = this.barGeom.fillW * this.displayFraction;
    this.barLabel.setText(
      `${compactValue(Math.round(this.displayFraction * target))} / ${compactValue(target)}`,
    );
  }

  private updateBarVisual(): void {
    // The per-frame animator owns the fill width + label; this just refreshes on discrete
    // events (initial build, target change) so the bar isn't blank until the next tick.
    this.renderBar();
  }

  /** A full bar throbs and throws off a rising brass sparkle — the "you filled it" beat.
   *  Fires once per bar-full: the live first fill and every rolled level. */
  private celebrateFull(): void {
    const scene = this.scene;
    if (!scene) return;

    Sfx.goal();

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
