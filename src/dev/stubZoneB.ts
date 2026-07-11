import type Phaser from 'phaser';
import { GameEvent, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';

/** How long the fake arena "runs" a ball before it drains, in ms. */
const FAKE_FLIGHT_MS = 900;
/** Fallback score-bar target until the first PROGRESSION_CHANGED arrives. */
const DEFAULT_BAR_TARGET = 50;

/**
 * Stub Zone B for `?zone=ac`. Implements the B-side of the contract so Dev 1 can
 * build Zone A + C against a believable arena with zero dependence on real physics:
 * each drop goes BUSY, waits, then scores the ball's value and goes EMPTY.
 *
 * It also fakes the score bar so the full phase loop is drivable in isolation: drained
 * value accumulates toward the current stage's target (read off PROGRESSION_CHANGED),
 * and — like the real Zone B — SCORE_BAR_FILLED only fires once the arena is empty,
 * which is what pans the game back into the Zone-A phase.
 *
 * Lives in dev/ so it stays available even while Dev 2's real Zone B is mid-change.
 */
export class StubZoneB implements GameSystem {
  private scene?: Phaser.Scene;
  private inFlight = 0;
  private total = 0;
  private roundScore = 0;
  private barFilled = 0;
  private barTarget = DEFAULT_BAR_TARGET;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;
    this.bus.on(GameEvent.BallDropped, (ball) => this.runBall(ball.value));
    this.bus.on(GameEvent.ProgressionChanged, ({ scoreBarTarget }) => {
      this.barTarget = scoreBarTarget;
    });
  }

  update(_time: number, _delta: number): void {}

  private runBall(value: number): void {
    this.inFlight += 1;
    if (this.inFlight === 1) this.bus.emit(GameEvent.ZoneBBusy);

    this.scene?.time.delayedCall(FAKE_FLIGHT_MS, () => {
      this.total += value;
      this.bus.emit(GameEvent.ScoreChanged, { total: this.total });
      this.roundScore += value;
      this.barFilled += value;
      this.bus.emit(GameEvent.ScoreBarChanged, { filled: this.barFilled, target: this.barTarget });

      this.inFlight -= 1;
      if (this.inFlight === 0) {
        this.maybeCashIn();
        this.bus.emit(GameEvent.ZoneBEmpty);
      }
    });
  }

  /** Arena just emptied: if the fake bar is full, cash it in (Filled advances the stage in
   *  Zone A, whose PROGRESSION_CHANGED handler above then refreshes our target). The stub does
   *  no multi-level roll, so it fires CashedIn — the pan-up trigger — in the same beat. */
  private maybeCashIn(): void {
    if (this.barFilled < this.barTarget) return;
    this.barFilled = 0;
    this.bus.emit(GameEvent.ScoreBarFilled);
    this.bus.emit(GameEvent.ScoreBarChanged, { filled: 0, target: this.barTarget });
    if (this.roundScore > 0) {
      this.bus.emit(GameEvent.ScoreHarvested, {
        amount: this.roundScore,
        x: Layout.WIDTH / 2,
        y: Layout.HEIGHT - 5,
      });
      this.roundScore = 0;
    }
    this.bus.emit(GameEvent.ScoreBarCashedIn);
  }
}
