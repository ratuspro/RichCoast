import Phaser from 'phaser';
import { GameEvent, type BallBodyData, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { Sfx } from '../core/Sfx';

/** How long the suck tween runs before the ball pops into Zone B, in ms. */
const SUCK_MS = 150;
/** The quick scale-up "spawn pop" at the entry column after the suck, in ms. */
const POP_MS = 110;
/** Duration of one left→right sweep leg (yoyo doubles it). Difficulty knob. */
const SWEEP_MS = 1100;
/** Inset of the sweep from each Zone B edge (~one ball radius + wall slack), in px. */
const SWEEP_MARGIN = 18;

/**
 * Minimal structural view of a Matter body carrying ball identity. Zone A stamps
 * `ballData` onto each ball body it spawns (see BallBodyData); Zone C reads it off
 * a shared-world query — so the two zones couple through the physics world + bus,
 * never by importing each other.
 *
 * `circleRadius` (set by Matter on circle bodies) lets us measure edge distance, and
 * `gameObject` (Phaser's back-reference to the Matter.Image) lets us remove the ball
 * from Zone A by destroying its image — the Board self-prunes off that DESTROY event.
 */
interface BallBody {
  position: { x: number; y: number };
  circleRadius?: number;
  gameObject?: Phaser.GameObjects.GameObject;
  ballData?: BallBodyData;
}

/**
 * Zone C — the trap-door (Dev 1).
 *
 * Owns the cooldown lock (driven by Zone B's busy/empty events) and a sweep marker
 * that oscillates left↔right across the band while armed. A tap freezes the marker;
 * its current column becomes the Zone B entry. ZoneBBusy fires up front (so Zone A's
 * stalemate check stays blocked while the ball is mid-transit), then a suck→pop
 * cosmetic runs and BALL_DROPPED is emitted at the frozen column when it lands — so
 * WHERE a ball enters Zone B is a timing skill, not a fixed column.
 */
export class ZoneCSystem implements GameSystem {
  private locked = false;
  private scene?: Phaser.Scene;
  private door?: Phaser.GameObjects.Rectangle;
  private marker?: Phaser.GameObjects.Rectangle;
  private sweepMinX = 0;
  private sweepMaxX = 0;
  /** Elapsed sweep time (ms); advanced only while armed, reset to 0 on re-arm. */
  private sweepT = 0;

  constructor(private readonly bus: EventBus) {}

  create(scene: Phaser.Scene): void {
    this.scene = scene;

    // Cooldown: locked while Zone B has balls in flight, re-armed when it's empty.
    this.bus.on(GameEvent.ZoneBBusy, () => this.setLocked(true));
    this.bus.on(GameEvent.ZoneBEmpty, () => this.setLocked(false));

    const r = Layout.zoneC;
    this.door = scene.add
      .rectangle(r.x + r.width / 2, r.y + r.height / 2, r.width, r.height, 0x1d2740)
      .setInteractive({ useHandCursor: true });
    this.door.on(Phaser.Input.Events.POINTER_DOWN, () => this.onTap());

    // The sweep marker: a puck gliding left↔right along the door band. Where it sits
    // when the player taps becomes the Zone B entry column, so its travel is inset by
    // one ball radius from each edge — a ball can never spawn into the side wall.
    const mouthY = r.y + r.height / 2;
    this.sweepMinX = Layout.zoneB.x + SWEEP_MARGIN;
    this.sweepMaxX = Layout.zoneB.x + Layout.zoneB.width - SWEEP_MARGIN;
    this.marker = scene.add
      .rectangle(this.sweepMinX, mouthY, 18, 12, 0x6cf0c2)
      .setStrokeStyle(2, 0xffffff)
      .setDepth(50);

    this.refreshDoor();
  }

  /**
   * The marker is driven straight off the `locked` flag every frame — no tween to get
   * stuck. Armed: it oscillates left↔right (cosine, so it eases in/out at both ends).
   * Locked: it's hidden and frozen. Because this reads the current state each tick, the
   * sweep always reappears the instant Zone B clears (`locked` → false), self-healing
   * regardless of how the busy/empty events interleave.
   */
  update(_time: number, delta: number): void {
    const marker = this.marker;
    if (!marker) return;
    if (this.locked) {
      marker.setVisible(false);
      return;
    }
    marker.setVisible(true);
    this.sweepT += delta;
    const period = SWEEP_MS * 2; // there and back
    const phase = ((this.sweepT % period) / period) * Math.PI * 2;
    const k = (1 - Math.cos(phase)) / 2; // 0→1→0, zero velocity at each end
    marker.x = this.sweepMinX + (this.sweepMaxX - this.sweepMinX) * k;
  }

  private onTap(): void {
    if (this.locked) return;

    const ball = this.findNearestBall();
    if (!ball?.ballData) return; // nothing to suck yet (e.g. Zone A still empty)

    // Freeze the sweep the instant the player commits — the marker's current column is
    // where the ball will enter Zone B. Capture it before setLocked() hides the marker.
    const spawnX = this.marker?.x ?? Layout.zoneBEntry.x;

    // Signal busy up front so ZoneASystem.checkLoss() can't read a stalemate while the
    // ball is mid-transit: BALL_DROPPED is now deferred to the end of the suck→pop, but
    // Zone A still sees "Zone B busy", so the board emptying here is never a game-over.
    this.setLocked(true);
    this.bus.emit(GameEvent.ZoneBBusy);

    const { value, tier } = ball.ballData;
    const startX = ball.position.x;
    const startY = ball.position.y;
    const image = ball.gameObject as Phaser.GameObjects.Image | undefined;
    const texKey = image?.texture?.key;

    // Remove the ball from Zone A by destroying its image — the Board self-prunes its
    // registry off the image's DESTROY event (see Board.register).
    image?.destroy();

    Sfx.transition();
    // Cosmetic suck → spawn pop → hand off to Zone B at the frozen column.
    this.playSuck(startX, startY, texKey, value, tier, spawnX);
  }

  /**
   * Cosmetic suck → spawn pop → handoff. A throwaway snapshot sprite slides from the ball's
   * last Zone-A position to the frozen entry column at the door mouth (suck), then pops up at
   * the top of Zone B (pop), and only when the pop lands do we emit BALL_DROPPED so Zone B's
   * real ball appears exactly there — deferring the emit avoids any double-ball flicker. If
   * there's no sprite to animate we still hand the ball off so Zone B isn't starved.
   */
  private playSuck(
    x: number,
    y: number,
    texKey: string | undefined,
    value: number,
    tier: number,
    spawnX: number,
  ): void {
    const scene = this.scene;
    if (!scene || !texKey) {
      this.emitDrop(value, tier, spawnX);
      return;
    }

    const mouthY = Layout.zoneC.y + Layout.zoneC.height / 2;
    const sprite = scene.add.image(x, y, texKey).setDepth(800);
    scene.tweens.add({
      targets: sprite,
      x: spawnX,
      y: mouthY,
      scale: 0.4,
      duration: SUCK_MS,
      ease: 'Cubic.easeIn',
      onComplete: () => {
        sprite.setPosition(spawnX, Layout.zoneBEntry.y);
        scene.tweens.add({
          targets: sprite,
          scale: 1,
          duration: POP_MS,
          ease: 'Back.easeOut',
          onComplete: () => {
            this.emitDrop(value, tier, spawnX);
            sprite.destroy();
          },
        });
      },
    });
  }

  private emitDrop(value: number, tier: number, x: number): void {
    // x is the column the player picked by tapping the Zone C sweep marker at the right moment.
    this.bus.emit(GameEvent.BallDropped, { value, tier, x });
  }

  /**
   * The droppable ball closest to the tunnel mouth by EDGE distance, read from the
   * SHARED Matter world. Only bodies tagged with `ballData` and still above the door
   * count. Subtracting the radius favours a bigger ball whose edge reaches nearer.
   */
  private findNearestBall(): BallBody | undefined {
    if (!this.scene) return undefined;

    const mouthX = Layout.zoneBEntry.x;
    const doorY = Layout.zoneC.y;

    let best: BallBody | undefined;
    let bestDist = Infinity;
    for (const raw of this.scene.matter.world.getAllBodies()) {
      const body = raw as unknown as BallBody;
      if (!body.ballData) continue;
      if (body.position.y > doorY) continue; // only balls still in Zone A
      const dx = body.position.x - mouthX;
      const dy = body.position.y - doorY;
      const dist = Math.hypot(dx, dy) - (body.circleRadius ?? 0);
      if (dist < bestDist) {
        bestDist = dist;
        best = body;
      }
    }
    return best;
  }

  private setLocked(locked: boolean): void {
    const wasLocked = this.locked;
    this.locked = locked;
    // On re-arm (locked→unlocked) restart the sweep from the left edge. update() does the
    // actual showing/hiding each frame, so the marker can't get stuck out of sync.
    if (wasLocked && !locked) this.sweepT = 0;
    this.refreshDoor();
  }

  private refreshDoor(): void {
    // Armed = blue, locked = dim red.
    this.door?.setFillStyle(this.locked ? 0x3a1d1d : 0x1d2740);
    this.door?.setStrokeStyle(2, this.locked ? 0xd55a5a : 0x3a7bd5);
  }
}
