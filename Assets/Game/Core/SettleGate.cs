namespace RichCoast.Core
{
    /// <summary>
    /// Pure accumulator for the "board has settled" gate that arms the A→B phase pan.
    ///
    /// The buffer hits 0 the instant the last ball is RELEASED — but that ball is still falling
    /// and may cascade merges, and the pan shouldn't yank the camera mid-action. So the gate
    /// requires a contiguous hold of settled time before firing, resets the hold whenever motion
    /// resumes, and carries a hard timeout so a jittering board (balls trembling just above the
    /// rest-speed threshold) can never wedge the flow.
    ///
    /// Ported from <c>zoneA/settleGate.ts</c>. Zone A drives it once per tick with the board's
    /// settled state.
    /// </summary>
    public struct SettleGate
    {
        /// <summary>Contiguous settled time required before the gate fires (ms).</summary>
        public const float DefaultHoldMs = 350f;

        /// <summary>Hard fallback: fire regardless of motion once this much time has passed (ms).</summary>
        public const float DefaultTimeoutMs = 4000f;

        /// <summary>Contiguous settled time so far; resets to 0 on motion.</summary>
        public float HoldMs;

        /// <summary>Total time since the gate was armed; never resets.</summary>
        public float TotalMs;

        public void Reset()
        {
            HoldMs = 0f;
            TotalMs = 0f;
        }

        /// <summary>
        /// Advance by <paramref name="deltaMs"/> and report whether the gate fires this tick.
        /// </summary>
        public bool Advance(float deltaMs, bool settledNow, float holdMs = DefaultHoldMs, float timeoutMs = DefaultTimeoutMs)
        {
            HoldMs = settledNow ? HoldMs + deltaMs : 0f;
            TotalMs += deltaMs;
            return HoldMs >= holdMs || TotalMs >= timeoutMs;
        }
    }
}
