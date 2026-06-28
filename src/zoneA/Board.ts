import Phaser from 'phaser';
import { blastImpulse, isNearDeath, isOverflow, isRestingAbove, midpoint, nextRestMs } from './ballMath';
import { canMerge, mergedTier } from './MergeLogic';
import { Sfx } from '../core/Sfx';
import type { Ball, BallFactory } from './BallFactory';
import { BLAST_RADIUS, BLAST_STRENGTH, DEATH_LINE_Y, REST_MS, REST_SPEED, SPAWN_Y, WARN_BAND } from './tuning';

/**
 * The live merge board: owns every dropped ball, merges same-tier pairs on contact,
 * fires the blast, and watches for overflow. Balls are keyed by their Matter body so
 * the collision handler — which only receives bodies — can map back to our records.
 *
 * Merges are detected in the collision callback but resolved in `update()`, so all
 * world mutation happens at one safe point and a body can't be claimed by two merges
 * in the same step (the `consumed` flag dedupes).
 */
export class Board {
  private readonly registry = new Map<MatterJS.BodyType, Ball>();
  private readonly pending: Array<{ a: Ball; b: Ball }> = [];
  private over = false;
  private dangerActive = false;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly factory: BallFactory,
    private readonly onGameOver: () => void,
    private readonly onEmpty?: () => void,
    private readonly onDanger?: (near: boolean) => void,
  ) {
    scene.matter.world.on(Phaser.Physics.Matter.Events.COLLISION_START, this.onCollisionStart);
  }

  /** Drop a fresh ball into the board at the spawn row. */
  spawnDropped(x: number, tier: number): void {
    if (this.over) return;
    this.register(this.factory.spawn(x, SPAWN_Y, tier));
  }

  update(delta: number): void {
    if (this.over) return;
    this.resolveMerges();
    this.scanOverflow(delta);
  }

  destroy(): void {
    // On scene shutdown/restart, Phaser's Matter plugin runs its own SHUTDOWN handler first
    // and sets `matter.world` to null (destroying the world, which clears its own listeners
    // and bodies). So when we get here mid-restart the world may already be gone — guard it,
    // and let Phaser's display-list teardown reclaim the ball images. Touching a null world
    // here used to throw and abort the whole restart.
    const world = this.scene.matter.world;
    if (world) {
      world.off(Phaser.Physics.Matter.Events.COLLISION_START, this.onCollisionStart);
      for (const ball of [...this.registry.values()]) ball.image.destroy();
    }
    this.registry.clear();
    this.pending.length = 0;
  }

  getBallCount(): number { return this.registry.size; }

  private merging = false;

  private register(ball: Ball): void {
    this.registry.set(ball.body, ball);
    // Self-prune when the image is destroyed (by a merge here, or later by Zone C's suck).
    // Skip the empty check during a merge batch — the merged result hasn't been registered
    // yet, so the registry momentarily hits zero even though a ball is being born.
    ball.image.once(Phaser.GameObjects.Events.DESTROY, () => {
      this.registry.delete(ball.body);
      if (!this.merging && this.registry.size === 0) this.onEmpty?.();
    });
  }

  /** Flag mergeable new contacts; defer the actual world mutation to `update()`. */
  private onCollisionStart = (event: Phaser.Physics.Matter.Events.CollisionStartEvent): void => {
    if (this.over) return;
    for (const pair of event.pairs) {
      const a = this.registry.get(pair.bodyA);
      const b = this.registry.get(pair.bodyB);
      if (!a || !b) continue; // a wall/floor, or a body we don't own
      if (a.consumed || b.consumed) continue; // already claimed this step → dedupe
      if (!canMerge(a.tier, b.tier)) continue;
      a.consumed = true;
      b.consumed = true;
      this.pending.push({ a, b });
    }
  };

  private resolveMerges(): void {
    if (this.pending.length === 0) return;
    this.merging = true;
    for (const { a, b } of this.pending.splice(0)) {
      const where = midpoint(a.body.position, b.body.position);
      const tier = mergedTier(a.tier);
      a.image.destroy(); // destroys the body too; the DESTROY listener prunes the registry
      b.image.destroy();
      const merged = this.factory.spawn(where.x, where.y, tier);
      this.register(merged);
      this.applyBlast(where, merged.body);
      Sfx.merge();
    }
    this.merging = false;
    if (this.registry.size === 0) this.onEmpty?.();
  }

  /** Push nearby balls outward from a merge point (additive velocity kick). */
  private applyBlast(origin: { x: number; y: number }, exclude: MatterJS.BodyType): void {
    for (const ball of this.registry.values()) {
      if (ball.body === exclude) continue;
      const dv = blastImpulse(ball.body.position, origin, BLAST_RADIUS, BLAST_STRENGTH);
      if (dv.x === 0 && dv.y === 0) continue;
      const v = ball.body.velocity;
      ball.image.setVelocity(v.x + dv.x, v.y + dv.y);
    }
  }

  /**
   * End the run if any ball has rested above the death line long enough, and
   * flag the death-line warning when any resting ball is close (but not yet over).
   */
  private scanOverflow(delta: number): void {
    let near = false;
    for (const ball of this.registry.values()) {
      const resting = isRestingAbove(
        ball.body.position.y,
        ball.body.speed,
        DEATH_LINE_Y,
        REST_SPEED,
      );
      ball.restMs = nextRestMs(ball.restMs, delta, resting);
      if (isOverflow(ball.restMs, REST_MS)) {
        this.over = true;
        this.onGameOver();
        return;
      }
      near ||= isNearDeath(ball.body.position.y, ball.body.speed, DEATH_LINE_Y, WARN_BAND, REST_SPEED);
    }
    this.setDanger(near);
  }

  /** Emit a danger transition only when it changes, so the warning tween isn't restarted each frame. */
  private setDanger(near: boolean): void {
    if (near === this.dangerActive) return;
    this.dangerActive = near;
    this.onDanger?.(near);
  }
}
