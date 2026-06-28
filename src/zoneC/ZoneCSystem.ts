import Phaser from 'phaser';
import { GameEvent, type BallBodyData, type GameSystem } from '../core/contracts';
import type { EventBus } from '../core/EventBus';
import * as Layout from '../core/Layout';
import { Sfx } from '../core/Sfx';

/** How long the suck tween runs before the ball lands in Zone B, in ms. */
const SUCK_MS = 150;

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
 * Fully plumbed: owns the cooldown lock (driven by Zone B's busy/empty events),
 * handles taps, finds the nearest ball still in Zone A via a shared-world query,
 * and hands the ball off to Zone B by emitting BALL_DROPPED at the FIXED entry
 * column the instant it leaves Zone A (so the A→B transition is atomic on the bus),
 * then plays a purely cosmetic suck animation.
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
    if (!ball?.ballData) return; // nothing to suck yet (e.g. Zone A still empty)

    // Signal busy before destroying the ball so ZoneASystem.checkLoss() doesn't
    // fire while the ball is in transit (board empties before ZoneBBusy would
    // otherwise arrive from Zone B after the tween completes).
    this.setLocked(true);
    this.bus.emit(GameEvent.ZoneBBusy);

    const { value, tier } = ball.ballData;
    const startX = ball.position.x;
    const startY = ball.position.y;
    const image = ball.gameObject as Phaser.GameObjects.Image | undefined;
    const texKey = image?.texture?.key;

    // Hand the ball off to Zone B FIRST, so the A→B transition is atomic on the bus:
    // ZoneBBusy fires now (clearing Zone A's "Zone B empty" flag) before destroying the
    // A ball can trigger Zone A's loss check. The suck below is then pure cosmetics.
    this.emitDrop(value, tier);

    // Remove the ball from Zone A by destroying its image — the Board self-prunes its
    // registry off the image's DESTROY event (see Board.register).
    image?.destroy();

    Sfx.transition();
    this.playSuck(startX, startY, texKey);
  }

  /**
   * Cosmetic suck: animate a throwaway snapshot sprite from the ball's last position into
   * the door mouth. Purely visual — the ball was already handed off to Zone B in `onTap`,
   * and the real ball is gone, so this never fights Zone A physics nor gates the handoff.
   */
  private playSuck(x: number, y: number, texKey: string | undefined): void {
    const scene = this.scene;
    if (!scene || !texKey) return; // nothing to animate — the ball is already handed off

    const sprite = scene.add.image(x, y, texKey).setDepth(800);
    scene.tweens.add({
      targets: sprite,
      x: Layout.zoneBEntry.x,
      y: Layout.zoneC.y + Layout.zoneC.height / 2,
      scale: 0,
      duration: SUCK_MS,
      ease: 'Cubic.easeIn',
      onComplete: () => sprite.destroy(),
    });
  }

  private emitDrop(value: number, tier: number): void {
    // FROZEN: x is always the fixed Zone B entry column — never the ball's A position.
    this.bus.emit(GameEvent.BallDropped, { value, tier, x: Layout.zoneBEntry.x });
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
    this.locked = locked;
    this.refreshDoor();
  }

  private refreshDoor(): void {
    // Armed = blue, locked = dim red.
    this.door?.setFillStyle(this.locked ? 0x3a1d1d : 0x1d2740);
    this.door?.setStrokeStyle(2, this.locked ? 0xd55a5a : 0x3a7bd5);
  }
}
