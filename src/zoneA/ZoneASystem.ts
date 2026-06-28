import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { Sfx } from '../core/Sfx';
import { AimController } from './AimController';
import { BallFactory } from './BallFactory';
import { Board } from './Board';
import { DeathLine } from './DeathLine';

const BALL_BUFFER_INITIAL = 4;

/**
 * A stalemate must persist this long before the run actually ends. Balls hand off between
 * zones through transient empty states (a Zone C suck is mid-tween, a merge briefly empties
 * the board), so we confirm the stalemate after a short grace — longer than Zone C's suck —
 * and only end if it still holds. A real stalemate persists; a transient resolves.
 */
const STALEMATE_GRACE_MS = 250;

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
  private deathLine?: DeathLine;
  private over = false;

  private ballBuffer = BALL_BUFFER_INITIAL;
  private zoneBEmpty = true; // starts true — nothing is in flight yet
  private score = 0; // latest Zone B total, mirrored for the game-over screen
  private lossPending = false; // a stalemate is scheduled for grace-period re-check

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.deathLine = new DeathLine(scene);
    const factory = new BallFactory(scene);
    const board = new Board(
      scene,
      factory,
      () => this.handleGameOver(),
      () => this.checkLoss(),   // fires when the last Zone A ball is destroyed
      (near) => this.deathLine?.setDanger(near),
    );
    const aim = new AimController(scene, factory, (x, tier) => {
      board.spawnDropped(x, tier);
      this.onBallDropped();
      Sfx.drop();
    });
    this.board = board;
    this.aim = aim;

    this.emitBuffer();

    this.bus.on(GameEvent.ScoreChanged, ({ total }) => { this.score = total; });

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
    this.deathLine?.destroy();
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
   * End the run only on a *settled* stalemate. Because balls hand off between zones through
   * transient empty states, we confirm after a short grace and end only if it still holds.
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
    this.aim?.setBallsLeft(this.ballBuffer); // mirror into the Zone A queue row
  }

  private handleGameOver(): void {
    if (this.over) return;
    this.over = true;
    this.aim?.disable();
    this.deathLine?.setDanger(false);
    // Freeze the whole game (Zone A + Zone B physics) so the run reads as ended. This only
    // pauses the current world; scene.restart() destroys it and boots a fresh, running one,
    // so the pause never leaks into the next run.
    this.scene?.matter.world.pause();
    this.drawGameOverOverlay();
  }

  /**
   * The single, full-screen game-over screen: dims every zone, shows the final
   * score, and offers a RESTART button that rebuilds the scene from scratch.
   */
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
