import Phaser from 'phaser';
import { GameEvent, type BallBodyData, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';

/**
 * Minimal structural view of a Matter body carrying ball identity. Zone A stamps
 * `ballData` onto each ball body it spawns (see BallBodyData); Zone C reads it off
 * a shared-world query — so the two zones couple through the physics world + bus,
 * never by importing each other.
 */
interface BallBody {
  position: { x: number; y: number };
  ballData?: BallBodyData;
}

/**
 * Zone C — the trap-door (Dev 1).
 *
 * Fully plumbed: owns the cooldown lock (driven by Zone B's busy/empty events),
 * handles taps, finds the nearest ball still in Zone A via a shared-world query,
 * and emits BALL_DROPPED at the FIXED entry column. Only the suck animation/feel
 * and removing the consumed body are left as TODO.
 */
export class ZoneCSystem implements GameSystem {
  private locked = false;
  private scene?: Phaser.Scene;
  private door?: Phaser.GameObjects.Rectangle;

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

    this.refreshDoor();
  }

  update(_time: number, _delta: number): void {
    // Event-driven; nothing per frame.
  }

  private onTap(): void {
    if (this.locked) return;

    const ball = this.findNearestBall();
    if (!ball) return; // nothing to suck yet (e.g. Zone A still empty)

    // FROZEN: x is always the fixed Zone B entry column — never the ball's A position.
    this.bus.emit(GameEvent.BallDropped, {
      value: ball.value,
      tier: ball.tier,
      x: Layout.zoneBEntry.x,
    });

    // TODO(zoneC): play the suck animation and remove the consumed body from Zone A.
  }

  /**
   * The droppable ball closest to the tunnel mouth, read from the SHARED Matter
   * world. Only bodies tagged with `ballData` and still above the door count.
   */
  private findNearestBall(): BallBodyData | undefined {
    if (!this.scene) return undefined;

    const mouthX = Layout.zoneBEntry.x;
    const doorY = Layout.zoneC.y;

    let best: BallBodyData | undefined;
    let bestDist = Infinity;
    for (const raw of this.scene.matter.world.getAllBodies()) {
      const body = raw as unknown as BallBody;
      if (!body.ballData) continue;
      if (body.position.y > doorY) continue; // only balls still in Zone A
      const dist = Math.abs(body.position.x - mouthX) + Math.abs(body.position.y - doorY);
      if (dist < bestDist) {
        bestDist = dist;
        best = body.ballData;
      }
    }
    return best;
  }

  private setLocked(locked: boolean): void {
    this.locked = locked;
    this.refreshDoor();
  }

  private refreshDoor(): void {
    // Armed = blue, locked = dim red.
    this.door?.setFillStyle(this.locked ? 0x3a1d1d : 0x1d2740);
    this.door?.setStrokeStyle(2, this.locked ? 0xd55a5a : 0x3a7bd5);
  }
}
