import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { Sfx } from '../core/Sfx';
import { getStage, type ProgressionStage } from '../core/Progression';
import { AimController } from './AimController';
import { BallFactory } from './BallFactory';
import { BallQueue } from './BallQueue';
import { Board } from './Board';
import { DeathLine } from './DeathLine';

/**
 * A stalemate must persist this long before the run actually ends. Balls hand off between
 * zones through transient empty states (a Zone C suck is mid-tween, a merge briefly empties
 * the board), so we confirm the stalemate after a short grace — longer than Zone C's suck —
 * and only end if it still holds. A real stalemate persists; a transient resolves.
 */
const STALEMATE_GRACE_MS = 250;

export class ZoneASystem implements GameSystem {
  private scene?: Phaser.Scene;
  private board?: Board;
  private aim?: AimController;
  private deathLine?: DeathLine;
  private over = false;

  private internalLevel = 1;
  private ballBuffer = 0;
  private zoneBEmpty = true;
  private score = 0;
  private lossPending = false;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.deathLine = new DeathLine(scene);
    const factory = new BallFactory(scene);

    const queue = new BallQueue();
    const initialStage = getStage(this.internalLevel);
    this.applyStage(initialStage, queue);

    const board = new Board(
      scene,
      factory,
      () => this.handleGameOver(),
      () => this.checkLoss(),
      (near) => this.deathLine?.setDanger(near),
    );
    const aim = new AimController(scene, factory, (x, tier) => {
      board.spawnDropped(x, tier);
      this.onBallDropped();
      Sfx.drop();
    }, queue);

    this.board = board;
    this.aim = aim;

    this.emitBuffer();

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => { this.score = total; });

    this.bus.on(GameEvent.ScoreBarFilled, () => {
      this.internalLevel += 1;
      const stage = getStage(this.internalLevel);
      this.applyStage(stage, queue);
      this.aim?.setDropLocked(false);
      this.emitBuffer();
      this.bus.emit(GameEvent.ProgressionChanged, {
        level: this.internalLevel,
        minTier: stage.ballWindow[0],
        maxTier: stage.ballWindow[1],
        bufferCapacity: stage.bufferCapacity,
        scoreBarTarget: stage.scoreBarTarget,
      });
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
    this.aim?.destroy();
    this.board?.destroy();
    this.deathLine?.destroy();
  }

  private applyStage(stage: ProgressionStage, queue: BallQueue): void {
    queue.setWindow(stage.ballWindow[0], stage.ballWindow[1]);
    if (stage.bufferBalls) queue.seed(stage.bufferBalls);
    this.ballBuffer = stage.bufferCapacity;
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
    return this.ballBuffer === 0 && this.zoneBEmpty && (this.board?.getBallCount() ?? 0) === 0;
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
      .rectangle(cx, cy, Layout.WIDTH, Layout.HEIGHT, 0x0b0d12, 0.82)
      .setDepth(2000);

    scene.add
      .text(cx, cy - 80, 'GAME OVER', {
        fontFamily: 'monospace',
        fontSize: '40px',
        color: '#ff6d6d',
        fontStyle: 'bold',
        align: 'center',
      })
      .setOrigin(0.5)
      .setDepth(2001);

    scene.add
      .text(cx, cy - 20, `Score: ${this.score}`, {
        fontFamily: 'monospace',
        fontSize: '24px',
        color: '#ffffff',
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
      .rectangle(cx, cy, width, height, 0x2a3346)
      .setStrokeStyle(2, 0x4cc9f0)
      .setDepth(2001)
      .setInteractive({ useHandCursor: true });

    scene.add
      .text(cx, cy, 'RESTART', {
        fontFamily: 'monospace',
        fontSize: '22px',
        color: '#ffffff',
        fontStyle: 'bold',
      })
      .setOrigin(0.5)
      .setDepth(2002);

    button.on('pointerup', () => scene.scene.restart());
  }
}
