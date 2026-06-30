export class BallQueue {
  private minTier = 1;
  private maxTier = 4;
  private seeded: number[] = [];

  private currentTier: number;
  private nextTier: number;

  constructor() {
    this.currentTier = this.pullNext();
    this.nextTier = this.pullNext();
  }

  /** Tier of the ball ready to drop now. */
  peek(): number { return this.currentTier; }

  /** Tier of the ball after the current one (drives the preview). */
  peekNext(): number { return this.nextTier; }

  /** Consume the current ball, advance the queue, and return the dropped tier. */
  pop(): number {
    const dropped = this.currentTier;
    this.currentTier = this.nextTier;
    this.nextTier = this.pullNext();
    return dropped;
  }

  /** Update the random tier range. Takes effect for any future random draws. */
  setWindow(minTier: number, maxTier: number): void {
    this.minTier = minTier;
    this.maxTier = maxTier;
  }

  /**
   * Re-draw the current + next tiers from the active window. Called when a milestone shifts the
   * window up so the in-hand and preview balls aren't stranded on now-blacklisted low tiers
   * (setWindow alone only governs *future* draws). Clears any unconsumed seed first, so the
   * draw always comes from the live random window — never a stale low-tier seed.
   */
  reroll(): void {
    this.seeded = [];
    this.currentTier = this.pullNext();
    this.nextTier = this.pullNext();
  }

  /**
   * Pre-load specific tiers into the front of the queue.
   * Called on level transitions that have a defined `bufferBalls` list.
   * Once the seeded tiers are consumed, future draws use the random window.
   */
  seed(tiers: number[]): void {
    this.seeded = [...tiers];
    this.currentTier = this.pullNext();
    this.nextTier = this.pullNext();
  }

  private pullNext(): number {
    if (this.seeded.length > 0) return this.seeded.shift()!;
    return this.randomTier();
  }

  private randomTier(): number {
    const span = this.maxTier - this.minTier + 1;
    return this.minTier + Math.floor(Math.random() * span);
  }
}
