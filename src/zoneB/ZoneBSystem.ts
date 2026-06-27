import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { GateSystem } from './GateSystem';
import { Funnel } from './Funnel';

/**
 * Zone B — the ball-split arena (Dev 2). Owns gates, the funnel, and scoring.
 *
 * The CONTRACT plumbing is implemented here: in-flight bookkeeping drives
 * ZONE_B_BUSY / ZONE_B_EMPTY (the trap-door lock), and score accumulation drives
 * SCORE_CHANGED. Dev 2 calls onBallSpawned / onBallDrained / addScore from the real
 * physics. Spawning the dropped ball, gate splitting and funnel draining are the
 * TODO seams.
 */
export class ZoneBSystem implements GameSystem {
  private readonly gates = new GateSystem();
  private readonly funnel = new Funnel();

  private inFlight = 0;
  private total = 0;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.drawLabel(scene);
    this.gates.create(scene);
    this.funnel.create(scene);

    this.bus.on(GameEvent.BallDropped, (ball) => {
      // TODO(zoneB): spawn a Matter circle of value `ball.value` at
      //   (ball.x, Layout.zoneB.y), then call this.onBallSpawned(). Gate hits split
      //   it (each copy → onBallSpawned); funnel drains call addScore + onBallDrained.
      void ball;
    });
  }

  update(time: number, delta: number): void {
    this.gates.update(time, delta);
    this.funnel.update(time, delta);
  }

  // --- Contract plumbing: the events Zone B owns. Call from the physics. --------

  /** A ball entered play (the initial drop, or a gate-split copy). */
  onBallSpawned(): void {
    this.inFlight += 1;
    if (this.inFlight === 1) this.bus.emit(GameEvent.ZoneBBusy);
  }

  /** A ball left play via the funnel. */
  onBallDrained(): void {
    if (this.inFlight === 0) return;
    this.inFlight -= 1;
    if (this.inFlight === 0) this.bus.emit(GameEvent.ZoneBEmpty);
  }

  /** Add a drained ball's value to the running score and report it to the HUD. */
  addScore(value: number): void {
    this.total += value;
    this.bus.emit(GameEvent.ScoreChanged, { total: this.total });
  }

  private drawLabel(scene: Phaser.Scene): void {
    const r = Layout.zoneB;
    scene.add
      .text(r.x + r.width / 2, r.y + r.height / 2, 'ZONE B\nsplit arena', {
        fontFamily: 'monospace',
        fontSize: '20px',
        color: '#566080',
        align: 'center',
      })
      .setOrigin(0.5)
      .setAlpha(0.5);
  }
}
