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
 *  radius 34) clears it: top edge at y=44 clears the bar, bottom at y=112 stays above the
 *  death line. */
export const SPAWN_Y = 78;

/** Forgiving death line, just below the spawn row: a ball resting ABOVE this for
 *  REST_MS ends the run (see ballMath.isRestingAbove / isOverflow). In the B-phase
 *  framing this row is cropped off-screen — benign, since only sucks (removals) touch
 *  the board then, so it can't newly overflow while invisible. */
export const DEATH_LINE_Y = 108;

/** Ball radius for the base tier table; index = tier-1. Has TIER_COUNT (10) entries.
 *  tier-10 diameter 198 < 390 (fits the base arena); tier-4 radius 34 < SPAWN_Y (clears ceiling).
 *  Low tiers are deliberately chunky (~30% over the original table) so a 12-ball buffer
 *  visibly crowds the base board — early-game tension comes from board pressure, not count.
 *  Merges are uncapped, so tiers past the table grow geometrically — see RADIUS_GROWTH. */
export const RADII: readonly number[] = [17, 22, 28, 34, 41, 50, 60, 71, 84, 99];

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

/** Surface friction of the funnel floor. Matter combines contacts as
 *  min(ballFriction, floorFriction), so this floor value governs how readily
 *  balls slide — lower = smaller/lighter balls slide toward the apex more. */
export const FLOOR_FRICTION = 0.02;

/** Base density for the small tiers — Matter derives mass from density*area. Larger balls
 *  taper below this (see DENSITY_TAPER_TIER / DENSITY_MASS_EXP) so their mass — and the
 *  collision momentum between big balls — grows sub-quadratically instead of ∝ r². */
export const DENSITY = 0.02;

/** Tiers at or below this keep the flat DENSITY (early/mid-game balance untouched); larger
 *  tiers taper. `densityForTier` in ballMath.ts turns this into the per-tier density. */
export const DENSITY_TAPER_TIER = 8;

/** Above the taper tier, mass grows like radius^DENSITY_MASS_EXP (was ∝ r², i.e. exp 2). 1 =
 *  linear in radius, so a tier-20 ball ends up ~5× lighter than a flat-density one would be. */
export const DENSITY_MASS_EXP = 1;

/** Restitution (bounciness). Modest, so balls settle but still bounce/roll a little. */
export const RESTITUTION = 0.2;

// --- Collision categories -------------------------------------------------

/**
 * Matter collision-filter bitmask categories for Zone A's own bodies. Zone B owns
 * `0x0001`–`0x0008` (see `zoneB/ZoneBBall.ts`), and Zone B balls mask exactly those bits —
 * so Zone A takes the *next* bits, which Zone B balls never collide with. This is what keeps
 * Zone A's boundary walls + funnel (whose thickness now scales with the arena, so at the last
 * milestone the funnel reaches hundreds of px past the Zone A/C seam into Zone B's space) from
 * catching balls that belong to Zone B. Zone A balls and walls collide only with each other,
 * never with anything in Zone B. Keep these outside the `0x0001`–`0x0008` range Zone B uses.
 */
export const CAT_ZONE_A_BALL = 0x0010;
export const CAT_ZONE_A_WALL = 0x0020;

// --- Merge blast ----------------------------------------------------------

/** Neighbours within this radius of a merge get nudged outward. Base value at arena
 *  scale 1 — Board multiplies it by the live scale so the reach tracks ball sizes. */
export const BLAST_RADIUS = 60;

/** Peak outward velocity kick at the merge point (linear falloff to 0 at the radius).
 *  Base value at arena scale 1, scaled like BLAST_RADIUS. */
export const BLAST_STRENGTH = 1.6;

// --- Overflow / game over -------------------------------------------------

/** A body whose `speed` is below this counts as "at rest". Base value at arena scale 1 —
 *  Board scales it by the live scale (normalized gravity makes world speeds grow with it). */
export const REST_SPEED = 0.8;

/** Hard ceiling on a ball's per-step speed (px/step at arena scale 1); Board scales it by the
 *  live scale, so it tracks the ×s world speeds. The anti-tunnel backstop: it MUST stay below
 *  WALL_T (the ArenaView wall thickness, also ×s), so a ball can never cross a wall in one step
 *  even from stacked merge-blasts. Set well above normal play speeds (rest 0.8, blast peak 1.6)
 *  so it only clips runaway cases and doesn't change ordinary feel. */
export const MAX_BALL_SPEED = 16;

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
