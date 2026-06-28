import type Phaser from 'phaser';
import { hexColor } from '../core/BallColors';
import { tierToValue, type BallBodyData } from '../core/contracts';
import { frictionForTier, radiusForTier } from './ballMath';
import { DENSITY, FRICTION_AIR, FRICTION_STATIC, RESTITUTION, TIER_COLORS } from './tuning';

/**
 * A live Zone A ball: its Matter.Image (sprite + body in one), its tier, and the
 * per-frame bookkeeping the Board mutates (merge dedupe + rest-time accumulation).
 */
export interface Ball {
  readonly image: Phaser.Physics.Matter.Image;
  readonly body: MatterJS.BodyType;
  readonly tier: number;
  consumed: boolean;
  restMs: number;
}

/** A Matter body once ball identity is stamped on it — exactly what Zone C reads. */
type TaggedBody = MatterJS.BodyType & { ballData: BallBodyData };

/**
 * Builds Zone A balls. One procedurally-drawn texture per tier (flat colour + the
 * ball's value) is generated once and cached in the Texture Manager; `spawn` then
 * creates the Matter circle, applies per-tier physics, and stamps `body.ballData`
 * so Zone C can discover it on a shared-world query. No image assets.
 */
export class BallFactory {
  constructor(private readonly scene: Phaser.Scene) {}

  /** Stable texture key for a tier (also used by the aim ball + preview). */
  textureKey(tier: number): string {
    return `zoneA-ball-t${tier}`;
  }

  /** Ensure the tier's texture exists (drawn once, then reused) and return its key. */
  ensureTexture(tier: number): string {
    const key = this.textureKey(tier);
    const { textures } = this.scene;
    if (textures.exists(key)) return key;

    const radius = radiusForTier(tier);
    const size = radius * 2;

    // Drawn with the Canvas 2D API rather than Phaser Graphics: Phaser 4's renderer
    // doesn't reliably rasterise Graphics geometry into a texture (the fill is dropped),
    // but a CanvasTexture uploads like any bitmap, so fill + number both show.
    const canvas = textures.createCanvas(key, size, size);
    if (!canvas) return key;
    const ctx = canvas.getContext();

    const lineW = Math.max(2, radius * 0.08);
    ctx.beginPath();
    ctx.arc(radius, radius, radius - lineW / 2, 0, Math.PI * 2);
    ctx.fillStyle = hexColor(TIER_COLORS[tier - 1]);
    ctx.fill();
    ctx.lineWidth = lineW;
    ctx.strokeStyle = 'rgba(11, 13, 18, 0.35)';
    ctx.stroke();

    const digits = String(tierToValue(tier));
    const fontScale = digits.length <= 1 ? 1.05 : digits.length === 2 ? 0.82 : 0.62;
    const fontPx = Math.round(radius * fontScale);
    ctx.font = `bold ${fontPx}px monospace`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.lineWidth = Math.max(2, Math.round(fontPx * 0.14));
    ctx.strokeStyle = '#0b0d12';
    ctx.strokeText(digits, radius, radius);
    ctx.fillStyle = '#ffffff';
    ctx.fillText(digits, radius, radius);

    canvas.refresh();
    return key;
  }

  /** Create a live, physics-driven ball at (x, y) for the given tier. */
  spawn(x: number, y: number, tier: number): Ball {
    const key = this.ensureTexture(tier);
    const radius = radiusForTier(tier);

    const image = this.scene.matter.add.image(x, y, key, undefined, {
      shape: { type: 'circle', radius },
    });
    image
      .setFriction(frictionForTier(tier), FRICTION_AIR, FRICTION_STATIC)
      .setBounce(RESTITUTION)
      .setDensity(DENSITY);

    const body = image.body as MatterJS.BodyType;
    (body as TaggedBody).ballData = { value: tierToValue(tier), tier };

    return { image, body, tier, consumed: false, restMs: 0 };
  }
}
