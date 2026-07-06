import type Phaser from 'phaser';
import { materialForTier } from '../core/Materials';
import { paintBall } from '../core/MaterialPainter';
import { tierToValue, type BallBodyData } from '../core/contracts';
import { frictionForTier, radiusForTier } from './ballMath';
import { DENSITY, FRICTION_AIR, FRICTION_STATIC, RESTITUTION } from './tuning';

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
 * Builds Zone A balls. One procedurally-drawn texture per tier (the material recipe,
 * no value digits) is generated once and cached in the Texture Manager; `spawn` then
 * creates the Matter circle, applies per-tier physics, and stamps `body.ballData`
 * so Zone C can discover it on a shared-world query. No image assets.
 */
export class BallFactory {
  /**
   * @param onSpawn optional hook run on every freshly-spawned ball image — Zone A uses it to
   *   add the ball to the arena render layer (so the dedicated arena camera draws it zoomed).
   */
  constructor(
    private readonly scene: Phaser.Scene,
    private readonly onSpawn?: (image: Phaser.Physics.Matter.Image) => void,
  ) {}

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
    // but a CanvasTexture uploads like any bitmap, so the painted sphere shows.
    const canvas = textures.createCanvas(key, size, size);
    if (!canvas) return key;
    const ctx = canvas.getContext();

    // No value digits: the ball's worth stays hidden from the player — material look
    // (colour family + detail pass) is the only tier signal.
    paintBall(ctx, radius, tier, 'full');

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
    // Material feel: the shared multipliers are a narrow band on top of Zone A's tuned
    // constants — wood bounces a touch, gold is dense, gems slip — no balance rework.
    const feel = materialForTier(tier).def.physics;
    image
      .setFriction(frictionForTier(tier), FRICTION_AIR, FRICTION_STATIC)
      .setBounce(RESTITUTION * feel.restitutionMult)
      .setDensity(DENSITY * feel.densityMult);

    const body = image.body as MatterJS.BodyType;
    (body as TaggedBody).ballData = { value: tierToValue(tier), tier };

    this.onSpawn?.(image);
    return { image, body, tier, consumed: false, restMs: 0 };
  }
}
