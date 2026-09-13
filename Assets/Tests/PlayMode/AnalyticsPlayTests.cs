using System.IO;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// The analytics wiring, played against a live event bus. The EditMode suite proves the recorder's
    /// arithmetic; these prove the two things that can only go wrong once it is actually subscribed —
    /// the consent gate, and handler hygiene across a scene reload.
    /// </summary>
    public class AnalyticsPlayTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Reset();
            tempDir = Path.Combine(Application.temporaryCachePath, "analyticstests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            RingBufferSink.PathOverride = Path.Combine(tempDir, "analytics.jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            RingBufferSink.PathOverride = null;
            GameEvents.Reset();
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        [Test]
        public void WithoutConsentTheBackendSinkIsNeverEvenConstructed()
        {
            using (var denied = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false))
            {
                Assert.IsFalse(denied.BackendActive,
                    "the object capable of sending data must not exist unless the player said yes");
                denied.StartRun(false, 1);
                GameEvents.RaiseGoldenGateHit(6);
                Assert.Greater(denied.Local.Count, 0, "the local sink still records — it never leaves the device");
            }
        }

        [Test]
        public void UnaskedIsTreatedAsDenied()
        {
            using (var unasked = new AnalyticsService(ConsentState.Unasked, writeLocalToDisk: false))
                Assert.IsFalse(unasked.BackendActive, "silence is not consent");
        }

        [Test]
        public void ConsentGrantedStandsTheBackendUp()
        {
            using (var granted = new AnalyticsService(ConsentState.Granted, writeLocalToDisk: false))
                Assert.IsTrue(granted.BackendActive);
        }

        [Test]
        public void GameplayEventsReachTheRecorderThroughTheBus()
        {
            using (var svc = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false))
            {
                svc.StartRun(resumed: false, level: 1);
                int before = svc.Local.Count;

                GameEvents.RaiseDoorTapped(new DoorTapEvent(false, 3, 195.0));
                GameEvents.RaiseGoldenGateHit(7);
                GameEvents.RaiseGameOver(new GameOverEvent(1234, GameOverCause.Stalemate));

                Assert.AreEqual(before + 3, svc.Local.Count);
                Assert.IsFalse(svc.InRun, "GameOver on the bus must end the recorded run");
            }
        }

        [Test]
        public void DisposeUnsubscribesSoATornDownServiceStopsRecording()
        {
            var svc = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false);
            svc.StartRun(false, 1);
            svc.Dispose();

            int after = svc.Local.Count;
            GameEvents.RaiseGoldenGateHit(6);

            Assert.AreEqual(after, svc.Local.Count,
                "a disposed service still holding handlers would double-count every event after a reload");
        }

        [Test]
        public void TwoServicesDoNotDoubleCountOnceTheFirstIsDisposed()
        {
            // The scene-reload shape: Reload() disposes the old service before the new scene builds one.
            var first = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false);
            first.StartRun(false, 1);
            first.Dispose();

            using (var second = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false))
            {
                second.StartRun(false, 1);
                int before = second.Local.Count;
                GameEvents.RaiseGoldenGateHit(6);

                Assert.AreEqual(before + 1, second.Local.Count);
            }
        }

        [Test]
        public void RevokingConsentWipesTheLocalTail()
        {
            using (var svc = new AnalyticsService(ConsentState.Granted, writeLocalToDisk: true))
            {
                svc.StartRun(false, 1);
                GameEvents.RaiseGoldenGateHit(6);
                svc.Flush();
                Assert.Greater(svc.Local.Count, 0);

                svc.OnConsentRevoked();

                Assert.AreEqual(0, svc.Local.Count);
                Assert.IsFalse(File.Exists(RingBufferSink.FilePath), "the on-disk tail goes too");
            }
        }

        [Test]
        public void TheLocalSinkWritesOneJsonLinePerEvent()
        {
            using (var svc = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: true))
            {
                svc.StartRun(false, 1);
                GameEvents.RaiseGoldenGateHit(6);
                svc.Flush();
            }

            var lines = File.ReadAllLines(RingBufferSink.FilePath);
            Assert.AreEqual(2, lines.Length, "run_start and golden_gate_hit");
            foreach (var line in lines)
            {
                Assert.IsTrue(line.StartsWith("{") && line.EndsWith("}"), $"not one JSON object: {line}");
                Assert.IsTrue(line.Contains("\"event\":"));
            }
        }

        [Test]
        public void EventsRaisedOutsideARunAreNotRecorded()
        {
            using (var svc = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false))
            {
                // The title screen sits on the same live bus; nothing there may invent a measurement.
                GameEvents.RaiseGoldenGateHit(6);
                GameEvents.RaiseDoorTapped(new DoorTapEvent(true, 1, 2));

                Assert.AreEqual(0, svc.Local.Count);
            }
        }

        [Test]
        public void TheRingBufferIsBoundedSoALongSessionCannotGrowWithoutLimit()
        {
            using (var svc = new AnalyticsService(ConsentState.Denied, writeLocalToDisk: false))
            {
                svc.StartRun(false, 1);
                for (int i = 0; i < RingBufferSink.Capacity * 2; i++) GameEvents.RaiseGoldenGateHit(6);

                Assert.AreEqual(RingBufferSink.Capacity, svc.Local.Count);
            }
        }
    }
}
