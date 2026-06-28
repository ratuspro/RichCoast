import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';

/** How long the fake arena "runs" a ball before it drains, in ms. */
const FAKE_FLIGHT_MS = 900;

/**
 * Stub Zone B for `?zone=ac`. Implements the B-side of the contract so Dev 1 can
 * build Zone A + C against a believable arena with zero dependence on real physics:
 * each drop goes BUSY, waits, then scores the ball's value and goes EMPTY.
 *
 * Lives in dev/ so it stays available even while Dev 2's real Zone B is mid-change.
 */
export class StubZoneB implements GameSystem {
  private scene?: Phaser.Scene;
  private inFlight = 0;
  private total = 0;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.bus.on(GameEvent.BallDropped, (ball) => this.runBall(ball.value));
  }

  update(_time: number, _delta: number): void {}

  private runBall(value: number): void {
    this.inFlight += 1;
    if (this.inFlight === 1) this.bus.emit(GameEvent.ZoneBBusy);

    this.scene?.time.delayedCall(FAKE_FLIGHT_MS, () => {
      this.total += value;
      this.bus.emit(GameEvent.ScoreChanged, { total: this.total });

      this.inFlight -= 1;
      if (this.inFlight === 0) this.bus.emit(GameEvent.ZoneBEmpty);
    });
  }
}
