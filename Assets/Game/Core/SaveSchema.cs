namespace RichCoast.Core
{
    /// <summary>
    /// The save format's contract — the counterpart to <see cref="ZoneBGenerator"/>'s Validate.
    /// A snapshot that passes MUST rebuild into a playable board; anything else is discarded (the run
    /// ALONE, never the records or settings).
    /// <para>Deliberately permissive where a false rejection would cost a player a legitimate run —
    /// notably, merging climbs ABOVE the draw window, so only the window's floor is a bound — and
    /// strict only about what would actually break restore.</para>
    /// </summary>
    public static class SaveSchema
    {
        /// <summary>Bump on any breaking model change. A mismatch drops the run and keeps the records.</summary>
        public const int Current = 1;

        /// <summary>Arena growth only ever multiplies up; the ceiling is far past any reachable level.</summary>
        public const float MinArenaScale = 1f, MaxArenaScale = 1e6f;

        /// <summary>A loose bound, not the level's real capacity: rejecting a valid run is worse than accepting an odd buffer.</summary>
        const int MaxBuffer = 1000;

        public static bool ValidateRun(RunSnapshot run, ProgressionCurve curve, out string reason)
        {
            reason = null;
            if (run == null) { reason = "no run"; return false; }
            if (curve == null) { reason = "no progression curve"; return false; }

            if (run.level < 1) { reason = $"level {run.level} < 1"; return false; }
            if (!Finite(run.score) || run.score < 0) { reason = $"bad score {run.score}"; return false; }
            if (run.ballBuffer < 0 || run.ballBuffer > MaxBuffer) { reason = $"bad ballBuffer {run.ballBuffer}"; return false; }
            if (!Finite(run.arenaScale) || run.arenaScale < MinArenaScale || run.arenaScale > MaxArenaScale)
            { reason = $"bad arenaScale {run.arenaScale}"; return false; }
            if (!Finite(run.barTarget) || run.barTarget <= 0) { reason = $"bad barTarget {run.barTarget}"; return false; }
            if (!Finite(run.barFilled) || run.barFilled < 0 || run.barFilled >= run.barTarget)
            { reason = $"barFilled {run.barFilled} not in [0, {run.barTarget})"; return false; }
            if (run.currentTier < 1 || run.nextTier < 1)
            { reason = $"bad queue tiers {run.currentTier}/{run.nextTier}"; return false; }
            if (run.board == null) { reason = "null board list"; return false; }

            // Only the FLOOR bounds a ball's tier: merging deliberately climbs above the draw window,
            // while anything below the floor would have been taken by the milestone blacklist drain.
            int floor = curve.WindowForLevel(run.level).min;
            for (int i = 0; i < run.board.Count; i++)
            {
                var b = run.board[i];
                if (b == null) { reason = $"null ball at {i}"; return false; }
                if (b.tier < floor) { reason = $"ball {i} tier {b.tier} below window floor {floor}"; return false; }
                if (!Finite(b.x) || b.x < 0 || b.x > DesignSpace.Width)
                { reason = $"ball {i} x {b.x} off board"; return false; }
                if (!Finite(b.yFromTop) || b.yFromTop < 0 || b.yFromTop > DesignSpace.BoardHeight)
                { reason = $"ball {i} yFromTop {b.yFromTop} off board"; return false; }
            }
            return true;
        }

        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
