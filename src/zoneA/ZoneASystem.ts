import Phaser from 'phaser';
import { GameEvent, tierToValue, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { hexColor } from '../core/Materials';
import { Sfx } from '../core/Sfx';
import { Theme } from '../core/Theme';
import { getStage, type ProgressionStage } from '../core/Progression';
import { AimController } from './AimController';
import { ArenaView } from './ArenaView';
import { neutralGrowth } from './ballMath';
import { BallFactory } from './BallFactory';
import { BallQueue } from './BallQueue';
import { Board } from './Board';
import { DeathLine } from './DeathLine';

/** Levels between arena zoom-out milestones (50, 100, 150, …). The draw-window *shift-ups* in
 *  `progression.json` MUST land on these same levels — the milestone reads the new window's floor
 *  (`stage.ballWindow[0]`) as its blacklist threshold, so a window that steps between milestones
 *  would desync the scale/blacklist from the spawn pool. */
const MILESTONE_EVERY = 50;

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
      this.cashInPending = true;
      this.internalLevel += 1;
      const stage = getStage(this.internalLevel);
      this.applyStage(stage, queue);
      // applyStage may have re-seeded the queue's current/next (stages with bufferBalls),
      // so re-sync the in-hand ball + Next preview — otherwise the player aims one tier
      // and drops another. The milestone path below re-rolls and refreshes again on top.
      this.aim?.refreshQueue();
      this.animateBufferTo(stage.bufferCapacity);
      this.bus.emit(GameEvent.ProgressionChanged, {
        level: this.internalLevel,
        minTier: stage.ballWindow[0],
        maxTier: stage.ballWindow[1],
        bufferCapacity: stage.bufferCapacity,
        scoreBarTarget: stage.scoreBarTarget,
      });
      // Every MILESTONE_EVERY levels the draw window jumps up, blacklisting the lowest
      // tiers, and the arena zooms out (input frozen during the tween) by the neutral
      // ball-growth match × the stage's authored tightness — so apparent ball size holds
      // constant at tightness 1 and the arena-to-ball headroom is exactly the tightness
      // rhythm authored in progression.json. Past the last authored window shift the stage
      // stops moving, so milestones become plain levels (no growth — the tail self-heals).
      // Otherwise just lift the buffer-empty drop lock as before (guarded: the ticked
      // buffer refill above may not have delivered its first slot yet).
      const prev = getStage(this.internalLevel - 1);
      const shifted =
        stage.ballWindow[0] !== prev.ballWindow[0] || stage.ballWindow[1] !== prev.ballWindow[1];
      if (this.internalLevel % MILESTONE_EVERY === 0 && shifted) {
        const factor =
          neutralGrowth(prev.ballWindow[1], stage.ballWindow[1]) * (stage.tightness ?? 1);
        this.beginMilestoneZoom(factor, stage.ballWindow[0]);
      } else {
        this.maybeUnlockDrop();
      }
    });

    this.bus.on(GameEvent.ZoneBBusy,  () => { this.zoneBEmpty = false; });
    this.bus.on(GameEvent.ZoneBEmpty, () => {
      this.zoneBEmpty = true;
      this.checkLoss();
    });
  }

  update(_time: number, delta: number): void {
    if (this.over) return;
    this.board?.update(delta);
  }

  destroy(): void {
    this.bufferTickTimer?.remove();
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
    this.aim?.setFrozen(true);
    // The window already shifted up (applyStage); re-roll the in-hand + Next pieces off any
    // now-blacklisted tiers and refresh the queue row so it shows valid tiers when input returns.
    this.queue?.reroll();
    this.aim?.refreshQueue();
    this.bus.emit(GameEvent.ArenaZoom, { active: true });
    this.arena?.grow(factor, () => {
      this.drainBlacklisted(newMinTier, () => {
        this.aim?.setFrozen(false);
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
   * score bar read as a reward. Drop unlocks the moment the first tick lands, via
   * maybeUnlockDrop(). If the buffer is already at or above the new capacity (shouldn't
   * normally happen, since capacity is non-decreasing across stages), applies it in one
   * step instead.
   */
  private animateBufferTo(newCapacity: number): void {
    this.bufferTickTimer?.remove();
    if (newCapacity <= this.ballBuffer) {
      this.ballBuffer = newCapacity;
      this.emitBuffer();
      this.maybeUnlockDrop();
      return;
    }
    const ticksNeeded = newCapacity - this.ballBuffer;
    let ticksDone = 0;
    this.bufferTickTimer = this.scene?.time.addEvent({
      delay: BUFFER_TICK_MS,
      repeat: ticksNeeded - 1,
      callback: () => {
        this.ballBuffer += 1;
        this.emitBuffer();
        Sfx.bufferTick(ticksDone);
        ticksDone += 1;
        this.maybeUnlockDrop();
      },
    });
  }

  /** Unlock dropping only once the buffer actually has a slot — safe to call from both the
   *  ticked refill and the milestone-zoom completion regardless of which finishes first. */
  private maybeUnlockDrop(): void {
    if (this.ballBuffer > 0) {
      this.aim?.setDropLocked(false);
      this.cashInPending = false;
    }
  }

  private onBallDropped(): void {
    if (this.ballBuffer <= 0) return;
    this.ballBuffer -= 1;
    this.emitBuffer();
    if (this.ballBuffer === 0) {
      this.aim?.setDropLocked(true);
      this.checkLoss();
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

    scene.add
      .rectangle(cx, cy, Layout.WIDTH, Layout.HEIGHT, Theme.scrim, 0.85)
      .setDepth(2000);

    scene.add
      .text(cx, cy - 80, 'GAME OVER', {
        fontFamily: 'monospace',
        fontSize: '40px',
        color: hexColor(Theme.brassBright),
        fontStyle: 'bold',
        align: 'center',
      })
      .setOrigin(0.5)
      .setDepth(2001);

    scene.add
      .text(cx, cy - 20, `Score: ${this.score}`, {
        fontFamily: 'monospace',
        fontSize: '24px',
        color: hexColor(Theme.cream),
        align: 'center',
      })
      .setOrigin(0.5)
      .setDepth(2001);

    this.drawRestartButton(scene, cx, cy + 60);
  }

  private drawRestartButton(scene: Phaser.Scene, cx: number, cy: number): void {
    const width = 200;
    const height = 56;
    const button = scene.add
      .rectangle(cx, cy, width, height, Theme.pineDark)
      .setStrokeStyle(2, Theme.brassBright)
      .setDepth(2001)
      .setInteractive({ useHandCursor: true });

    scene.add
      .text(cx, cy, 'RESTART', {
        fontFamily: 'monospace',
        fontSize: '22px',
        color: hexColor(Theme.cream),
        fontStyle: 'bold',
      })
      .setOrigin(0.5)
      .setDepth(2002);

    button.on('pointerup', () => scene.scene.restart());
  }
}
