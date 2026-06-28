import Phaser from 'phaser';
import type { BallSpec } from '../core/contracts';

export const BALL_RADIUS = 14;

export const CAT_BALL      = 0x0001;
export const CAT_GATE      = 0x0002;
export const CAT_WALL      = 0x0004;
export const CAT_COLLECTOR = 0x0008;

// Freshly-split balls ignore gates for 300 ms so they don't immediately re-trigger.
const SPLIT_GRACE_MS = 300;

const TIER_HUES = [200, 160, 120, 80, 40, 20, 0, 300, 260, 220] as const;

/** 0xRRGGBB → `#rrggbb` CSS string */
function hexColor(rgb: number): string {
  return `#${rgb.toString(16).padStart(6, '0')}`;
}

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
    const hue = (TIER_HUES[(tier - 1) % TIER_HUES.length] ?? 200) / 360;
    const color = Phaser.Display.Color.HSLToColor(hue, 0.7, 0.55).color;
    const size = BALL_RADIUS * 2;
    const canvas = scene.textures.createCanvas(key, size, size);
    if (canvas) {
      const ctx = canvas.getContext();
      ctx.beginPath();
      ctx.arc(BALL_RADIUS, BALL_RADIUS, BALL_RADIUS, 0, Math.PI * 2);
      ctx.fillStyle = hexColor(color);
      ctx.fill();
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
