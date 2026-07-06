import Phaser from 'phaser';
import { GameEvent, tierToValue, type GamePhase, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { compactValue, hexColor } from '../core/Materials';
import { Sfx } from '../core/Sfx';
import { Theme } from '../core/Theme';
import { getStage, MILESTONE_EVERY, type ProgressionStage } from '../core/Progression';
import { AimController } from './AimController';
import { ArenaView } from './ArenaView';
import { neutralGrowth } from './ballMath';
import { BallFactory } from './BallFactory';
import { BallQueue } from './BallQueue';
import { Board } from './Board';
import { DeathLine } from './DeathLine';
import { advanceSettleGate, initialSettleGate, type SettleGateState } from './settleGate';

/** Blacklist-drain: how long each snapshot slides from Zone A down into Zone B (ms). */
const DRAIN_MS = 280;
/** On-screen size a drained snapshot lands at — matches Zone B's fixed ball (radius 10). */
const ZONE_B_BALL_PX = 20;
/** Keep drained columns a touch inside the Zone B side walls. */
const DRAIN_MARGIN = 12;

/**
 * A stalemate must persist this long before the run actually ends. Balls hand off between
 * zones through transient empty states (a Zone C suck is mid-tween, a merge briefly empties
 * the board), so we confirm the stalemate after a short grace — longer than Zone C's suck —
 * and only end if it still holds. A real stalemate persists; a transient resolves.
 */
const STALEMATE_GRACE_MS = 250;

/** Ball-buffer refill cadence during a score-bar cash-in: one slot every this many ms. */
const BUFFER_TICK_MS = 130;

/** Cash-in particle: flight time from the score bar up to the queue-row count (ms). Each
 *  refilled buffer slot only lands (count pop + blip) when its particle arrives. */
const PARTICLE_FLIGHT_MS = 500;
/** Cash-in particle: horizontal jitter on the flight path's bezier control point (px). */
const PARTICLE_BOW_JITTER = 60;
/** Cash-in particle: trail motes fade out over this long (ms). */
const TRAIL_FADE_MS = 220;

/** One in-flight cash-in particle — tracked so a re-triggered refill or destroy() can
 *  settle or discard it deterministically. */
interface BufferParticle {
  tween: Phaser.Tweens.Tween;
  dot: Phaser.GameObjects.Arc;
  index: number;
}

export class ZoneASystem implements GameSystem {
  private scene?: Phaser.Scene;
  private arena?: ArenaView;
  private board?: Board;
  private aim?: AimController;
  private deathLine?: DeathLine;
  private queue?: BallQueue;
  private over = false;

  private internalLevel = 1;
  private ballBuffer = 0;
  private bufferTickTimer?: Phaser.Time.TimerEvent;
  private readonly bufferParticles = new Set<BufferParticle>();
  private zoneBEmpty = true;
  /** True whenever the score bar is holding full mid cash-in (dwell/wait-for-empty/drain) —
   *  derived from SCORE_BAR_CHANGED's filled/target, which stays >= target for the entire
   *  cash-in window regardless of how long it takes. A buffer refill is guaranteed once
   *  this clears, so it must not read as a stalemate in the meantime. */
  private scoreBarCashingIn = false;
  /** True from the instant ScoreBarFilled arrives until the ENTIRE ticked buffer refill has
   *  landed (the last tick, or immediately in the no-animation skip case) — not merely once
   *  the buffer becomes non-zero. Drop unlocks at the first tick (see maybeUnlockDrop), but a
   *  fast player can spend that first slot before the remaining ticks land, so isStalemate()
   *  must keep reading false for the refill's entire duration, not just its first slot. */
  private cashInPending = false;
  private score = 0;
  private lossPending = false;

  /** Current gameplay phase (PhaseChanged event). Aiming is live only in 'A'. */
  private phase: GamePhase = 'A';
  /** True while the milestone zoom-out tween runs — one of the two freeze sources. */
  private milestoneZoomActive = false;
  /** Armed when the buffer empties: the settle gate that fires ZONE_A_DEPLETED. */
  private settleGate?: SettleGateState;
  /** A cash-in that arrived outside the 'A' phase — its visible reward sequence
   *  (milestone zoom / buffer refill) is deferred until the pan lands back in A. */
  private pendingCashIn?: { stage: ProgressionStage; prev: ProgressionStage };

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;

    const arena = new ArenaView(scene);
    arena.create();
    this.arena = arena;

    const factory = new BallFactory(scene, (img) => arena.claim(img));
    this.deathLine = new DeathLine(scene, arena);

    const queue = new BallQueue();
    this.queue = queue;
    const initialStage = getStage(this.internalLevel);
    this.applyStage(initialStage, queue);
    this.ballBuffer = initialStage.bufferCapacity;

    const board = new Board(
      scene,
      factory,
      arena,
      () => this.handleGameOver(),
      () => this.checkLoss(),
      (near) => this.deathLine?.setDanger(near),
    );
    const aim = new AimController(scene, factory, arena, (x, tier) => {
      board.spawnDropped(x, tier);
      this.onBallDropped();
      Sfx.drop();
    }, queue);

    this.board = board;
    this.aim = aim;

    this.emitBuffer();

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => { this.score = total; });

    this.bus.on(GameEvent.ScoreBarChanged, ({ filled, target }) => {
      this.scoreBarCashingIn = filled >= target;
    });

    this.bus.on(GameEvent.ScoreBarFilled, () => {
      // Immediate half (any phase): advance the stage and broadcast it — Zone B reads the
      // new scoreBarTarget synchronously for its cascade math, so this ordering is frozen.
      this.cashInPending = true;
      this.internalLevel += 1;
      const stage = getStage(this.internalLevel);
      this.applyStage(stage, queue);
      // applyStage may have re-seeded the queue's current/next (stages with bufferBalls),
      // so re-sync the in-hand ball + Next preview — otherwise the player aims one tier
      // and drops another. The milestone path below re-rolls and refreshes again on top.
      this.aim?.refreshQueue();
      this.bus.emit(GameEvent.ProgressionChanged, {
        level: this.internalLevel,
        minTier: stage.ballWindow[0],
        maxTier: stage.ballWindow[1],
        bufferCapacity: stage.bufferCapacity,
        scoreBarTarget: stage.scoreBarTarget,
      });
      // Deferred half: the visible reward (milestone zoom / drain / ticked buffer refill)
      // runs only in the 'A' phase. A cash-in normally arrives in the 'B' phase and the
      // PhaseDirector pans back up on this same event — the sequence then runs when
      // PHASE_CHANGED('A') lands, so the milestone zoom never overlaps the pan. A cash-in
      // while already in 'A' (e.g. milestone-drain balls filled the bar) runs immediately.
      const prev = getStage(this.internalLevel - 1);
      if (this.phase === 'A') {
        this.runCashInSequence(stage, prev);
      } else {
        this.pendingCashIn = { stage, prev };
      }
    });

    this.bus.on(GameEvent.PhaseChanged, ({ phase }) => {
      this.phase = phase;
      this.applyFreeze();
      if (phase === 'A' && this.pendingCashIn) {
        const { stage, prev } = this.pendingCashIn;
        this.pendingCashIn = undefined;
        this.runCashInSequence(stage, prev);
      }
    });

    this.bus.on(GameEvent.ZoneBBusy,  () => { this.zoneBEmpty = false; });
    this.bus.on(GameEvent.ZoneBEmpty, () => {
      this.zoneBEmpty = true;
      this.checkLoss();
    });
  }

  /**
   * The visible reward sequence for one score-bar cash-in. Every MILESTONE_EVERY levels the
   * draw window jumps up, blacklisting the lowest tiers, and the arena zooms out (input
   * frozen during the tween) by the neutral ball-growth match × the stage's authored
   * tightness — so apparent ball size holds constant at tightness 1 and the arena-to-ball
   * headroom is exactly the tightness rhythm authored in progression.json. Past the last
   * authored window shift the stage stops moving, so milestones become plain levels (no
   * growth — the tail self-heals). Otherwise just lift the buffer-empty drop lock (guarded:
   * the ticked buffer refill may not have delivered its first slot yet).
   */
  private runCashInSequence(stage: ProgressionStage, prev: ProgressionStage): void {
    this.animateBufferTo(stage.bufferCapacity);
    const shifted =
      stage.ballWindow[0] !== prev.ballWindow[0] || stage.ballWindow[1] !== prev.ballWindow[1];
    if (this.internalLevel % MILESTONE_EVERY === 0 && shifted) {
      const factor =
        neutralGrowth(prev.ballWindow[1], stage.ballWindow[1]) * (stage.tightness ?? 1);
      this.beginMilestoneZoom(factor, stage.ballWindow[0]);
    } else {
      this.maybeUnlockDrop();
    }
  }

  update(_time: number, delta: number): void {
    if (this.over) return;
    this.board?.update(delta);
    this.advanceDepletionGate(delta);
  }

  /**
   * While the buffer sits at 0, wait for the last drop to settle (with a hard timeout so a
   * trembling board can't wedge the flow), then hand control to the Zone-B phase via
   * ZONE_A_DEPLETED — unless the run is over, or the board+Zone B are empty (that shape is
   * the stalemate, already ending the run through checkLoss). Refills disarm the gate.
   */
  private advanceDepletionGate(delta: number): void {
    if (!this.settleGate || !this.board) return;
    const step = advanceSettleGate(this.settleGate, delta, this.board.isSettled());
    this.settleGate = step.state;
    if (!step.fire) return;
    this.settleGate = undefined;
    if (this.over || this.phase !== 'A') return;
    // A ticked refill is still landing slots (the player spent the first one back to 0):
    // more balls are seconds away, so don't pan — maybeUnlockDrop re-opens play shortly.
    if (this.cashInPending) return;
    if (this.board.getBallCount() === 0 && this.zoneBEmpty) return; // stalemate path owns this
    this.bus.emit(GameEvent.ZoneADepleted);
  }

  /** One sink for the reversible aim freeze, composing its two sources: the milestone
   *  zoom-out and the game not being in the 'A' phase. */
  private applyFreeze(): void {
    this.aim?.setFrozen(this.milestoneZoomActive || this.phase !== 'A');
  }

  destroy(): void {
    this.bufferTickTimer?.remove();
    this.discardBufferParticles();
    this.aim?.destroy();
    this.board?.destroy();
    this.deathLine?.destroy();
    this.arena?.destroy();
  }

  /**
   * Run a milestone zoom-out: freeze Zone A input and lock Zone C (via the ArenaZoom event),
   * grow the arena + tween the camera, then — once it settles — drain the freshly-blacklisted
   * tiers into Zone B and only restore input/Zone C when that finishes. The death line and aim
   * ball are re-seated to the grown arena up front so they animate with the camera.
   */
  private beginMilestoneZoom(factor: number, newMinTier: number): void {
    this.milestoneZoomActive = true;
    this.applyFreeze();
    // The window already shifted up (applyStage); re-roll the in-hand + Next pieces off any
    // now-blacklisted tiers and refresh the queue row so it shows valid tiers when input returns.
    this.queue?.reroll();
    this.aim?.refreshQueue();
    this.bus.emit(GameEvent.ArenaZoom, { active: true });
    this.arena?.grow(factor, () => {
      this.drainBlacklisted(newMinTier, () => {
        this.milestoneZoomActive = false;
        this.applyFreeze();
        this.maybeUnlockDrop();
        this.bus.emit(GameEvent.ArenaZoom, { active: false });
      });
    });
    this.deathLine?.reposition();
    this.aim?.syncToArena();
  }

  /**
   * Drain every board ball below the new draw-window floor into Zone B in one synchronized
   * slide. Mirrors Zone C's handoff: signal Zone B busy up front (so the emptying board can't
   * read as a stalemate), animate a throwaway snapshot of each ball from where it appears on
   * screen down to the Zone B entry, then emit BALL_DROPPED for it when its slide lands.
   */
  private drainBlacklisted(minTier: number, onDone: () => void): void {
    const scene = this.scene;
    const arena = this.arena;
    const board = this.board;
    if (!scene || !arena || !board) { onDone(); return; }

    const drained = board.takeBallsBelow(minTier);
    if (drained.length === 0) { onDone(); return; }

    // Up-front busy so the (now emptier) board can't be read as a stalemate mid-drain.
    this.bus.emit(GameEvent.ZoneBBusy);
    Sfx.transition();

    const minX = Layout.zoneB.x + DRAIN_MARGIN;
    const maxX = Layout.zoneB.x + Layout.zoneB.width - DRAIN_MARGIN;
    let remaining = drained.length;

    for (const d of drained) {
      const start = arena.screenPoint(d.x, d.y);
      const size = d.worldDiameter * arena.viewScale;
      const targetX = Phaser.Math.Clamp(start.x, minX, maxX);
      const tier = d.tier;

      const sprite = scene.add.image(start.x, start.y, d.texKey).setDepth(800);
      sprite.setDisplaySize(size, size);
      arena.ignoreOnArenaCamera(sprite); // it leaves the arena into Zone B — main camera only

      scene.tweens.add({
        targets: sprite,
        x: targetX,
        y: Layout.zoneBEntry.y,
        displayWidth: ZONE_B_BALL_PX,
        displayHeight: ZONE_B_BALL_PX,
        duration: DRAIN_MS,
        ease: 'Cubic.easeIn',
        onComplete: () => {
          this.bus.emit(GameEvent.BallDropped, { value: tierToValue(tier), tier, x: targetX });
          sprite.destroy();
          if (--remaining === 0) onDone();
        },
      });
    }
  }

  private applyStage(stage: ProgressionStage, queue: BallQueue): void {
    queue.setWindow(stage.ballWindow[0], stage.ballWindow[1]);
    if (stage.bufferBalls) queue.seed(stage.bufferBalls);
  }

  /**
   * Refill the ball buffer to `newCapacity`, one slot at a time, so the HUD's queue-row
   * count visibly ticks up instead of instantly jumping — this is what makes filling the
   * score bar read as a reward. Each tick launches a brass particle from the (just-drained)
   * score bar at the bottom of Zone B; the slot only lands — count pop + blip — when the
   * particle arrives at the queue-row count, so the refill reads as the bar's energy flying
   * up into the ball supply. Drop unlocks the moment the first slot lands, via
   * maybeUnlockDrop(). If the buffer is already at or above the new capacity (shouldn't
   * normally happen, since capacity is non-decreasing across stages), applies it in one
   * step instead.
   */
  private animateBufferTo(newCapacity: number): void {
    this.bufferTickTimer?.remove();
    // A back-to-back cash-in (overflow cascade) can restart the refill while particles from
    // the previous one are still in flight. Settle them instantly — their slots are already
    // spoken for — so the ticksNeeded arithmetic below starts from an honest ballBuffer.
    this.settleBufferParticles();
    if (newCapacity <= this.ballBuffer) {
      this.ballBuffer = newCapacity;
      this.emitBuffer();
      this.maybeUnlockDrop();
      return;
    }
    const ticksNeeded = newCapacity - this.ballBuffer;
    let launched = 0;
    this.bufferTickTimer = this.scene?.time.addEvent({
      delay: BUFFER_TICK_MS,
      repeat: ticksNeeded - 1,
      callback: () => this.launchBufferParticle(launched++),
    });
  }

  /**
   * Fly one brass mote from a random point along the score bar (bottom edge of Zone B) up
   * to the queue-row balls-left count, bowing sideways along a quadratic bezier and shedding
   * a short fading trail. The buffer slot lands on arrival. Screen-space chrome: kept off
   * the arena camera so a milestone zoom can't shrink or double-draw it.
   */
  private launchBufferParticle(index: number): void {
    const scene = this.scene;
    const anchor = this.aim?.countAnchor();
    if (!scene || !anchor) { this.landBufferSlot(index); return; }

    // Launch from the bottom of the SCREEN, not the world: this runs in the A-phase
    // (scroll 0), where Zone B's band bottom (world 1238) is off-screen below y=844.
    const start = {
      x: Layout.zoneB.x + Math.random() * Layout.zoneB.width,
      y: Layout.HEIGHT - 5,
    };
    const control = new Phaser.Math.Vector2(
      (start.x + anchor.x) / 2 + Phaser.Math.FloatBetween(-PARTICLE_BOW_JITTER, PARTICLE_BOW_JITTER),
      Phaser.Math.Linear(start.y, anchor.y, 0.45),
    );
    const curve = new Phaser.Curves.QuadraticBezier(
      new Phaser.Math.Vector2(start.x, start.y),
      control,
      new Phaser.Math.Vector2(anchor.x, anchor.y),
    );

    const dot = scene.add
      .circle(start.x, start.y, 4, Theme.brassBright)
      .setStrokeStyle(2, Theme.brass, 0.6)
      .setDepth(950);
    this.arena?.ignoreOnArenaCamera(dot);

    const proxy = { t: 0 };
    let frame = 0;
    const particle: BufferParticle = { dot, index, tween: undefined as unknown as Phaser.Tweens.Tween };
    particle.tween = scene.tweens.add({
      targets: proxy,
      t: 1,
      duration: PARTICLE_FLIGHT_MS,
      ease: 'Sine.easeInOut',
      onUpdate: () => {
        const p = curve.getPoint(proxy.t);
        dot.setPosition(p.x, p.y);
        if (frame++ % 3 === 0) this.shedTrailMote(scene, p.x, p.y);
      },
      onComplete: () => {
        this.bufferParticles.delete(particle);
        dot.destroy();
        this.landBufferSlot(index);
      },
    });
    this.bufferParticles.add(particle);
  }

  /** A tiny fading mote left behind by an in-flight cash-in particle. Self-destroys. */
  private shedTrailMote(scene: Phaser.Scene, x: number, y: number): void {
    const mote = scene.add.circle(x, y, 2, Theme.brassBright, 0.55).setDepth(949);
    this.arena?.ignoreOnArenaCamera(mote);
    scene.tweens.add({
      targets: mote,
      alpha: 0,
      scale: 0.3,
      duration: TRAIL_FADE_MS,
      onComplete: () => mote.destroy(),
    });
  }

  /** One refilled buffer slot arrives: count up, pop the queue-row number, blip. */
  private landBufferSlot(index: number): void {
    this.ballBuffer += 1;
    this.emitBuffer();
    Sfx.bufferTick(index);
    this.maybeUnlockDrop();
  }

  /** Land every in-flight particle's slot immediately (no per-slot blip spam) and clear
   *  the visuals — used when a new refill starts while the previous one is still flying. */
  private settleBufferParticles(): void {
    if (this.bufferParticles.size === 0) return;
    for (const p of this.bufferParticles) {
      p.tween.remove();
      p.dot.destroy();
      this.ballBuffer += 1;
    }
    this.bufferParticles.clear();
    this.emitBuffer();
    this.maybeUnlockDrop();
  }

  /** Teardown-only: kill particle tweens and visuals without landing slots or emitting. */
  private discardBufferParticles(): void {
    for (const p of this.bufferParticles) {
      p.tween.remove();
      p.dot.destroy();
    }
    this.bufferParticles.clear();
  }

  /** Unlock dropping only once the buffer actually has a slot — safe to call from both the
   *  ticked refill and the milestone-zoom completion regardless of which finishes first. */
  private maybeUnlockDrop(): void {
    if (this.ballBuffer > 0) {
      this.aim?.setDropLocked(false);
      this.cashInPending = false;
      this.settleGate = undefined; // a refill landed — the buffer is no longer depleted
    }
  }

  private onBallDropped(): void {
    if (this.ballBuffer <= 0) return;
    this.ballBuffer -= 1;
    this.emitBuffer();
    if (this.ballBuffer === 0) {
      this.aim?.setDropLocked(true);
      this.checkLoss();
      // Last ball released: arm the settle gate — once the board comes to rest (or the
      // timeout hits) the A→B phase pan begins. A refill in the meantime disarms it.
      this.settleGate = initialSettleGate();
    }
  }

  /** True when the player has no path forward: no drops left, Zone A empty, Zone B idle. */
  private isStalemate(): boolean {
    return (
      this.ballBuffer === 0 &&
      this.zoneBEmpty &&
      (this.board?.getBallCount() ?? 0) === 0 &&
      !this.scoreBarCashingIn &&
      !this.cashInPending
    );
  }

  /**
   * End the run only on a settled stalemate. Balls hand off between zones through transient
   * empty states, so we confirm after a short grace and end only if it still holds.
   */
  private checkLoss(): void {
    if (this.over || this.lossPending || !this.isStalemate()) return;
    this.lossPending = true;
    this.scene?.time.delayedCall(STALEMATE_GRACE_MS, () => {
      this.lossPending = false;
      if (this.isStalemate()) this.handleGameOver();
    });
  }

  private emitBuffer(): void {
    this.bus.emit(GameEvent.BallBufferChanged, { count: this.ballBuffer });
    this.aim?.setBallsLeft(this.ballBuffer);
  }

  private handleGameOver(): void {
    if (this.over) return;
    this.over = true;
    this.aim?.disable();
    this.deathLine?.setDanger(false);
    this.scene?.matter.world.pause();
    this.drawGameOverOverlay();
  }

  private drawGameOverOverlay(): void {
    const scene = this.scene;
    if (!scene) return;
    const cx = Layout.WIDTH / 2;
    const cy = Layout.HEIGHT / 2;

    // Pinned + arena-ignored: game over can fire in either phase framing (main camera may
    // be scrolled down), so everything is scrollFactor(0) to cover the actual screen, and
    // kept off the arena camera so the zoomed band can't double-draw it.
    const pin = <T extends Phaser.GameObjects.GameObject & { setScrollFactor(v: number): T }>(
      obj: T,
    ): T => {
      obj.setScrollFactor(0);
      this.arena?.ignoreOnArenaCamera(obj);
      return obj;
    };

    pin(
      scene.add
        .rectangle(cx, cy, Layout.WIDTH, Layout.HEIGHT, Theme.scrim, 0.85)
        .setDepth(2000),
    );

    pin(
      scene.add
        .text(cx, cy - 80, 'GAME OVER', {
          fontFamily: 'monospace',
          fontSize: '40px',
          color: hexColor(Theme.brassBright),
          fontStyle: 'bold',
          align: 'center',
        })
        .setOrigin(0.5)
        .setDepth(2001),
    );

    pin(
      scene.add
        .text(cx, cy - 20, `Score: ${compactValue(this.score)}`, {
          fontFamily: 'monospace',
          fontSize: '24px',
          color: hexColor(Theme.cream),
          align: 'center',
        })
        .setOrigin(0.5)
        .setDepth(2001),
    );

    this.drawRestartButton(scene, cx, cy + 60);
  }

  private drawRestartButton(scene: Phaser.Scene, cx: number, cy: number): void {
    const width = 200;
    const height = 56;
    const button = scene.add
      .rectangle(cx, cy, width, height, Theme.pineDark)
      .setStrokeStyle(2, Theme.brassBright)
      .setScrollFactor(0)
      .setDepth(2001)
      .setInteractive({ useHandCursor: true });

    const label = scene.add
      .text(cx, cy, 'RESTART', {
        fontFamily: 'monospace',
        fontSize: '22px',
        color: hexColor(Theme.cream),
        fontStyle: 'bold',
      })
      .setOrigin(0.5)
      .setScrollFactor(0)
      .setDepth(2002);
    this.arena?.ignoreOnArenaCamera([button, label]);

    button.on('pointerup', () => scene.scene.restart());
  }
}
