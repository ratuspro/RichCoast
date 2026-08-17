namespace RichCoast.Core
{
    /// <summary>
    /// Pure score-bar logic — no scene dependency, fully unit-testable. Ported from
    /// <c>zoneB/ScoreBar.ts</c>.
    ///
    /// Deliberately minimal: it holds the running fill toward the current target. Points
    /// accumulate through <see cref="Add"/>; whenever <see cref="CrossedTarget"/> is true the
    /// caller consumes one level with <see cref="ConsumeLevel"/> (subtracting the current target)
    /// and — via ScoreBarFilled → ProgressionChanged → <see cref="SetTarget"/> — may raise the
    /// target for the next level, then re-checks. Because levels are consumed one at a time
    /// against their own target, a single big <see cref="Add"/> can roll through several levels
    /// and land the exact remainder.
    ///
    /// No pinning / cash-in state lives here: the bar wraps LIVE as balls drain (Zone B drives
    /// the fill/empty animation), so the logic never has to hold a "full" state.
    /// </summary>
    public sealed class ScoreBar
    {
        /// <summary>Fallback target before progression sets a real one.</summary>
        public const double DefaultTarget = 10;

        private double _target;

        public ScoreBar(double target = DefaultTarget)
        {
            _target = target;
        }

        public double Filled { get; private set; }
        public double Target => _target;
        public double Progress => _target > 0 ? Filled / _target : 0;

        /// <summary>Accumulate drained score into the current level's fill.</summary>
        public void Add(double points) => Filled += points;

        /// <summary>True while the current fill has reached the target — the caller should consume a level.</summary>
        public bool CrossedTarget() => Filled >= _target;

        /// <summary>Consume one level's worth: subtract the current target from the fill.</summary>
        public void ConsumeLevel() => Filled -= _target;

        /// <summary>
        /// Safety valve: discard any fill at or above the target, leaving a nearly-full (99%) bar.
        /// The caller invokes this when a freak drain hits its levels-per-cash-in cap — the excess
        /// is forfeited so the crossing loop terminates instead of banking thousands of owed
        /// wraps. Anything already below 99% of the target is left alone.
        /// </summary>
        public void ForfeitOverflow()
        {
            if (Filled > _target * 0.99) Filled = _target * 0.99;
        }

        public void SetTarget(double target) => _target = target;

        /// <summary>Clear the bar for a fresh run.</summary>
        public void Reset(double target)
        {
            Filled = 0;
            _target = target;
        }
    }
}
