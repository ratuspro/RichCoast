import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { getStage, type ProgressionStage } from '../core/Progression';
import { AimController } from './AimController';
import { BallFactory } from './BallFactory';
import { BallQueue } from './BallQueue';
import { Board } from './Board';

export class ZoneASystem implements GameSystem {
  private scene?: Phaser.Scene;
  private board?: Board;
  private aim?: AimController;
  private over = false;

  private internalLevel = 1;
  private ballBuffer = 0;
  private zoneBEmpty = true;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    const factory = new BallFactory(scene);

    const queue = new BallQueue();
    const initialStage = getStage(this.internalLevel);
    this.applyStage(initialStage, queue);

    const board = new Board(
      scene,
      factory,
      () => this.handleGameOver(),
      () => this.checkLoss(),
    );
    const aim = new AimController(scene, factory, (x, tier) => {
      board.spawnDropped(x, tier);
      this.onBallDropped();
    }, queue);

    this.board = board;
    this.aim = aim;

    this.emitBuffer();

    this.bus.on(GameEvent.ScoreBarFilled, () => {
      this.internalLevel += 1;
      const stage = getStage(this.internalLevel);
      this.applyStage(stage, queue);
      this.ballBuffer = stage.bufferCapacity;
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

  private checkLoss(): void {
    if (this.ballBuffer === 0 && this.zoneBEmpty && (this.board?.getBallCount() ?? 0) === 0) {
      this.handleGameOver();
    }
  }

  private emitBuffer(): void {
    this.bus.emit(GameEvent.BallBufferChanged, { count: this.ballBuffer });
  }

  private handleGameOver(): void {
    if (this.over) return;
    this.over = true;
    this.aim?.disable();
    this.drawGameOverOverlay();
  }

  private drawGameOverOverlay(): void {
    const scene = this.scene;
    if (!scene) return;
    const r = Layout.zoneA;
    const cx = r.x + r.width / 2;
    const cy = r.y + r.height / 2;
    scene.add.rectangle(cx, cy, r.width, r.height, 0x0b0d12, 0.66).setDepth(900);
    scene.add
      .text(cx, cy, 'GAME OVER', {
        fontFamily: 'monospace',
        fontSize: '40px',
        color: '#ff6d6d',
        fontStyle: 'bold',
        align: 'center',
      })
      .setOrigin(0.5)
      .setDepth(901);
  }
}
