import Phaser from 'phaser';
import { blastImpulse, isNearDeath, isOverflow, isRestingAbove, midpoint, nextRestMs } from './ballMath';
import { canMerge, mergedTier } from './MergeLogic';
import { Sfx } from '../core/Sfx';
import type { ArenaView } from './ArenaView';
import type { Ball, BallFactory } from './BallFactory';
import { BLAST_RADIUS, BLAST_STRENGTH, REST_MS, REST_SPEED, WARN_BAND } from './tuning';

/** A blacklisted ball pulled off the board at a milestone — enough to animate its drain to Zone B. */
export interface DrainedBall {
  x: number;
  y: number;
  tier: number;
  texKey: string;
  worldDiameter: number;
}

/**
 * The live merge board: owns every dropped ball, merges same-tier pairs on contact,
 * fires the blast, and watches for overflow. Balls are keyed by their Matter body so
 * the collision handler — which only receives bodies — can map back to our records.
 *
 * Merges are detected in the collision callback but resolved in `update()`, so all
 * world mutation happens at one safe point and a body can't be claimed by two merges
 * in the same step (the `consumed` flag dedupes).
 *
 * Physics feel is normalized to the arena scale `s`: the camera zoom is 1/s, so any fixed
 * world-space constant would drift on screen as milestones grow the arena. Gravity gets a
 * per-ball supplemental force of (s−1) extra gravities (world gravity itself is shared
 * with Zone B and must stay untouched), and the blast radius/strength and rest-speed
 * thresholds scale by s — so falls, shoves, and settling look identical at every milestone.
 */
export class Board {
  private readonly registry = new Map<MatterJS.BodyType, Ball>();
  private readonly pending: Array<{ a: Ball; b: Ball }> = [];
  private over = false;
  private dangerActive = false;

  constructor(
    private readonly scene: Phaser.Scene,
    private readonly factory: BallFactory,
    private readonly arena: ArenaView,
    private readonly onGameOver: () => void,
    private readonly onEmpty?: () => void,
    private readonly onDanger?: (near: boolean) => void,
  ) {
    scene.matter.world.on(Phaser.Physics.Matter.Events.COLLISION_START, this.onCollisionStart);
    scene.matter.world.on(Phaser.Physics.Matter.Events.BEFORE_UPDATE, this.onBeforePhysics);
  }

  /** Drop a fresh ball into the board at the (scale-aware) spawn row. */
  spawnDropped(x: number, tier: number): void {
    if (this.over) return;
    this.register(this.factory.spawn(x, this.arena.spawnY, tier));
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
      world.off(Phaser.Physics.Matter.Events.BEFORE_UPDATE, this.onBeforePhysics);
      for (const ball of [...this.registry.values()]) ball.image.destroy();
    }
    this.registry.clear();
    this.pending.length = 0;
  }

  getBallCount(): number { return this.registry.size; }

  /**
   * True when nothing on the board is still in motion: no merges waiting to resolve, and
   * every body either sleeping or below the (scale-normalized) rest speed — the same
   * threshold scanOverflow uses. Drives the settle gate that arms the A→B phase pan.
   */
  isSettled(): boolean {
    if (this.pending.length > 0) return false;
    const restSpeed = REST_SPEED * this.arena.scale;
    for (const ball of this.registry.values()) {
      if (!ball.body.isSleeping && ball.body.speed >= restSpeed) return false;
    }
    return true;
  }

  /**
   * Remove every board ball whose tier is below `minTier` — the tiers blacklisted when a
   * milestone shifts the draw window up — and return a snapshot of each so the caller can
   * animate them draining into Zone B. Destroying each image self-prunes the registry via its
   * DESTROY hook (see `register`).
   */
  takeBallsBelow(minTier: number): DrainedBall[] {
    const drained: DrainedBall[] = [];
    for (const ball of [...this.registry.values()]) {
      if (ball.tier >= minTier) continue;
      drained.push({
        x: ball.body.position.x,
        y: ball.body.position.y,
        tier: ball.tier,
        texKey: ball.image.texture.key,
        worldDiameter: ball.image.displayWidth,
      });
      ball.image.destroy();
    }
    return drained;
  }

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

  /**
   * Supplemental gravity, applied before each physics step. Zone A balls should feel
   * gravity × arena scale `s` (the camera zoom is 1/s, so this keeps on-screen fall speed
   * milestone-invariant), but world gravity is shared with Zone B and must stay put. So we
   * add the missing (s−1) gravities to our own balls only, mirroring Matter's gravity
   * formula (force += mass × g × g.scale); forces are cleared by the engine each step.
   */
  private onBeforePhysics = (): void => {
    const extra = this.arena.scale - 1;
    if (extra === 0) return;
    const g = this.scene.matter.world.localWorld.gravity;
    for (const ball of this.registry.values()) {
      const body = ball.body;
      if (body.isSleeping) continue; // Matter skips gravity for sleepers; so do we
      body.force.x += body.mass * g.x * g.scale * extra;
      body.force.y += body.mass * g.y * g.scale * extra;
    }
  };

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

  /** Push nearby balls outward from a merge point (additive velocity kick). Radius and
   *  strength scale with the arena so the shove stays constant relative to ball sizes. */
  private applyBlast(origin: { x: number; y: number }, exclude: MatterJS.BodyType): void {
    const s = this.arena.scale;
    for (const ball of this.registry.values()) {
      if (ball.body === exclude) continue;
      const dv = blastImpulse(ball.body.position, origin, BLAST_RADIUS * s, BLAST_STRENGTH * s);
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
    // The death line scales with the arena, so a milestone zoom-out moves it up as headroom
    // grows; the rest-speed threshold scales too (world speeds are ×s under normalized gravity).
    const lineY = this.arena.deathLineY;
    const band = WARN_BAND * this.arena.scale;
    const restSpeed = REST_SPEED * this.arena.scale;
    let near = false;
    for (const ball of this.registry.values()) {
      const resting = isRestingAbove(
        ball.body.position.y,
        ball.body.speed,
        lineY,
        restSpeed,
      );
      ball.restMs = nextRestMs(ball.restMs, delta, resting);
      if (isOverflow(ball.restMs, REST_MS)) {
        this.over = true;
        this.onGameOver();
        return;
      }
      near ||= isNearDeath(ball.body.position.y, ball.body.speed, lineY, band, restSpeed);
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
