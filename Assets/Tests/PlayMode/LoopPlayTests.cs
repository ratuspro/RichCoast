using System;
using System.Collections;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>The M2 loop against the real Main scene: Zone B drain + scoring, the phase pan, and the trap-door handoff.</summary>
    public class LoopPlayTests
    {
        static IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        static GameBootstrap Boot() => UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutS)
        {
            float deadline = Time.time + timeoutS;
            while (!condition() && Time.time < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator ZoneBDrainsADroppedBallScoresItAndReportsEmpty()
        {
            yield return LoadMain();
            var boot = Boot();
            bool busy = false, empty = false;
            double total = 0;
            GameEvents.ZoneBBusy += () => busy = true;
            GameEvents.ZoneBEmpty += () => empty = true;
            GameEvents.ScoreChanged += t => total = t;

            GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(3), DesignSpace.Width / 2));
            yield return null;
            Assert.IsTrue(busy, "spawning a ball must report ZoneBBusy");
            Assert.AreEqual(1, boot.ZoneB.InFlight);

            yield return WaitUntil(() => empty, 15f);
            Assert.IsTrue(empty, $"Zone B never drained (in flight: {boot.ZoneB.InFlight}, layout {boot.ZoneB.LayoutName})");
            Assert.AreEqual(0, boot.ZoneB.InFlight);
            Assert.AreEqual(0, boot.ZoneB.BallCount);
            Assert.That(total, Is.GreaterThanOrEqualTo(TierMath.ValueForTier(3)), "every copy of a tier-3 ball scores at least its raw value");
            Assert.That(boot.ZoneA.IsOver, Is.False);
        }

        [UnityTest]
        public IEnumerator DepletingTheBufferPansToPhaseBAndTheDoorHandsABallToZoneB()
        {
            yield return LoadMain();
            var boot = Boot();
            Assert.AreEqual(GamePhase.A, boot.Phases.Phase);
            Assert.IsFalse(boot.ZoneC.IsArmed, "the door is locked in phase A");

            int buffer = boot.ZoneA.BallBuffer;
            float[] xs = { -3.5f, 3.5f, -1.5f, 1.5f, -3f, 3f, 0f, -2.5f, 2.5f, 0.5f };
            for (int i = 0; i < buffer; i++)
            {
                boot.DebugDrop(xs[i % xs.Length], 1 + i % 3);
                yield return new WaitForSeconds(0.3f);
            }
            Assert.AreEqual(0, boot.ZoneA.BallBuffer);

            yield return WaitUntil(() => boot.Phases.Phase == GamePhase.B, 12f);
            Assert.AreEqual(GamePhase.B, boot.Phases.Phase, "the buffer emptied and the board settled, so the camera should have panned to B");
            Assert.That(boot.Cam.GetComponent<CameraRig>().Pan, Is.EqualTo(1f).Within(1e-3f));
            Assert.IsTrue(boot.ZoneC.IsArmed, "the door arms in phase B");

            int before = boot.Board.BallCount;
            Assert.That(before, Is.GreaterThan(0));
            boot.ZoneC.DebugTap();
            yield return null;
            Assert.AreEqual(before - 1, boot.Board.BallCount, "the tap sucks exactly one ball off the board");
            Assert.IsFalse(boot.ZoneC.IsArmed, "the door locks the instant a tap is accepted");

            yield return WaitUntil(() => boot.ZoneB.InFlight > 0, 3f);
            Assert.That(boot.ZoneB.InFlight, Is.GreaterThan(0), "after the suck→pop the ball lands in Zone B");
        }
    }
}
