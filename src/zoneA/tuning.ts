/**
 * Zone A gameplay tuning — plain numbers only, no Phaser, no logic.
 *
 * Lives in `zoneA/` (not the shared `core/Layout.ts`) because this is Dev 1's
 * Zone-A feel, tweakable without touching the seam. `ballMath.ts` turns these into
 * the per-tier values the system reads at runtime.
 *
 * Design space is Layout's 390x844; Zone A is the top 390x448 band.
 */

// --- Geometry (design-space px) -------------------------------------------

/** The row the current/aim ball sits on, near the top. Above the death line and
 *  far enough below the y=0 ceiling that the largest spawnable ball (tier 4) clears it. */
export const SPAWN_Y = 56;

/** Forgiving death line, just below the spawn row: a ball resting ABOVE this for
 *  REST_MS ends the run (see ballMath.isRestingAbove / isOverflow). */
export const DEATH_LINE_Y = 96;

/** Ball radius per tier; index = tier-1. Must have TIER_COUNT (10) entries.
 *  tier-10 diameter 156 < 390 (fits across); tier-4 radius 26 < SPAWN_Y (clears ceiling). */
export const RADII: readonly number[] = [13, 17, 21, 26, 32, 39, 47, 56, 66, 78];

// --- Physics --------------------------------------------------------------

/** Surface friction grows with tier: FRICTION_BASE + FRICTION_STEP*(tier-1), clamped. */
export const FRICTION_BASE = 0.08;
export const FRICTION_STEP = 0.025;
export const FRICTION_MAX = 0.45;

/** Constant friction terms applied to every ball. */
export const FRICTION_AIR = 0.01;
export const FRICTION_STATIC = 0.6;

/** Uniform density — Matter derives mass from density*area, so bigger tier = heavier. */
export const DENSITY = 0.001;

/** Restitution (bounciness). Modest, so balls settle but still bounce/roll a little. */
export const RESTITUTION = 0.1;

// --- Merge blast ----------------------------------------------------------

/** Neighbours within this radius of a merge get nudged outward. */
export const BLAST_RADIUS = 90;

/** Peak outward velocity kick at the merge point (linear falloff to 0 at the radius). */
export const BLAST_STRENGTH = 3.0;

// --- Overflow / game over -------------------------------------------------

/** A body whose `speed` is below this counts as "at rest". */
export const REST_SPEED = 0.8;

/** How long a ball must rest above the death line before the run ends (ms). */
export const REST_MS = 1000;

// --- Input ----------------------------------------------------------------

/** Brief lock-out after a drop so the next ball can't be slammed into the same spot. */
export const DROP_COOLDOWN_MS = 250;

// --- Colour ---------------------------------------------------------------

/** One flat hue per tier; index = tier-1. Must have TIER_COUNT (10) entries.
 *  High-contrast geometric palette (SPEC.md visual theme), no image assets. */
export const TIER_COLORS: readonly number[] = [
  0x4cc9f0, 0x4895ef, 0x4361ee, 0x3f37c9, 0x7209b7, 0xb5179e, 0xf72585, 0xff6d00,
  0xffba08, 0x80ed99,
];
