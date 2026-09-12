namespace RichCoast.Core
{
    /// <summary>What the phase machine is fed (port of <c>phaseMachine.ts</c>).</summary>
    public enum PhaseInput
    {
        /// <summary>Zone A's buffer hit 0 and the board settled → A begins panning down.</summary>
        Depleted,
        /// <summary>Zone B's score bar cashed in → B begins panning up.</summary>
        BarFilled,
        /// <summary>The camera tween landed → arrive at the target phase.</summary>
        PanDone,
    }

    public readonly struct PhaseState
    {
        public readonly GamePhase Phase;
        /// <summary>A cash-in arrived while panning down; bounce back up as soon as we land in B.</summary>
        public readonly bool RefillQueued;

        public PhaseState(GamePhase phase, bool refillQueued)
        {
            Phase = phase;
            RefillQueued = refillQueued;
        }
    }

    public readonly struct PhaseStep
    {
        public readonly PhaseState State;
        /// <summary>Start the camera pan toward this phase (A or B); null = no new pan.</summary>
        public readonly GamePhase? StartPan;
        /// <summary>The phase changed — broadcast <c>PhaseChanged</c> with <see cref="State"/>.Phase.</summary>
        public readonly bool Changed;

        public PhaseStep(PhaseState state, GamePhase? startPan, bool changed)
        {
            State = state;
            StartPan = startPan;
            Changed = changed;
        }
    }

    /// <summary>
    /// Pure state machine for the two-phase flow. Quirks encoded here (and covered by tests):
    /// <c>BarFilled</c> in the A phase is IGNORED as a pan trigger (a cash-in can happen while already
    /// in A — Zone A just refills in place); <c>BarFilled</c> during A→B can't turn the pan around
    /// mid-tween — it queues, and the machine bounces straight back (B → B→A) the moment the
    /// downward pan lands.
    /// </summary>
    public static class PhaseMachine
    {
        public static PhaseState Initial => new PhaseState(GamePhase.A, false);

        public static PhaseStep Step(PhaseState state, PhaseInput input)
        {
            var phase = state.Phase;
            bool queued = state.RefillQueued;
            switch (input)
            {
                case PhaseInput.Depleted:
                    if (phase != GamePhase.A) return Same(state);
                    return new PhaseStep(new PhaseState(GamePhase.AToB, queued), GamePhase.B, true);

                case PhaseInput.BarFilled:
                    if (phase == GamePhase.B) return new PhaseStep(new PhaseState(GamePhase.BToA, false), GamePhase.A, true);
                    if (phase == GamePhase.AToB) return new PhaseStep(new PhaseState(phase, true), null, false);
                    return Same(state); // in A (refill in place) or already panning up

                case PhaseInput.PanDone:
                    if (phase == GamePhase.AToB)
                    {
                        if (queued) return new PhaseStep(new PhaseState(GamePhase.BToA, false), GamePhase.A, true);
                        return new PhaseStep(new PhaseState(GamePhase.B, queued), null, true);
                    }
                    if (phase == GamePhase.BToA) return new PhaseStep(new PhaseState(GamePhase.A, queued), null, true);
                    return Same(state);
            }
            return Same(state);
        }

        static PhaseStep Same(PhaseState state) => new PhaseStep(state, null, false);
    }
}
