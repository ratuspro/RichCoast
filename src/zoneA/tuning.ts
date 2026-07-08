/**
 * Zone A gameplay tuning — plain numbers only, no Phaser, no logic.
 *
 * Lives in `zoneA/` (not the shared `core/Layout.ts`) because this is Dev 1's
 * Zone-A feel, tweakable without touching the seam. `ballMath.ts` turns these into
 * the per-tier values the system reads at runtime.
 *
 * Design space is Layout's 390x1238 world; Zone A is the top 390x507 band (42px HUD +
 * 465px board — ~60% of the 844px screen). In the B phase the camera top-crops the board
 * to 71px on screen (HUD + board = a 113px sliver), but these numbers are world-space
 * and never change with the phase framing.
 */

// --- Geometry (design-space px) -------------------------------------------

/** The row the current/aim ball sits on, near the top. Above the death line and far
 *  enough below the HUD chrome bar (42px tall) that the largest spawnable ball (tier 4,
 *  radius 26) clears it: top edge at y=42 meets the bar, bottom at y=94 stays above the
 *  death line. */
export const SPAWN_Y = 68;

/** Forgiving death line, just below the spawn row: a ball resting ABOVE this for
 *  REST_MS ends the run (see ballMath.isRestingAbove / isOverflow). In the B-phase
 *  framing this row is cropped off-screen — benign, since only sucks (removals) touch
 *  the board then, so it can't newly overflow while invisible. */
export const DEATH_LINE_Y = 96;

/** Ball radius for the base tier table; index = tier-1. Has TIER_COUNT (10) entries.
 *  tier-10 diameter 156 < 390 (fits the base arena); tier-4 radius 26 < SPAWN_Y (clears ceiling).
 *  Merges are uncapped, so tiers past the table grow geometrically — see RADIUS_GROWTH. */
export const RADII: readonly number[] = [13, 17, 21, 26, 32, 39, 47, 56, 66, 78];

/** Per-tier radius multiplier beyond the base table (≈ the table's own top step, 78/66).
 *  radiusForTier(tier > 10) = RADII[last] * RADIUS_GROWTH^(tier-10). */
export const RADIUS_GROWTH = 1.18;

// --- Physics --------------------------------------------------------------

/** Surface friction grows with tier: FRICTION_BASE + FRICTION_STEP*(tier-1), clamped. */
export const FRICTION_BASE = 0.4;
export const FRICTION_STEP = 0.025;
export const FRICTION_MAX = 0.5;

/** Constant friction terms applied to every ball. */
export const FRICTION_AIR = 0.01;
export const FRICTION_STATIC = 0.1;

/** Uniform density — Matter derives mass from density*area, so bigger tier = heavier. */
export const DENSITY = 0.02;

/** Restitution (bounciness). Modest, so balls settle but still bounce/roll a little. */
export const RESTITUTION = 0.2;

// --- Merge blast ----------------------------------------------------------

/** Neighbours within this radius of a merge get nudged outward. Base value at arena
 *  scale 1 — Board multiplies it by the live scale so the reach tracks ball sizes. */
export const BLAST_RADIUS = 90;

/** Peak outward velocity kick at the merge point (linear falloff to 0 at the radius).
 *  Base value at arena scale 1, scaled like BLAST_RADIUS. */
export const BLAST_STRENGTH = 3.0;

// --- Overflow / game over -------------------------------------------------

/** A body whose `speed` is below this counts as "at rest". Base value at arena scale 1 —
 *  Board scales it by the live scale (normalized gravity makes world speeds grow with it). */
export const REST_SPEED = 0.8;

/** How long a ball must rest above the death line before the run ends (ms). */
export const REST_MS = 1000;

/** Px below the death line within which a resting ball flags the red warning line. */
export const WARN_BAND = 28;

// --- Input ----------------------------------------------------------------

/** Brief lock-out after a drop so the next ball can't be slammed into the same spot. */
export const DROP_COOLDOWN_MS = 250;

// --- Colour ---------------------------------------------------------------

// The tier look now lives in the shared `core/Materials` ladder (name + colours +
// physics feel per tier), so Zone A and Zone B stay pixel-identical — a transferred
// ball keeps its material. Import `materialForTier`/`colorForTier` from there.
