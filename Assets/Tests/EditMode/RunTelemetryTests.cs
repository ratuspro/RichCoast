using System.Collections.Generic;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    /// <summary>
    /// The pure recorder. Everything interesting about analytics lives here precisely so it can be
    /// tested without an engine, a device or a network: the counters, the per-level clock, and the
    /// guards that keep a stray event outside a run from inventing data.
    /// </summary>
    public class RunTelemetryTests
    {
        /// <summary>A sink that keeps everything, so a test can assert on order as well as content.</summary>
        sealed class RecordingSink : IAnalyticsSink
        {
            public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();
            public int Flushes;

            public void Track(in AnalyticsEvent e) => Events.Add(e);
            public void Flush() => Flushes++;

            public AnalyticsEvent? First(string name)
            {
                foreach (var e in Events) if (e.Name == name) return e;
                return null;
            }

            public int Count(string name)
            {
                int n = 0;
                foreach (var e in Events) if (e.Name == name) n++;
                return n;
            }
        }

        RecordingSink sink;
        RunTelemetry telemetry;

        [SetUp]
        public void SetUp()
        {
            sink = new RecordingSink();
            telemetry = new RunTelemetry(sink);
        }

        [Test]
        public void StartRunEmitsRunStartWithResumeFlag()
        {
            telemetry.StartRun(resumed: true, level: 7);

            var e = sink.First("run_start");
            Assert.IsNotNull(e, "a started run must announce itself");
            Assert.AreEqual("true", e.Value.Get("resumed"));
            Assert.AreEqual("7", e.Value.Get("level"));
            Assert.IsTrue(telemetry.InRun);
        }

        [Test]
        public void RunEndCarriesTheCauseThatEndedIt()
        {
            telemetry.StartRun(resumed: false, level: 1);
            telemetry.EndRun(5000.0, GameOverCause.Stalemate);

            var e = sink.First("run_end");
            Assert.IsNotNull(e);
            Assert.AreEqual("stalemate", e.Value.Get("cause"),
                "distinguishing a stalemate from a death line is the whole reason the cause exists");
            Assert.AreEqual("5000", e.Value.Get("score"));
            Assert.IsFalse(telemetry.InRun);
        }

        [Test]
        public void DeathLineAndStalemateAreDistinguishable()
        {
            telemetry.StartRun(false, 1);
            telemetry.EndRun(1.0, GameOverCause.DeathLine);
            Assert.AreEqual("death_line", sink.First("run_end").Value.Get("cause"));
        }

        [Test]
        public void RunEndTalliesDropsAndGoldenHits()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordDrop();
            telemetry.RecordDrop();
            telemetry.RecordDrop();
            telemetry.RecordGoldenHit(7);
            telemetry.RecordGoldenHit(6);
            telemetry.EndRun(42.0, GameOverCause.DeathLine);

            var e = sink.First("run_end").Value;
            Assert.AreEqual("3", e.Get("drops"));
            Assert.AreEqual("2", e.Get("golden_hits"));
        }

        [Test]
        public void RunDurationAccumulatesFromTicks()
        {
            telemetry.StartRun(false, 1);
            telemetry.Tick(1500);
            telemetry.Tick(2000);
            telemetry.EndRun(0.0, GameOverCause.Stalemate);

            Assert.AreEqual("3.5", sink.First("run_end").Value.Get("duration_s"));
        }

        [Test]
        public void LevelUpEmitsOncePerLevelWithItsOwnClock()
        {
            telemetry.StartRun(false, 1);
            telemetry.Tick(4000);
            telemetry.RecordLevel(2);
            telemetry.Tick(1000);
            telemetry.RecordLevel(3);

            Assert.AreEqual(2, sink.Count("level_up"));
            var first = sink.Events.Find(x => x.Name == "level_up" && x.Get("level") == "2");
            var second = sink.Events.Find(x => x.Name == "level_up" && x.Get("level") == "3");
            Assert.AreEqual("4", first.Get("seconds_in_level"));
            Assert.AreEqual("1", second.Get("seconds_in_level"),
                "the per-level clock must restart, not report the run's total");
        }

        [Test]
        public void RepeatingTheCurrentLevelEmitsNothing()
        {
            telemetry.StartRun(false, 3);
            telemetry.RecordLevel(3);
            telemetry.RecordLevel(3);

            Assert.AreEqual(0, sink.Count("level_up"),
                "ProgressionChanged also fires on restore and on re-emit; only a real advance is a level-up");
        }

        [Test]
        public void RunEndReportsTheLevelReached()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordLevel(2);
            telemetry.RecordLevel(3);
            telemetry.EndRun(0.0, GameOverCause.DeathLine);

            Assert.AreEqual("3", sink.First("run_end").Value.Get("level"));
        }

        [Test]
        public void DoorTapRecordsMissesToo()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordDoorTap(grabbed: false, sweepIndex: 4, dropX: 195.0);

            var e = sink.First("door_tap");
            Assert.IsNotNull(e, "a tap that grabbed nothing is the measurement that BallDropped cannot make");
            Assert.AreEqual("false", e.Value.Get("grabbed"));
            Assert.AreEqual("4", e.Value.Get("sweep_index"));
        }

        [Test]
        public void DoorTapDoesNotCountAsADrop()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordDoorTap(true, 0, 0);
            telemetry.EndRun(0.0, GameOverCause.DeathLine);

            Assert.AreEqual("0", sink.First("run_end").Value.Get("drops"),
                "drops are counted from BallDropped so the milestone drain is counted the same way");
        }

        [Test]
        public void GoldenHitCarriesTheLevelItHappenedOn()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordLevel(12);
            telemetry.RecordGoldenHit(8);

            var e = sink.First("golden_gate_hit").Value;
            Assert.AreEqual("8", e.Get("multiplier"));
            Assert.AreEqual("12", e.Get("level"));
        }

        [Test]
        public void MilestoneCarriesLevelAndArenaScale()
        {
            telemetry.StartRun(false, 20);
            telemetry.RecordMilestone(20, 1.2);

            var e = sink.First("milestone").Value;
            Assert.AreEqual("20", e.Get("level"));
            Assert.AreEqual("1.2", e.Get("arena_scale"));
        }

        [Test]
        public void EventsOutsideARunAreIgnored()
        {
            telemetry.RecordDrop();
            telemetry.RecordGoldenHit(6);
            telemetry.RecordDoorTap(true, 1, 2);
            telemetry.RecordLevel(9);
            telemetry.RecordMilestone(20, 1.2);
            telemetry.Tick(1000);
            telemetry.EndRun(99.0, GameOverCause.DeathLine);

            Assert.AreEqual(0, sink.Events.Count,
                "the title screen is not a run; nothing there may invent a measurement");
        }

        [Test]
        public void EndingTwiceEmitsOneRunEnd()
        {
            telemetry.StartRun(false, 1);
            telemetry.EndRun(1.0, GameOverCause.DeathLine);
            telemetry.EndRun(1.0, GameOverCause.Stalemate);

            Assert.AreEqual(1, sink.Count("run_end"));
        }

        [Test]
        public void AbandonEndsTheRunWithoutEmittingRunEnd()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordDrop();
            telemetry.Abandon();

            Assert.AreEqual(0, sink.Count("run_end"),
                "a run left via the menu did not END; counting it as one would poison the cause funnel");
            Assert.IsFalse(telemetry.InRun);
        }

        [Test]
        public void AStartedRunResetsTheCountersOfThePreviousOne()
        {
            telemetry.StartRun(false, 1);
            telemetry.RecordDrop();
            telemetry.RecordGoldenHit(6);
            telemetry.Tick(9000);
            telemetry.EndRun(1.0, GameOverCause.DeathLine);

            telemetry.StartRun(false, 1);
            telemetry.RecordDrop();
            telemetry.Tick(1000);
            telemetry.EndRun(2.0, GameOverCause.DeathLine);

            var last = sink.Events[sink.Events.Count - 1];
            Assert.AreEqual("run_end", last.Name);
            Assert.AreEqual("1", last.Get("drops"));
            Assert.AreEqual("0", last.Get("golden_hits"));
            Assert.AreEqual("1", last.Get("duration_s"));
        }

        [Test]
        public void FlushReachesEverySink()
        {
            var second = new RecordingSink();
            var two = new RunTelemetry(new IAnalyticsSink[] { sink, second });
            two.StartRun(false, 1);
            two.Flush();

            Assert.AreEqual(1, sink.Flushes);
            Assert.AreEqual(1, second.Flushes);
            Assert.AreEqual(1, second.Count("run_start"), "every sink sees every event");
        }

        [Test]
        public void AThrowingSinkCannotTakeTheRunDown()
        {
            var bad = new ThrowingSink();
            var mixed = new RunTelemetry(new IAnalyticsSink[] { bad, sink });

            Assert.DoesNotThrow(() => mixed.StartRun(false, 1));
            Assert.DoesNotThrow(() => mixed.Flush());
            Assert.AreEqual(1, sink.Count("run_start"),
                "one broken sink must not stop the others from being fed");
        }

        sealed class ThrowingSink : IAnalyticsSink
        {
            public void Track(in AnalyticsEvent e) => throw new System.InvalidOperationException("sink is broken");
            public void Flush() => throw new System.InvalidOperationException("sink is broken");
        }
    }
}
