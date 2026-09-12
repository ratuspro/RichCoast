using System;

namespace RichCoast.Core
{
    /// <summary>
    /// Pure score-bar logic (port of <c>ScoreBar.ts</c>). Holds the running fill toward the current
    /// target. Points accumulate through <see cref="Add"/>; while <see cref="CrossedTarget"/> the caller
    /// consumes one level with <see cref="ConsumeLevel"/> (subtracting the current target) and — via
    /// <c>ScoreBarFilled → ProgressionChanged → SetTarget</c> — may raise the target for the next
    /// level, then re-checks. Because levels are consumed one at a time against their own target, a
    /// single big add can roll through several levels and land the exact remainder.
    /// </summary>
    public sealed class ScoreBar
    {
        public double Filled { get; private set; }
        public double Target { get; private set; }

        public ScoreBar(double target)
        {
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            Target = target;
        }

        public void Add(double points) => Filled += points;

        /// <summary>True while the current fill has reached the target — the caller should consume a level.</summary>
        public bool CrossedTarget => Filled >= Target;

        /// <summary>Consume one level's worth: subtract the current target from the fill.</summary>
        public void ConsumeLevel() => Filled -= Target;

        /// <summary>
        /// Safety valve: discard any fill at/above the target, leaving a nearly-full (99%) bar. The
        /// caller invokes this when a freak drain hits its levels-per-cash-in cap. No-op below target.
        /// </summary>
        public void ForfeitOverflow() => Filled = Math.Min(Filled, Target * 0.99);

        public void SetTarget(double target)
        {
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target));
            Target = target;
        }

        public double Progress => Filled / Target;
    }
}
