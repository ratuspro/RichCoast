namespace RichCoast.Core
{
    /// <summary>
    /// Zone A gameplay tuning — plain numbers only, no logic. Ported verbatim from the original
    /// <c>zoneA/tuning.ts</c>; <see cref="BallMath"/> turns these into the per-tier values the
    /// system reads at runtime.
    ///
    /// Design space is Layout's 390×1238 world; Zone A is the top 390×507 band (42 HUD +
    /// 465 board). In the B phase the camera top-crops the board to a 71 sliver, but these
    /// numbers are world-space and never change with the phase framing.
    /// </summary>
    public static class Tuning
    {
        // --- Geometry (design-space units) -----------------------------------

        /// <summary>
        /// The row the current/aim ball sits on, near the top. Above the death line and far
        /// enough below the 42-tall HUD bar that the largest spawnable ball (tier 4, radius 34)
        /// clears it.
        /// </summary>
        public const float SpawnY = 78f;

        /// <summary>
        /// Forgiving death line, just below the spawn row: a ball resting ABOVE this for
        /// <see cref="RestMs"/> ends the run. In the B-phase framing this row is cropped
        /// off-screen — benign, since only removals touch the board then.
        /// </summary>
        public const float DeathLineY = 108f;

        /// <summary>
        /// Base tier radii; index = tier-1, <see cref="Tiers.TierCount"/> entries. Tier-10
        /// diameter 198 &lt; 390 (fits the base arena); tier-4 radius 34 &lt; SpawnY (clears the
        /// ceiling). Low tiers are deliberately chunky so a small buffer visibly crowds the base
        /// board — early-game tension comes from board pressure, not ball count.
        /// </summary>
        public static readonly float[] Radii = { 17f, 22f, 28f, 34f, 41f, 50f, 60f, 71f, 84f, 99f };

        /// <summary>Per-tier radius multiplier beyond the base table (≈ the table's own top step).</summary>
        public const float RadiusGrowth = 1.18f;

        // --- Physics ---------------------------------------------------------

        /// <summary>Surface friction grows with tier: Base + Step*(tier-1), clamped to Max.</summary>
        public const float FrictionBase = 0.4f;
        public const float FrictionStep = 0.025f;
        public const float FrictionMax = 0.5f;

        /// <summary>Constant friction terms applied to every ball.</summary>
        public const float FrictionAir = 0.01f;
        public const float FrictionStatic = 0.1f;

        /// <summary>
        /// Surface friction of the funnel floor. Contacts combine as min(ball, floor), so this
        /// governs how readily balls slide — lower = smaller balls slide toward the apex more.
        /// </summary>
        public const float FloorFriction = 0.02f;

        /// <summary>
        /// Base density for the small tiers — mass derives from density×area. Larger balls taper
        /// below this so collision momentum between big balls grows sub-quadratically.
        /// </summary>
        public const float Density = 0.02f;

        /// <summary>Tiers at or below this keep the flat <see cref="Density"/>; larger tiers taper.</summary>
        public const int DensityTaperTier = 8;

        /// <summary>
        /// Above the taper tier, mass grows like radius^this (was ∝ r², i.e. 2). 1 = linear in
        /// radius, so a tier-20 ball ends up ~5× lighter than a flat-density one would be.
        /// </summary>
        public const float DensityMassExp = 1f;

        /// <summary>Restitution (bounciness). Modest, so balls settle but still bounce/roll a little.</summary>
        public const float Restitution = 0.2f;

        /// <summary>
        /// Physics steps per second. The original tuning is authored per STEP (a 60 Hz Matter.js
        /// step), so the per-step speed constants below convert to per-second by this factor.
        /// The fixed timestep is pinned to match.
        /// </summary>
        public const float StepsPerSecond = 60f;

        /// <summary>
        /// Downward acceleration in design units/s². The original relied on Matter.js's implicit
        /// gravity scaling, which has no Unity equivalent, so this is set by feel against the
        /// authored geometry: a ball released at the spawn row reaches the funnel floor of the base
        /// arena in a little over half a second. It is the one Zone A number that is genuinely new
        /// rather than ported, and the first knob to turn if the drop feels off.
        /// </summary>
        public const float Gravity = 2000f;

        /// <summary>Rest threshold in design units/second — <see cref="RestSpeed"/> per step.</summary>
        public const float RestSpeedPerSecond = RestSpeed * StepsPerSecond;

        /// <summary>Anti-tunnel speed ceiling in design units/second — <see cref="MaxBallSpeed"/> per step.</summary>
        public const float MaxBallSpeedPerSecond = MaxBallSpeed * StepsPerSecond;

        // --- Merge blast -----------------------------------------------------

        /// <summary>
        /// Neighbours within this radius of a merge get nudged outward. Base value at arena scale
        /// 1 — the board multiplies it by the live scale so the reach tracks ball sizes.
        /// </summary>
        public const float BlastRadius = 60f;

        /// <summary>Peak outward kick at the merge point (linear falloff to 0 at the radius).</summary>
        public const float BlastStrength = 1.6f;

        // --- Overflow / game over --------------------------------------------

        /// <summary>
        /// A body slower than this counts as "at rest". Base value at arena scale 1 — the board
        /// scales it by the live scale, since normalized gravity makes world speeds grow with it.
        /// </summary>
        public const float RestSpeed = 0.8f;

        /// <summary>
        /// Hard ceiling on a ball's per-step speed at arena scale 1, scaled by the live scale.
        /// The anti-tunnel backstop: it MUST stay below the arena wall thickness (also scaled), so
        /// a ball can never cross a wall in one step even from stacked merge-blasts. Set well
        /// above normal play speeds so it only clips runaway cases.
        /// </summary>
        public const float MaxBallSpeed = 16f;

        /// <summary>How long a ball must rest above the death line before the run ends (ms).</summary>
        public const float RestMs = 1000f;

        /// <summary>Distance below the death line within which a resting ball flags the red warning line.</summary>
        public const float WarnBand = 28f;

        // --- Zone B ----------------------------------------------------------

        /// <summary>
        /// Zone B ball radius. Deliberately tiny: many balls coexist in the cascade, and at this
        /// size colour alone carries a ball's identity.
        /// </summary>
        public const float ZoneBBallRadius = 10f;

        /// <summary>Physical thickness of a gate bar.</summary>
        public const float GateThickness = 16f;

        /// <summary>Zone B surface feel, before each material's own multipliers.</summary>
        public const float ZoneBRestitution = 0.35f;

        /// <summary>Cap on restitution after the material multiplier, so exotic tiers stay lively
        /// without ping-ponging the cascade forever.</summary>
        public const float ZoneBRestitutionMax = 0.5f;

        public const float ZoneBFriction = 0.05f;
        public const float ZoneBFrictionAir = 0.008f;

        /// <summary>
        /// Freshly-split balls ignore gates briefly, or a copy born touching the bar it was just
        /// split by would split again immediately and the cascade would run away.
        /// </summary>
        public const float SplitGraceMs = 300f;

        /// <summary>Angular spread of a split fan (radians) and how far copies are offset from the hit.</summary>
        public const float SplitSpread = 0.8f;
        public const float SplitOffset = 18f;

        /// <summary>Launch speed of a split copy, per step at 60 Hz.</summary>
        public const float SplitSpeed = 3f;

        /// <summary>
        /// Hard cap on balls in flight. A deep cascade is the point, but a budget phone cannot
        /// simulate an unbounded one — beyond this a split simply stops adding copies.
        /// </summary>
        public const int ZoneBMaxBalls = 200;

        /// <summary>Levels a single cash-in may roll through before the excess is forfeited.</summary>
        public const int MaxLevelsPerCashIn = 10;

        // --- Input -----------------------------------------------------------

        /// <summary>Brief lock-out after a drop so the next ball can't be slammed into the same spot.</summary>
        public const float DropCooldownMs = 250f;
    }
}
