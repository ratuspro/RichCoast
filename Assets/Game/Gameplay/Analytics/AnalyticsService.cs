using System;
using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The wiring: subscribes to <see cref="GameEvents"/>, feeds <see cref="RunTelemetry"/>, and owns
    /// the sinks. A PURE SUBSCRIBER — no zone knows it exists, and deleting it would change nothing
    /// about how the game plays. That is the property the whole sub-project is built to preserve.
    /// <para>Lifetime is one scene load, like everything else downstream of <c>GameEvents.Reset()</c>.
    /// <see cref="Dispose"/> unsubscribes explicitly anyway: `Reset()` already drops every handler, but
    /// a service that only unsubscribed by being reset would double-count the moment anything ever
    /// built two of them.</para>
    /// </summary>
    public sealed class AnalyticsService : IDisposable
    {
        readonly RunTelemetry telemetry;
        readonly RingBufferSink local;
        readonly GameAnalyticsSink backend;
        bool subscribed;

        /// <summary>The in-memory tail, for the debug overlay.</summary>
        public RingBufferSink Local => local;
        public bool BackendActive => backend != null;
        public bool InRun => telemetry.InRun;

        /// <summary>
        /// The local sink always runs — it never leaves the device, so it is not "collection". The
        /// BACKEND sink is constructed only on <see cref="ConsentState.Granted"/>: the gate lives here
        /// rather than inside the sink so that the object which could send data does not exist at all
        /// unless the player said yes.
        /// </summary>
        public AnalyticsService(ConsentState consent, bool writeLocalToDisk = true)
        {
            local = new RingBufferSink(writeLocalToDisk);
            backend = consent == ConsentState.Granted ? new GameAnalyticsSink() : null;

            var sinks = new List<IAnalyticsSink> { local };
            if (backend != null) sinks.Add(backend);
            telemetry = new RunTelemetry(sinks.ToArray());

            Subscribe();
        }

        void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            GameEvents.BallDropped += OnBallDropped;
            GameEvents.GoldenGateHit += OnGoldenGateHit;
            GameEvents.ProgressionChanged += OnProgressionChanged;
            GameEvents.DoorTapped += OnDoorTapped;
            GameEvents.GameOver += OnGameOver;
        }

        public void Dispose()
        {
            if (!subscribed) return;
            subscribed = false;
            GameEvents.BallDropped -= OnBallDropped;
            GameEvents.GoldenGateHit -= OnGoldenGateHit;
            GameEvents.ProgressionChanged -= OnProgressionChanged;
            GameEvents.DoorTapped -= OnDoorTapped;
            GameEvents.GameOver -= OnGameOver;
            telemetry.Flush();
        }

        public void StartRun(bool resumed, int level) => telemetry.StartRun(resumed, level);

        /// <summary>
        /// Left rather than lost — MENU, or the app going away mid-run. Emits no `run_end`, so the
        /// cause funnel keeps meaning what it says.
        /// </summary>
        public void AbandonRun() => telemetry.Abandon();

        /// <summary>Unscaled: a run frozen under a modal dialog is still time the player spent on it.</summary>
        public void Tick(float unscaledDeltaMs) => telemetry.Tick(unscaledDeltaMs);

        public void Flush() => telemetry.Flush();

        /// <summary>
        /// Consent changed at runtime. A grant only takes effect on the next launch — standing up the
        /// SDK mid-session is the kind of thing that works in the editor and surprises you on device —
        /// but a REVOCATION is honoured immediately, and takes the local tail with it.
        /// </summary>
        public void OnConsentRevoked() => local.Clear();

        void OnBallDropped(BallDroppedEvent _) => telemetry.RecordDrop();
        void OnGoldenGateHit(int multiplier) => telemetry.RecordGoldenHit(multiplier);
        void OnDoorTapped(DoorTapEvent e) => telemetry.RecordDoorTap(e.Grabbed, e.SweepIndex, e.DropX);
        void OnGameOver(GameOverEvent e) => telemetry.EndRun(e.FinalScore, e.Cause);

        void OnProgressionChanged(ProgressionChangedEvent e)
        {
            telemetry.RecordLevel(e.Level);
            if (e.Level > 1 && e.Level % ProgressionCurve.MilestoneEvery == 0)
                telemetry.RecordMilestone(e.Level, ArenaScale);
        }

        /// <summary>
        /// Set by the session each time arena growth lands, so the milestone event can report it
        /// without the service reaching into gameplay for it.
        /// </summary>
        public double ArenaScale { get; set; } = 1.0;
    }
}
