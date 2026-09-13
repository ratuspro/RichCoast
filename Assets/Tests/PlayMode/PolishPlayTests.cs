using System;
using System.Collections;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using RichCoast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>The M4 polish beats against the real Main scene: buffer-refill particles, the cash-in drain-out, the phase ribbon.</summary>
    public class PolishPlayTests
    {
        static IEnumerator LoadMain()
        {
            // The scene boots to the title unless told otherwise; these tests all want a live run.
            GameBootstrap.PendingIntent = AppIntent.NewRun;
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
        public IEnumerator ARefillFliesOneDotPerSlotAndLandsEachSlotAfterItsFlight()
        {
            yield return LoadMain();
            var boot = Boot();
            int before = boot.ZoneA.BallBuffer;
            int launched = 0;
            GameEvents.BufferSlotLaunched += _ => launched++;

            // A cash-in while already in phase A refills in place: level 1 → 2 grows the buffer.
            GameEvents.RaiseScoreBarFilled();
            int capacity = ProgressionCurve.BufferForLevel(boot.ZoneA.Level);
            int slots = capacity - before;
            Assert.That(slots, Is.GreaterThan(0), "level 2 must hold more balls than level 1 for this test to mean anything");

            yield return WaitUntil(() => launched == slots, 5f);
            Assert.AreEqual(slots, launched, "one particle launches per refilled slot");
            Assert.That(boot.ZoneA.BallBuffer, Is.LessThan(capacity), "the last slot is still in flight the moment it launches");
            Assert.IsNotNull(GameObject.Find("BufferDot"), "a brass dot flies on the overlay while a slot is in flight");

            yield return WaitUntil(() => boot.ZoneA.BallBuffer == capacity, 3f);
            Assert.AreEqual(capacity, boot.ZoneA.BallBuffer, "every slot lands after its flight");
            yield return new WaitForSeconds(0.3f);
            Assert.IsNull(GameObject.Find("BufferDot"), "dots are destroyed on arrival");
        }

        [UnityTest]
        public IEnumerator TheRoundsCashInFiresOnlyAfterTheBarHoldsFullThenDrainsOut()
        {
            yield return LoadMain();
            var boot = Boot();
            var fill = GameObject.Find("BarFill").transform;
            float full = GameObject.Find("BarGroove").transform.localScale.x;
            int wraps = 0;
            GameEvents.ScoreBarFilled += () => wraps++;
            float cashedInAt = -1f, fillAtCashIn = -1f;
            GameEvents.ScoreBarCashedIn += () => { cashedInAt = Time.time; fillAtCashIn = fill.localScale.x; };

            // A tier-8 ball rolls the level-1 bar through several targets in one drain.
            GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(8), DesignSpace.Width / 2));

            // Track the final contiguous stretch the bar spends at full before the cash-in fires.
            float runStart = -1f, lastFullAt = -1f;
            bool wasFull = false;
            float deadline = Time.time + 20f;
            while (cashedInAt < 0f && Time.time < deadline)
            {
                bool isFull = fill.localScale.x >= 0.98f * full;
                if (isFull && !wasFull) runStart = Time.time;
                if (isFull) lastFullAt = Time.time;
                wasFull = isFull;
                yield return null;
            }

            Assert.That(wraps, Is.GreaterThan(0), "the ball should have crossed at least one target");
            Assert.That(cashedInAt, Is.GreaterThan(0f), "the round never cashed in");
            Assert.That(runStart, Is.GreaterThan(0f), "the final wrap must hold the bar visibly full");
            float dwellS = boot.feel.settleDwellMs / 1000f, drainS = boot.feel.barDrainMs / 1000f;
            Assert.That(lastFullAt - runStart, Is.GreaterThanOrEqualTo(0.8f * dwellS), "the bar holds full through the dwell");
            Assert.That(cashedInAt - lastFullAt, Is.GreaterThanOrEqualTo(0.8f * drainS), "the bar drains out before the cash-in fires");
            Assert.That(fillAtCashIn, Is.LessThanOrEqualTo(0.03f * full), "the bar is empty when the cash-in fires");
        }

        [UnityTest]
        public IEnumerator ThePhaseRibbonReadsTapTheDoorOnceThePanLandsInB()
        {
            yield return LoadMain();
            var boot = Boot();
            var label = GameObject.Find("PhaseRibbonLabel");
            Assert.IsNotNull(label, "the HUD carries a phase ribbon");
            Assert.AreEqual("DROP", label.GetComponent<TMP_Text>().text);

            int buffer = boot.ZoneA.BallBuffer;
            float[] xs = { -3.5f, 3.5f, -1.5f, 1.5f, -3f, 3f, 0f, -2.5f, 2.5f, 0.5f };
            for (int i = 0; i < buffer; i++)
            {
                boot.DebugDrop(xs[i % xs.Length], 1 + i % 3);
                yield return new WaitForSeconds(0.3f);
            }
            yield return WaitUntil(() => boot.Phases.Phase == GamePhase.B, 12f);
            Assert.AreEqual(GamePhase.B, boot.Phases.Phase);
            yield return new WaitForSeconds(boot.feel.ribbonMs / 1000f + 0.2f);
            Assert.AreEqual("TAP THE DOOR", label.GetComponent<TMP_Text>().text);
        }
    }
}
