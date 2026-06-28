import Phaser from 'phaser';
import { colorForTier, hexColor } from '../core/BallColors';
import type { BallSpec } from '../core/contracts';

export const BALL_RADIUS = 14;

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
    // Same shared palette as Zone A, so a transferred ball keeps its exact colour.
    const color = colorForTier(tier);
    const size = BALL_RADIUS * 2;
    const canvas = scene.textures.createCanvas(key, size, size);
    if (canvas) {
      const ctx = canvas.getContext();
      // Match Zone A's thin dark rim so balls read as the same family across zones.
      const lineW = Math.max(2, BALL_RADIUS * 0.08);
      ctx.beginPath();
      ctx.arc(BALL_RADIUS, BALL_RADIUS, BALL_RADIUS - lineW / 2, 0, Math.PI * 2);
      ctx.fillStyle = hexColor(color);
      ctx.fill();
      ctx.lineWidth = lineW;
      ctx.strokeStyle = 'rgba(11, 13, 18, 0.35)';
      ctx.stroke();
      canvas.refresh();
    }
  }

  const collisionFilter = fromSplit
    ? { category: CAT_BALL, mask: CAT_WALL | CAT_COLLECTOR }          // skip gates during grace
    : { category: CAT_BALL, mask: CAT_GATE | CAT_WALL | CAT_COLLECTOR };

  const img = scene.matter.add.image(x, y, key, undefined, {
    shape: { type: 'circle', radius: BALL_RADIUS },
    restitution: 0.35,
    friction: 0.05,
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
      img.setCollidesWith([CAT_GATE, CAT_WALL, CAT_COLLECTOR]);
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
