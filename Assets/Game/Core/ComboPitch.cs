using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>
    /// Combo pitch-rise: the pure timing rule shared by Zone A merges and Zone B multiplications.
    /// Triggering a channel again within the window continues a chain and steps the pitch up; a
    /// longer gap resets it. Ported from <c>core/comboPitch.ts</c>.
    ///
    /// Audio-engine-free, so it unit-tests on its own — the audio service just feeds it the
    /// current time and applies the returned multiplier to a frequency.
    /// </summary>
    public struct ComboPitch
    {
        public const int DefaultMaxStep = 8;

        /// <summary>Time (ms) of the last trigger; negative infinity until the first one.</summary>
        private float _lastAt;

        /// <summary>Current step in the chain (0 = base pitch).</summary>
        public int Step { get; private set; }

        public static ComboPitch New() => new ComboPitch { _lastAt = float.NegativeInfinity, Step = 0 };

        /// <summary>Frequency multiplier for the current step: one equal-tempered semitone per step.</summary>
        public float Multiplier => Mathf.Pow(2f, Step / 12f);

        /// <summary>
        /// Advance the channel: increment the step if <paramref name="nowMs"/> is within
        /// <paramref name="windowMs"/> of the previous trigger, else reset to 0. The step is
        /// capped so a long chain never gets shrill.
        /// </summary>
        public float Next(float nowMs, float windowMs, int maxStep = DefaultMaxStep)
        {
            var within = nowMs - _lastAt < windowMs;
            Step = within ? Mathf.Min(Step + 1, maxStep) : 0;
            _lastAt = nowMs;
            return Multiplier;
        }
    }
}
