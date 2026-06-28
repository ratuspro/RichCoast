/** Newly queued balls are drawn from the low tiers so the board doesn't fill with
 *  big balls immediately. */
const SPAWN_MIN_TIER = 1;
const SPAWN_MAX_TIER = 4;

/**
 * The "current + next" drop queue for Zone A: the player always has a ball ready
 * and can preview the upcoming one.
 *
 * The queue/tier logic is implemented (it's a pure rule). Binding it to actual
 * physics balls and the on-screen preview is Dev 1's job in ZoneASystem.
 */
export class BallQueue {
  private currentTier = BallQueue.randomSpawnTier();
  private nextTier = BallQueue.randomSpawnTier();

  /** Tier of the ball ready to drop now. */
  peek(): number {
    return this.currentTier;
  }

  /** Tier of the ball after the current one (drives the preview). */
  peekNext(): number {
    return this.nextTier;
  }

  /** Consume the current ball, advance the queue, and return the dropped tier. */
  pop(): number {
    const dropped = this.currentTier;
    this.currentTier = this.nextTier;
    this.nextTier = BallQueue.randomSpawnTier();
    return dropped;
  }

  private static randomSpawnTier(): number {
    const span = SPAWN_MAX_TIER - SPAWN_MIN_TIER + 1;
    return SPAWN_MIN_TIER + Math.floor(Math.random() * span);
  }
}
