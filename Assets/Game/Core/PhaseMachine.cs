namespace RichCoast.Core
{
    /// <summary>What the phase machine wants the caller to do after an input.</summary>
    public readonly struct PhaseStep
    {
        /// <summary>The phase changed — broadcast <see cref="PhaseChanged"/> with <see cref="Phase"/>.</summary>
        public readonly bool Changed;

        public readonly GamePhase Phase;

        /// <summary>Start the camera pan toward this phase; null = no new pan.</summary>
        public readonly GamePhase? StartPan;

        public PhaseStep(GamePhase phase, bool changed, GamePhase? startPan = null)
        {
            Phase = phase;
            Changed = changed;
            StartPan = startPan;
        }
    }

    public enum PhaseInput
    {
        /// <summary>Zone A's buffer hit 0 and the board settled → A begins panning down.</summary>
        Depleted,

        /// <summary>Zone B's score bar cashed in → B begins panning up.</summary>
        BarFilled,

        /// <summary>The camera tween landed → arrive at the target phase.</summary>
        PanDone,
    }

    /// <summary>
    /// Pure state machine for the two-phase flow. Ported from <c>core/phaseMachine.ts</c>.
    ///
    /// Quirks encoded here (and covered by tests):
    ///  - BarFilled in the A phase is IGNORED as a pan trigger: a cash-in can happen while already
    ///    in A (e.g. milestone-drain balls fill the bar) — Zone A just refills in place.
    ///  - BarFilled during AToB can't turn the pan around mid-tween; it queues, and the machine
    ///    bounces straight back (B → BToA) the moment the downward pan lands.
    /// </summary>
    public sealed class PhaseMachine
    {
        public GamePhase Phase { get; private set; } = GamePhase.A;

        /// <summary>A cash-in arrived while panning down; bounce back up as soon as we land in B.</summary>
        public bool RefillQueued { get; private set; }

        public void Reset()
        {
            Phase = GamePhase.A;
            RefillQueued = false;
        }

        public PhaseStep Step(PhaseInput input)
        {
            switch (input)
            {
                case PhaseInput.Depleted:
                    if (Phase != GamePhase.A) return Unchanged();
                    Phase = GamePhase.AToB;
                    return new PhaseStep(Phase, true, GamePhase.B);

                case PhaseInput.BarFilled:
                    if (Phase == GamePhase.B)
                    {
                        Phase = GamePhase.BToA;
                        RefillQueued = false;
                        return new PhaseStep(Phase, true, GamePhase.A);
                    }
                    if (Phase == GamePhase.AToB)
                    {
                        RefillQueued = true;
                        return Unchanged();
                    }
                    return Unchanged(); // in A (refill in place) or already panning up

                case PhaseInput.PanDone:
                    if (Phase == GamePhase.AToB)
                    {
                        if (RefillQueued)
                        {
                            // Landed in B with a refill already banked — turn straight around.
                            // B is never observable here; the pan up starts the same tick.
                            Phase = GamePhase.BToA;
                            RefillQueued = false;
                            return new PhaseStep(Phase, true, GamePhase.A);
                        }
                        Phase = GamePhase.B;
                        return new PhaseStep(Phase, true);
                    }
                    if (Phase == GamePhase.BToA)
                    {
                        Phase = GamePhase.A;
                        return new PhaseStep(Phase, true);
                    }
                    return Unchanged();

                default:
                    return Unchanged();
            }
        }

        private PhaseStep Unchanged() => new PhaseStep(Phase, false);
    }
}
