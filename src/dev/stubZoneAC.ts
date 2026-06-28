import type Phaser from 'phaser';
import { GameEvent, tierToValue, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';

/**
 * Stub Zone A + C for `?zone=b`. Implements the A/C side of the contract so Dev 2
 * can build the real Zone B in isolation: it fires BALL_DROPPED on demand and
 * honors the trap-door lock (won't fire while Zone B reports itself BUSY).
 *
 * The Harness UI calls dropBall(). Lives in dev/ — neither it nor the harness
 * depends on Dev 1's real files.
 */
export class StubZoneAC implements GameSystem {
  private locked = false;

  constructor(private readonly bus: EventBus) {}

  create(_scene: Phaser.Scene): void {
    this.bus.on(GameEvent.ZoneBBusy, () => (this.locked = true));
    this.bus.on(GameEvent.ZoneBEmpty, () => (this.locked = false));
  }

  update(_time: number, _delta: number): void {}

  /** Whether the trap-door would currently fire (mirrors Zone C's lock). */
  get armed(): boolean {
    return !this.locked;
  }

  /**
   * Fire the trap-door with a ball of `tier` at the fixed Zone B entry x. No-op
   * while locked, exactly like the real Zone C.
   * @returns true if the drop was emitted.
   */
  dropBall(tier: number): boolean {
    if (this.locked) return false;
    this.bus.emit(GameEvent.BallDropped, {
      tier,
      value: tierToValue(tier),
      x: Layout.zoneBEntry.x,
    });
    return true;
  }
}
