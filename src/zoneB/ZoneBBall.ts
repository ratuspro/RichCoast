import Phaser from 'phaser';
import { materialForTier } from '../core/Materials';
import { paintBall } from '../core/MaterialPainter';
import type { BallSpec } from '../core/contracts';

export const BALL_RADIUS = 10;

export const CAT_BALL      = 0x0001;
export const CAT_GATE      = 0x0002;
export const CAT_WALL      = 0x0004;
export const CAT_COLLECTOR = 0x0008;

// Freshly-split balls ignore gates for 300 ms so they don't immediately re-trigger.
const SPLIT_GRACE_MS = 300;

export function createZoneBBall(
  scene: Phaser.Scene,
  x: number,
  y: number,
  value: number,
  tier: number,
  fromSplit = false,
): Phaser.Physics.Matter.Image {
  const key = `zb-ball-t${tier}`;
  if (!scene.textures.exists(key)) {
    const size = BALL_RADIUS * 2;
    const canvas = scene.textures.createCanvas(key, size, size);
    if (canvas) {
      // Same shared material recipe as Zone A (small LOD: base shading only — at 10px
      // colour is the identity), so a transferred ball keeps its exact look.
      paintBall(canvas.getContext(), BALL_RADIUS, tier, 'small');
      canvas.refresh();
    }
  }

  const collisionFilter = fromSplit
    ? { category: CAT_BALL, mask: CAT_BALL | CAT_WALL | CAT_COLLECTOR }          // skip gates during grace, but still bump other balls
    : { category: CAT_BALL, mask: CAT_BALL | CAT_GATE | CAT_WALL | CAT_COLLECTOR };

  // Material feel: same shared multipliers as Zone A, on Zone B's own constants.
  // Restitution is capped so exotic tiers stay lively without ping-ponging the cascade;
  // mass is left alone — the fixed 10px radius already fixes it.
  const feel = materialForTier(tier).def.physics;
  const img = scene.matter.add.image(x, y, key, undefined, {
    shape: { type: 'circle', radius: BALL_RADIUS },
    restitution: Math.min(0.5, 0.35 * feel.restitutionMult),
    friction: 0.05 * feel.frictionMult,
    frictionAir: 0.008,
    label: 'zoneB-ball',
    collisionFilter,
  });

  const mb = img.body as MatterJS.BodyType;
  (mb as unknown as { ballData: BallSpec; ballImage: Phaser.Physics.Matter.Image }).ballData = { value, tier };
  (mb as unknown as { ballData: BallSpec; ballImage: Phaser.Physics.Matter.Image }).ballImage = img;

  if (fromSplit) {
    scene.time.delayedCall(SPLIT_GRACE_MS, () => {
      if (!img.active) return;
      img.setCollisionCategory(CAT_BALL);
      img.setCollidesWith([CAT_BALL, CAT_GATE, CAT_WALL, CAT_COLLECTOR]);
    });
  }

  return img;
}

export function destroyZoneBBall(img: Phaser.Physics.Matter.Image): void {
  img.destroy(); // Phaser removes the Matter body automatically
}

export function getBallData(body: MatterJS.BodyType): BallSpec | null {
  return (body as unknown as { ballData?: BallSpec }).ballData ?? null;
}

export function getBallImage(body: MatterJS.BodyType): Phaser.Physics.Matter.Image | null {
  return (body as unknown as { ballImage?: Phaser.Physics.Matter.Image }).ballImage ?? null;
}
