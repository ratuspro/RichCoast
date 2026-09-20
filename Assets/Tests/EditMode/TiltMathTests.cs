using System;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    /// <summary>
    /// The cabinet tilt's pulse train. Three properties carry the whole mechanic, and two of them are
    /// regressions the first build actually shipped into a PlayMode run: the train must NET TO ZERO
    /// (a decaying alternation walks the whole board sideways, a little further every tilt), the kick
    /// must VARY WITH POSITION (an identical delta on every ball is a rigid translation — the pile
    /// slides and keeps its arrangement, so no orphan ever meets a partner), and the lurches DECAY so
    /// the last one settles the board rather than launching it.
    /// </summary>
    public class TiltMathTests
    {
        [Test]
        public void LurchesAlternateDirection()
        {
            Assert.AreEqual(1.0, TiltMath.PulseSignX(0));
            Assert.AreEqual(-1.0, TiltMath.PulseSignX(1));
            Assert.AreEqual(1.0, TiltMath.PulseSignX(2));
            Assert.AreEqual(-1.0, TiltMath.PulseSignX(3));
        }

        [Test]
        public void StrengthDecaysAcrossTheTrainButNeverToNothing()
        {
            const int pulses = 4;
            double previous = double.MaxValue;
            for (int i = 0; i < pulses; i++)
            {
                double falloff = TiltMath.PulseFalloff(i, pulses);
                Assert.Less(falloff, previous, $"pulse {i} did not decay");
                Assert.GreaterOrEqual(falloff, TiltMath.MinFalloff - 1e-9);
                previous = falloff;
            }
            Assert.AreEqual(1.0, TiltMath.PulseFalloff(0, pulses), 1e-9);
            Assert.AreEqual(TiltMath.MinFalloff, TiltMath.PulseFalloff(pulses - 1, pulses), 1e-9);
        }

        /// <summary>A one-pulse train is a legal setting, and it should hit at full strength, not zero.</summary>
        [Test]
        public void ASinglePulseHitsFull()
        {
            Assert.AreEqual(1.0, TiltMath.PulseFalloff(0, 1), 1e-9);
            Assert.AreEqual(1.0, TiltMath.PulseAmplitude(0, 1), 1e-9);
            var kick = TiltMath.Kick(0, 1, x: 0, y: 0, slide: 3.0, rock: 0, lift: 2.0);
            Assert.AreEqual(3.0, kick.X, 1e-9);
            Assert.AreEqual(2.0, kick.Y, 1e-9);
        }

        /// <summary>
        /// THE regression test. A decaying alternation (+1, -0.7, +0.4) nets a shove in the first
        /// lurch's direction, and the whole board walks that way a little further with every tilt —
        /// which is exactly what the first build did. The train must balance.
        /// </summary>
        [Test]
        public void TheLurchTrainNetsToZeroSoTheBoardNeverWalks()
        {
            for (int pulses = 2; pulses <= 8; pulses++)
            {
                double sum = 0;
                for (int i = 0; i < pulses; i++) sum += TiltMath.PulseAmplitude(i, pulses);
                Assert.AreEqual(0.0, sum, 1e-9, $"a {pulses}-lurch train leaves a net shove of {sum:0.###}");
            }
        }

        [Test]
        public void TheTrainStillAlternatesAfterTheCorrection()
        {
            const int pulses = 4;
            for (int i = 0; i < pulses; i++)
                Assert.AreEqual(TiltMath.PulseSignX(i), Math.Sign(TiltMath.PulseAmplitude(i, pulses)),
                    $"mean-correction flipped lurch {i}");
        }

        /// <summary>
        /// The other half of why the first build did nothing: an identical delta on every ball is a
        /// rigid translation. The kick must DIFFER with position, or the pile keeps its arrangement
        /// and no orphan ever finds a partner.
        /// </summary>
        [Test]
        public void BallsAtDifferentHeightsGetDifferentKicks()
        {
            var low = TiltMath.Kick(0, 3, x: 0, y: 0.5, slide: 7, rock: 1.6, lift: 7);
            var high = TiltMath.Kick(0, 3, x: 0, y: 4.0, slide: 7, rock: 1.6, lift: 7);
            Assert.AreNotEqual(low.X, high.X);
            Assert.Greater(Math.Abs(low.X - high.X), 1.0,
                "the shear is too small to shuffle a pile");
        }

        /// <summary>The rock is a rotation about the apex, so the two flanks lift in opposite directions.</summary>
        [Test]
        public void TheTwoFlanksRockInOppositeDirections()
        {
            var left = TiltMath.Kick(0, 3, x: -2.0, y: 0.6, slide: 7, rock: 1.6, lift: 0);
            var right = TiltMath.Kick(0, 3, x: 2.0, y: 0.6, slide: 7, rock: 1.6, lift: 0);
            Assert.AreEqual(-left.Y, right.Y, 1e-9);
            Assert.AreNotEqual(0.0, right.Y);
        }

        /// <summary>With no rock the tilt degenerates to the rigid translation that does not work.</summary>
        [Test]
        public void ZeroRockIsAPureSlide()
        {
            var a = TiltMath.Kick(1, 3, x: -2.0, y: 0.6, slide: 7, rock: 0, lift: 7);
            var b = TiltMath.Kick(1, 3, x: 3.0, y: 2.4, slide: 7, rock: 0, lift: 7);
            Assert.AreEqual(a.X, b.X, 1e-9);
            Assert.AreEqual(a.Y, b.Y, 1e-9);
        }

        /// <summary>
        /// The lift does not alternate — every lurch throws the board upward. That is deliberate: it is
        /// what pops a ball out of the pocket its neighbours make, and it is the gamble.
        /// </summary>
        [Test]
        public void EveryLurchPushesUpwardsWhateverItsDirection()
        {
            for (int i = 0; i < 5; i++)
            {
                var kick = TiltMath.Kick(i, 5, x: 0, y: 0.6, slide: 7, rock: 0, lift: 7);
                Assert.Greater(kick.Y, 0.0, $"lurch {i} does not lift");
            }
        }

        [Test]
        public void TheSlideFollowsTheSignedAmplitude()
        {
            const int pulses = 3;
            for (int i = 0; i < pulses; i++)
            {
                var kick = TiltMath.Kick(i, pulses, x: 0, y: 0, slide: 4.0, rock: 0, lift: 0);
                Assert.AreEqual(TiltMath.PulseAmplitude(i, pulses) * 4.0, kick.X, 1e-9);
            }
        }
    }
}
