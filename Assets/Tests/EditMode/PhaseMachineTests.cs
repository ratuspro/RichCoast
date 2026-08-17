using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>Ported from the original <c>core/phaseMachine.test.ts</c>.</summary>
    public class PhaseMachineTests
    {
        private static PhaseMachine At(GamePhase phase, bool refillQueued = false)
        {
            var machine = new PhaseMachine();
            switch (phase)
            {
                case GamePhase.A:
                    break;
                case GamePhase.AToB:
                    machine.Step(PhaseInput.Depleted);
                    break;
                case GamePhase.B:
                    machine.Step(PhaseInput.Depleted);
                    machine.Step(PhaseInput.PanDone);
                    break;
                case GamePhase.BToA:
                    machine.Step(PhaseInput.Depleted);
                    machine.Step(PhaseInput.PanDone);
                    machine.Step(PhaseInput.BarFilled);
                    break;
            }
            if (refillQueued) machine.Step(PhaseInput.BarFilled);
            Assert.That(machine.Phase, Is.EqualTo(phase), "test helper failed to reach the requested phase");
            return machine;
        }

        [Test]
        public void StartsInTheAPhaseWithNoQueuedRefill()
        {
            var machine = new PhaseMachine();
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.A));
            Assert.That(machine.RefillQueued, Is.False);
        }

        [Test]
        public void RunsTheFullLoop()
        {
            var machine = new PhaseMachine();

            var step = machine.Step(PhaseInput.Depleted);
            Assert.That(step.Changed, Is.True);
            Assert.That(step.StartPan, Is.EqualTo(GamePhase.B));
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.AToB));

            step = machine.Step(PhaseInput.PanDone);
            Assert.That(step.Changed, Is.True);
            Assert.That(step.StartPan, Is.Null);
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.B));

            step = machine.Step(PhaseInput.BarFilled);
            Assert.That(step.Changed, Is.True);
            Assert.That(step.StartPan, Is.EqualTo(GamePhase.A));
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.BToA));

            step = machine.Step(PhaseInput.PanDone);
            Assert.That(step.Changed, Is.True);
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.A));
        }

        [Test]
        public void IgnoresDepletedOutsideTheAPhase()
        {
            foreach (var phase in new[] { GamePhase.AToB, GamePhase.B, GamePhase.BToA })
            {
                var machine = At(phase);
                var step = machine.Step(PhaseInput.Depleted);
                Assert.That(step.Changed, Is.False);
                Assert.That(step.StartPan, Is.Null);
                Assert.That(machine.Phase, Is.EqualTo(phase));
            }
        }

        [Test]
        public void IgnoresBarFilledInAAndWhileAlreadyPanningUp()
        {
            foreach (var phase in new[] { GamePhase.A, GamePhase.BToA })
            {
                var machine = At(phase);
                var step = machine.Step(PhaseInput.BarFilled);
                Assert.That(step.Changed, Is.False, $"phase {phase}");
                Assert.That(step.StartPan, Is.Null, $"phase {phase}");
                Assert.That(machine.Phase, Is.EqualTo(phase));
            }
        }

        [Test]
        public void QueuesABarFilledDuringTheDownwardPanAndTurnsStraightAroundOnLanding()
        {
            var machine = At(GamePhase.AToB);

            var queued = machine.Step(PhaseInput.BarFilled);
            Assert.That(queued.Changed, Is.False);
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.AToB));
            Assert.That(machine.RefillQueued, Is.True);

            var landed = machine.Step(PhaseInput.PanDone);
            Assert.That(landed.Changed, Is.True);
            Assert.That(landed.StartPan, Is.EqualTo(GamePhase.A));
            Assert.That(machine.Phase, Is.EqualTo(GamePhase.BToA), "B is never observable here");
            Assert.That(machine.RefillQueued, Is.False);
        }

        [Test]
        public void IgnoresPanDoneInTheSettledPhases()
        {
            foreach (var phase in new[] { GamePhase.A, GamePhase.B })
            {
                var machine = At(phase);
                var step = machine.Step(PhaseInput.PanDone);
                Assert.That(step.Changed, Is.False);
                Assert.That(machine.Phase, Is.EqualTo(phase));
            }
        }
    }
}
