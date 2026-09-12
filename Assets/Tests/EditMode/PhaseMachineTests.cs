using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class PhaseMachineTests
    {
        static PhaseStep Run(params PhaseInput[] inputs)
        {
            var state = PhaseMachine.Initial;
            PhaseStep last = new PhaseStep(state, null, false);
            foreach (var input in inputs)
            {
                last = PhaseMachine.Step(state, input);
                state = last.State;
            }
            return last;
        }

        [Test]
        public void StartsInPhaseA()
        {
            Assert.AreEqual(GamePhase.A, PhaseMachine.Initial.Phase);
            Assert.IsFalse(PhaseMachine.Initial.RefillQueued);
        }

        [Test]
        public void DepletedInAStartsPanDown()
        {
            var step = Run(PhaseInput.Depleted);
            Assert.AreEqual(GamePhase.AToB, step.State.Phase);
            Assert.AreEqual(GamePhase.B, step.StartPan);
            Assert.IsTrue(step.Changed);
        }

        [Test]
        public void PanDoneLandsInB()
        {
            var step = Run(PhaseInput.Depleted, PhaseInput.PanDone);
            Assert.AreEqual(GamePhase.B, step.State.Phase);
            Assert.IsNull(step.StartPan);
            Assert.IsTrue(step.Changed);
        }

        [Test]
        public void BarFilledInBPansUp()
        {
            var step = Run(PhaseInput.Depleted, PhaseInput.PanDone, PhaseInput.BarFilled);
            Assert.AreEqual(GamePhase.BToA, step.State.Phase);
            Assert.AreEqual(GamePhase.A, step.StartPan);
            var landed = PhaseMachine.Step(step.State, PhaseInput.PanDone);
            Assert.AreEqual(GamePhase.A, landed.State.Phase);
            Assert.IsTrue(landed.Changed);
        }

        [Test]
        public void BarFilledInAIsIgnored()
        {
            var step = Run(PhaseInput.BarFilled);
            Assert.AreEqual(GamePhase.A, step.State.Phase);
            Assert.IsNull(step.StartPan);
            Assert.IsFalse(step.Changed);
        }

        [Test]
        public void BarFilledWhilePanningDownQueuesAndBouncesBack()
        {
            var queued = Run(PhaseInput.Depleted, PhaseInput.BarFilled);
            Assert.AreEqual(GamePhase.AToB, queued.State.Phase);
            Assert.IsTrue(queued.State.RefillQueued);
            Assert.IsFalse(queued.Changed);
            var landed = PhaseMachine.Step(queued.State, PhaseInput.PanDone);
            Assert.AreEqual(GamePhase.BToA, landed.State.Phase);
            Assert.AreEqual(GamePhase.A, landed.StartPan);
            Assert.IsFalse(landed.State.RefillQueued);
        }

        [Test]
        public void DepletedOutsideAIsIgnored()
        {
            var step = Run(PhaseInput.Depleted, PhaseInput.PanDone, PhaseInput.Depleted);
            Assert.AreEqual(GamePhase.B, step.State.Phase);
            Assert.IsFalse(step.Changed);
        }

        [Test]
        public void StrayPanDoneInAOrBIsIgnored()
        {
            Assert.IsFalse(Run(PhaseInput.PanDone).Changed);
            Assert.IsFalse(Run(PhaseInput.Depleted, PhaseInput.PanDone, PhaseInput.PanDone).Changed);
        }
    }
}
