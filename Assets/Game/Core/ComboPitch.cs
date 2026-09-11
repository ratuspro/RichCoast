using System;

namespace RichCoast.Core
{
    /// <summary>
    /// Combo pitch-rise: triggering a channel again within <c>windowMs</c> of the previous hit
    /// continues a chain and steps the pitch up one equal-tempered semitone; a longer gap resets.
    /// Engine-free — the audio layer feeds it a clock and applies the multiplier to a pitch.
    /// </summary>
    public sealed class ComboState
    {
        /// <summary>Time (ms) of the last trigger, or -∞ until the first one.</summary>
        public double LastAt = double.NegativeInfinity;
        /// <summary>Current step in the chain (0 = base pitch).</summary>
        public int Step;
    }

    public readonly struct ComboResult
    {
        public readonly int Step;
        /// <summary>Frequency multiplier: 2^(step/12).</summary>
        public readonly double Mult;

        public ComboResult(int step, double mult)
        {
            Step = step;
            Mult = mult;
        }
    }

    public static class ComboPitch
    {
        /// <summary>
        /// Advance a combo channel. Mutates <paramref name="state"/>: increments the step if
        /// <paramref name="nowMs"/> is within <paramref name="windowMs"/> of the previous trigger, else
        /// resets to 0. Capped at <paramref name="maxStep"/> so a long chain never gets shrill.
        /// </summary>
        public static ComboResult Next(ComboState state, double nowMs, double windowMs, int maxStep = 8)
        {
            bool within = nowMs - state.LastAt < windowMs;
            state.Step = within ? Math.Min(state.Step + 1, maxStep) : 0;
            state.LastAt = nowMs;
            return new ComboResult(state.Step, Math.Pow(2, state.Step / 12.0));
        }
    }
}
