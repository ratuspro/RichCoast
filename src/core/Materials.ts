/**
 * Single source of truth for the ball MATERIAL ladder, shared by Zone A and Zone B.
 * Replaces the old flat `BallColors.ts` palette.
 *
 * Both zones regenerate their own ball textures, so without one shared table they
 * drift and a ball transferred A→B changes identity. Zones must not import each other
 * (TECH_SPEC), so the ladder lives here in `core/`.
 *
 * "Bright Workshop / industrial materials": each tier IS a physical material — the
 * higher the tier, the more valuable the stuff. 20 materials in 5 families of 4,
 * each family aligned with one 4-tier draw window in `progression.json`
 * ([1,4] Primitives → [5,8] Metals → [9,12] Precious → [13,16] Gems → [17,20] Exotic),
 * so a window shift reads as advancing to the next material age. Tiers past 20 wrap
 * (the painter adds one glow ring per completed cycle to keep them distinct).
 *
 * Any 4 consecutive tiers must stay mutually distinguishable at Zone B's 10px ball
 * size, where colour is the only identity — hue/value spacing inside each family
 * matters more than realism.
 *
 * Physics multipliers are the "subtle feel" band: applied on top of each zone's own
 * tuned constants at spawn, narrow enough that no layout or balance retuning is needed
 * (wood bounces a touch, metal thuds, gems slip) — see SPEC.md Visual Theme.
 */

export type MaterialFamily = 'primitive' | 'metal' | 'precious' | 'gem' | 'exotic';

export interface MaterialPhysics {
  /** Multiplier on the zone's base restitution (bounciness). */
  restitutionMult: number;
  /** Multiplier on the zone's surface friction. */
  frictionMult: number;
  /** Multiplier on the zone's ball density (Zone A only — Zone B leaves mass alone). */
  densityMult: number;
}

/** Which detail pass `MaterialPainter` runs on the full-LOD (Zone A) texture. The
 *  small-LOD (Zone B, 10px) texture skips details — colour is the identity there. */
export type MaterialDetail =
  | 'grain' // curved wood-grain strokes
  | 'speckle' // scattered stone dots
  | 'gloss' // big soft resin highlight
  | 'matte' // faint horizontal throw-lines
  | 'sheen' // diagonal brushed-metal band
  | 'rivets' // dot ring just inside the rim
  | 'glint' // thin bright edge arc
  | 'crescent' // glassy white crescent
  | 'facets' // flat gem wedges
  | 'glow' // emissive bright core
  | 'crust' // dark cracks over a glowing fill
  | 'specks' // tiny stars
  | 'corona'; // white core, coloured halo

export interface MaterialDef {
  name: string;
  family: MaterialFamily;
  /** Dominant fill colour — the identity colour at any size. */
  baseColor: number;
  /** Secondary recipe colour: grain, speckle, sheen, facet or glow, per family. */
  accentColor: number;
  detail: MaterialDetail;
  physics: MaterialPhysics;
}

// Family-level feel, per the approved design table. A few materials override below.
const PRIMITIVE = { restitutionMult: 0.9, frictionMult: 1.1, densityMult: 1.0 };
const METAL = { restitutionMult: 0.8, frictionMult: 0.75, densityMult: 1.15 };
const PRECIOUS = { restitutionMult: 0.9, frictionMult: 0.8, densityMult: 1.2 };
const GEM = { restitutionMult: 1.2, frictionMult: 0.5, densityMult: 1.1 };
const EXOTIC = { restitutionMult: 1.5, frictionMult: 0.6, densityMult: 0.9 };

export const MATERIALS: readonly MaterialDef[] = [
  // — Primitives [1,4] —
  { name: 'Wood',       family: 'primitive', baseColor: 0xa9713f, accentColor: 0x7a4e2a, detail: 'grain',    physics: { ...PRIMITIVE, restitutionMult: 1.2, densityMult: 0.85 } },
  { name: 'Stone',      family: 'primitive', baseColor: 0x94a1b0, accentColor: 0x6d7885, detail: 'speckle',  physics: PRIMITIVE },
  // Turquoise (not the spec's Amber): every warm slot is taken by a merge-reachable
  // neighbour (Wood brown, Clay red, Copper orange, Gold yellow), so an amber ball is
  // ambiguous at Zone B size. Teal is unclaimed until Plasma (tier 17), which can never
  // share a board with tier 3. Dark matrix veins via the 'crust' pass.
  { name: 'Turquoise',  family: 'primitive', baseColor: 0x2fb3a4, accentColor: 0x3d5049, detail: 'crust',    physics: PRIMITIVE },
  { name: 'Clay',       family: 'primitive', baseColor: 0xc2503a, accentColor: 0x9c3a2a, detail: 'matte',    physics: PRIMITIVE },
  // — Metals [5,8] —
  { name: 'Copper',     family: 'metal', baseColor: 0xd47b3c, accentColor: 0xf2b27a, detail: 'sheen',  physics: METAL },
  { name: 'Iron',       family: 'metal', baseColor: 0x5d6b7a, accentColor: 0x3f4a56, detail: 'rivets', physics: METAL },
  { name: 'Steel',      family: 'metal', baseColor: 0x6e8fb5, accentColor: 0xa9c6e8, detail: 'sheen',  physics: METAL },
  { name: 'Silver',     family: 'metal', baseColor: 0xc8d2dc, accentColor: 0x8fa0ad, detail: 'sheen',  physics: METAL },
  // — Precious [9,12] —
  { name: 'Gold',       family: 'precious', baseColor: 0xf2b024, accentColor: 0xffe08a, detail: 'sheen',    physics: { ...PRECIOUS, densityMult: 1.3 } },
  { name: 'Rose gold',  family: 'precious', baseColor: 0xe08a78, accentColor: 0xf7c4b5, detail: 'sheen',    physics: PRECIOUS },
  { name: 'Obsidian',   family: 'precious', baseColor: 0x332e3b, accentColor: 0x8a6ff0, detail: 'glint',    physics: PRECIOUS },
  { name: 'Glass',      family: 'precious', baseColor: 0xbfe8f0, accentColor: 0xffffff, detail: 'crescent', physics: GEM }, // slips like a gem
  // — Gems [13,16] —
  { name: 'Sapphire',   family: 'gem', baseColor: 0x2f6fd0, accentColor: 0x7fa8ef, detail: 'facets', physics: GEM },
  { name: 'Emerald',    family: 'gem', baseColor: 0x2fae66, accentColor: 0x7fdca8, detail: 'facets', physics: GEM },
  { name: 'Ruby',       family: 'gem', baseColor: 0xd63a56, accentColor: 0xf291a5, detail: 'facets', physics: GEM },
  { name: 'Diamond',    family: 'gem', baseColor: 0xdff3fa, accentColor: 0x9fd0e8, detail: 'facets', physics: GEM },
  // — Exotic [17,20] —
  { name: 'Plasma',     family: 'exotic', baseColor: 0x3ee0d8, accentColor: 0xd8fffb, detail: 'glow',   physics: EXOTIC },
  { name: 'Magma',      family: 'exotic', baseColor: 0xff5a2d, accentColor: 0x3a1f16, detail: 'crust',  physics: EXOTIC },
  { name: 'Void',       family: 'exotic', baseColor: 0x4a3f8f, accentColor: 0xc9c2ff, detail: 'specks', physics: EXOTIC },
  { name: 'Antimatter', family: 'exotic', baseColor: 0xe94fe0, accentColor: 0xffffff, detail: 'corona', physics: EXOTIC },
];

/** Length of the authored ladder. NOT a gameplay ceiling — tiers wrap past it. */
export const MATERIAL_COUNT = MATERIALS.length;

export interface TierMaterial {
  def: MaterialDef;
  /** Completed trips around the ladder: 0 for tiers 1–20, 1 for 21–40, … The painter
   *  draws one gold glow ring per cycle so a wrapped tier never mimics its ancestor. */
  cycle: number;
}

/** Material for a tier (1-based), wrapping past the ladder end. Tiers < 1 clamp to 1. */
export function materialForTier(tier: number): TierMaterial {
  const t = Math.max(1, Math.floor(tier)) - 1;
  return { def: MATERIALS[t % MATERIAL_COUNT], cycle: Math.floor(t / MATERIAL_COUNT) };
}

/** Identity colour for a tier — kept so pre-materials call sites read one table. */
export function colorForTier(tier: number): number {
  return materialForTier(tier).def.baseColor;
}

/** 0xRRGGBB number → `#rrggbb` CSS string for the Canvas 2D API. */
export function hexColor(rgb: number): string {
  return `#${rgb.toString(16).padStart(6, '0')}`;
}

/**
 * Compact display form of a ball's value for its face: values are 3^(tier-1), so they
 * outgrow any fixed digit budget fast (tier 13 is already 531441). ≤5 characters:
 * 981 → "981", 19683 → "20K", 1594323 → "1.6M", then idle-game units up to 1e33
 * ("150Qa"), falling back to exponent form ("4e+34") beyond — wrapped-cycle tiers
 * (21+) get there in very long runs.
 */
export function compactValue(value: number): string {
  if (value < 1000) return String(value);
  const units = ['K', 'M', 'B', 'T', 'Qa', 'Qi', 'Sx', 'Sp', 'Oc', 'No'];
  let n = value;
  let unit = -1;
  while (n >= 1000 && unit < units.length - 1) {
    n /= 1000;
    unit++;
  }
  if (n >= 1000) return value.toExponential(0).replace('e+', 'e'); // "4e34"
  const body = n >= 10 ? String(Math.round(n)) : String(Math.round(n * 10) / 10);
  return body + units[unit];
}
