export const BUFFER_INITIAL_COUNT = 20;
export const BUFFER_REFILL_AMOUNT = 10;
// Buffer refills as cumulative Zone B score crosses these escalating milestones. Since the
// merge value ladder is now powers of three (values inflate fast), the first milestone is
// scaled up so refills don't come trivially early; the multiplier keeps later ones in pace.
// Starting point — tune by playtest.
export const BUFFER_INITIAL_MILESTONE = 150;
export const BUFFER_MILESTONE_MULTIPLIER = 2.5;

export class BallBuffer {
  private count: number;
  private nextMilestone: number;

  constructor(
    initialCount = BUFFER_INITIAL_COUNT,
    private readonly refillAmount = BUFFER_REFILL_AMOUNT,
    initialMilestone = BUFFER_INITIAL_MILESTONE,
    private readonly milestoneMultiplier = BUFFER_MILESTONE_MULTIPLIER,
  ) {
    this.count = initialCount;
    this.nextMilestone = initialMilestone;
  }

  spend(): boolean {
    if (this.count <= 0) return false;
    this.count -= 1;
    return true;
  }

  refillIfMilestone(total: number): boolean {
    if (total < this.nextMilestone) return false;
    this.count += this.refillAmount;
    this.nextMilestone = Math.round(this.nextMilestone * this.milestoneMultiplier);
    return true;
  }

  getCount(): number { return this.count; }
  getNextMilestone(): number { return this.nextMilestone; }
  isExhausted(): boolean { return this.count === 0; }
}
