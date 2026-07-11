import Phaser from 'phaser';
import type { BallBodyData } from '../core/contracts';
import * as Layout from '../core/Layout';
import { nearestDoorBall } from './ballMath';

/**
 * A Matter body carrying a Zone A ball's identity, as seen through the shared physics world.
 * `circleRadius` (Matter sets it on circle bodies) measures edge distance; `gameObject` is
 * Phaser's back-reference to the Matter.Image so Zone C can remove the ball by destroying it.
 */
export interface DoorBall {
  position: { x: number; y: number };
  circleRadius?: number;
  gameObject?: Phaser.GameObjects.GameObject;
  ballData?: BallBodyData;
}

/**
 * The Zone A ball a trap-door tap would grab: nearest the fixed door mouth
 * (`Layout.zoneBEntry.x` / `Layout.zoneC.y`) by edge distance, read from the SHARED Matter
 * world. This is the single source of truth for BOTH Zone C's actual grab (`onTap`) and Zone
 * A's candidate highlight, so the ring can never sit on a different ball than the one the tap
 * takes. The pure selection math lives in `nearestDoorBall` (unit-tested); this only gathers
 * the world's bodies and hands over the door geometry.
 */
export function findNearestDoorBall(scene: Phaser.Scene): DoorBall | undefined {
  const bodies = scene.matter.world.getAllBodies() as unknown as DoorBall[];
  return nearestDoorBall(bodies, Layout.zoneBEntry.x, Layout.zoneC.y);
}
