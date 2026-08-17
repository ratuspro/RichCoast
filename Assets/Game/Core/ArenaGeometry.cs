namespace RichCoast.Core
{
    /// <summary>
    /// Zone A's boundary geometry at a given arena scale — pure numbers in design space.
    ///
    /// Because merges are uncapped, balls grow forever; the arena grows with them at milestones
    /// while the camera zooms out by 1/scale, so a window-max ball keeps a constant apparent size.
    /// Everything is anchored to the FUNNEL FLOOR (the Zone A/C boundary), which never moves: the
    /// arena grows upward and outward only, never into Zone B, and the camera's floor-pinned
    /// centring keeps the seam still while it happens.
    ///
    /// Since the world scales but ball radii do not, every distance authored in
    /// <see cref="Tuning"/> — the spawn row, the death line, the blast reach — is measured in
    /// arena units and multiplied by the scale here.
    /// </summary>
    public readonly struct ArenaGeometry
    {
        /// <summary>
        /// Boundary wall thickness at scale 1. Must stay above <see cref="Tuning.MaxBallSpeed"/>
        /// (both scale together), so a ball can never cross a wall within one physics step.
        /// </summary>
        public const float WallThickness = 40f;

        /// <summary>Arena scale: 1 = the base arena; each milestone multiplies it.</summary>
        public readonly float Scale;

        public ArenaGeometry(float scale)
        {
            Scale = scale;
        }

        /// <summary>The funnel floor — the fixed anchor everything else is measured from.</summary>
        public float FloorY => Layout.ZoneA.Bottom;

        /// <summary>Interior height: the A-phase viewport, scaled.</summary>
        public float Height => PhaseGeometry.ArenaViewHeightA * Scale;

        /// <summary>Interior width: the design width, scaled.</summary>
        public float Width => Layout.Width * Scale;

        public float CeilingY => FloorY - Height;
        public float CenterX => Layout.Width * 0.5f;
        public float MinX => CenterX - Width * 0.5f;
        public float MaxX => CenterX + Width * 0.5f;

        /// <summary>The row the aim ball sits on, measured down from the ceiling.</summary>
        public float SpawnY => CeilingY + Tuning.SpawnY * Scale;

        /// <summary>The overflow line, measured down from the ceiling.</summary>
        public float DeathLineY => CeilingY + Tuning.DeathLineY * Scale;

        /// <summary>Warning band below the death line, scaled.</summary>
        public float WarnBand => Tuning.WarnBand * Scale;

        /// <summary>
        /// Physics feel normalised to the scale: gravity and the speed thresholds all grow with
        /// the arena, so a drop looks and settles identically at every milestone.
        /// </summary>
        public float Gravity => Tuning.Gravity * Scale;

        public float RestSpeed => Tuning.RestSpeedPerSecond * Scale;
        public float MaxSpeed => Tuning.MaxBallSpeedPerSecond * Scale;
        public float BlastRadius => Tuning.BlastRadius * Scale;
        public float BlastStrength => Tuning.BlastStrength * Scale;
        public float ScaledWallThickness => WallThickness * Scale;

        /// <summary>Clamp a drop x so a ball of <paramref name="radius"/> spawns clear of both walls.</summary>
        public float ClampSpawnX(float x, float radius) => BallMath.ClampSpawnX(x, radius, MinX, MaxX);
    }
}
