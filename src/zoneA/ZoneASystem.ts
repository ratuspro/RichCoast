import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { AimController } from './AimController';
import { BallFactory } from './BallFactory';
import { Board } from './Board';

const BALL_BUFFER_INITIAL = 4;

/**
 * Zone A — the Suika-style merge board (Dev 1).
 *
 * Owns the ball buffer: a finite count of drops the player has remaining.
 * Dropping a ball costs 1. Zone B's score bar filling refills the buffer to
 * its full capacity. When the buffer hits 0 the player can still aim and wait
 * for Zone B to save them; if Zone B empties while the buffer is still 0 the
 * run ends.
 */
export class ZoneASystem implements GameSystem {
  private scene?: Phaser.Scene;
  private board?: Board;
  private aim?: AimController;
  private over = false;

  private ballBuffer = BALL_BUFFER_INITIAL;
  private zoneBEmpty = true; // starts true — nothing is in flight yet

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    const factory = new BallFactory(scene);
    const board = new Board(
      scene,
      factory,
      () => this.handleGameOver(),
      () => this.checkLoss(),   // fires when the last Zone A ball is destroyed
    );
    const aim = new AimController(scene, factory, (x, tier) => {
      board.spawnDropped(x, tier);
      this.onBallDropped();
    });
    this.board = board;
    this.aim = aim;

    this.emitBuffer();

    this.bus.on(GameEvent.ScoreBarFilled, () => {
      this.ballBuffer = BALL_BUFFER_INITIAL;
      this.aim?.setDropLocked(false);
      this.emitBuffer();
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

  private onBallDropped(): void {
    if (this.ballBuffer <= 0) return;
    this.ballBuffer -= 1;
    this.emitBuffer();
    if (this.ballBuffer === 0) {
      this.aim?.setDropLocked(true);
      this.checkLoss();
    }
  }

  /** Run ends only when all three doors are closed simultaneously. */
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
