using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>Ported from the original <c>zoneA/settleGate.test.ts</c>.</summary>
    public class SettleGateTests
    {
        /// <summary>Drive the gate with fixed-delta frames; reports whether any frame fired.</summary>
        private static bool Run(ref SettleGate gate, int frames, float ms, bool settled)
        {
            var fired = false;
            for (var i = 0; i < frames; i++)
            {
                fired |= gate.Advance(ms, settled);
            }
            return fired;
        }

        [Test]
        public void FiresAfterAContiguousSettledHold()
        {
            var gate = new SettleGate();
            Assert.That(Run(ref gate, 25, 16f, true), Is.True); // 400ms
        }

        [Test]
        public void DoesNotFireBeforeTheHoldCompletes()
        {
            var gate = new SettleGate();
            Assert.That(Run(ref gate, 10, 16f, true), Is.False); // 160ms
        }

        [Test]
        public void ResetsTheHoldWhenMotionResumes()
        {
            var gate = new SettleGate();
            var fired = gate.Advance(SettleGate.DefaultHoldMs - 10f, true);
            fired |= gate.Advance(16f, false); // a merge kicked balls around — start over
            Assert.That(fired, Is.False);
            Assert.That(gate.HoldMs, Is.EqualTo(0f));

            // Needs the full hold again after the reset.
            var partial = gate;
            Assert.That(partial.Advance(SettleGate.DefaultHoldMs - 10f, true), Is.False);
            var full = gate;
            Assert.That(full.Advance(SettleGate.DefaultHoldMs, true), Is.True);
        }

        [Test]
        public void FiresAtTheHardTimeoutEvenIfTheBoardNeverSettles()
        {
            var gate = new SettleGate();
            var frames = (int)(SettleGate.DefaultTimeoutMs / 16f) + 2;
            Assert.That(Run(ref gate, frames, 16f, false), Is.True);
        }

        [Test]
        public void KeepsTotalMsAccumulatingAcrossHoldResets()
        {
            var gate = new SettleGate();
            gate.Advance(100f, true);
            gate.Advance(100f, false);
            gate.Advance(100f, true);
            Assert.That(gate.TotalMs, Is.EqualTo(300f));
            Assert.That(gate.HoldMs, Is.EqualTo(100f));
        }
    }
}
