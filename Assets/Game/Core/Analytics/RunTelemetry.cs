using System;

namespace RichCoast.Core
{
    /// <summary>
    /// The recorder: turns the run's raw signals into the six measured events, and owns the counters
    /// and clocks they need. Pure C# — it is fed <c>deltaMs</c> rather than reading <c>Time</c>, and
    /// writes through <see cref="IAnalyticsSink"/> rather than to anything concrete, so the whole of
    /// the interesting behaviour is EditMode-testable.
    /// <para>Nothing here may throw into a run. Every sink call is guarded individually: one broken
    /// sink must not silence the others, and no measurement is worth a crash.</para>
    /// <para>Every recorder method except <see cref="StartRun"/> is INERT outside a run. The title
    /// screen and the game-over overlay both sit on a live event bus, and a counter that ticks there
    /// would quietly invent data.</para>
    /// </summary>
    public sealed class RunTelemetry
    {
        readonly IAnalyticsSink[] sinks;

        bool inRun;
        int level;
        int drops;
        int goldenHits;
        double runMs;
        double levelMs;

        public RunTelemetry(IAnalyticsSink sink) : this(new[] { sink }) { }

        public RunTelemetry(IAnalyticsSink[] sinks)
        {
            this.sinks = sinks ?? Array.Empty<IAnalyticsSink>();
        }

        public bool InRun => inRun;

        public void StartRun(bool resumed, int level)
        {
            inRun = true;
            this.level = level < 1 ? 1 : level;
            drops = 0;
            goldenHits = 0;
            runMs = 0;
            levelMs = 0;
            Emit(new AnalyticsEvent("run_start")
                .With("resumed", resumed)
                .With("level", this.level));
        }

        /// <summary>Advance both clocks. Unscaled real time — a paused or frozen game is still elapsed play.</summary>
        public void Tick(double deltaMs)
        {
            if (!inRun || deltaMs <= 0) return;
            runMs += deltaMs;
            levelMs += deltaMs;
        }

        /// <summary>
        /// One ball entered Zone B. Counted from <c>BallDropped</c> rather than from the door tap so
        /// the milestone blacklist drain — which drops several balls without any tap — is counted the
        /// same way a played drop is.
        /// </summary>
        public void RecordDrop()
        {
            if (!inRun) return;
            drops++;
        }

        public void RecordDoorTap(bool grabbed, int sweepIndex, double dropX)
        {
            if (!inRun) return;
            Emit(new AnalyticsEvent("door_tap")
                .With("grabbed", grabbed)
                .With("sweep_index", sweepIndex)
                .WithRounded("drop_x", dropX));
        }

        public void RecordGoldenHit(int multiplier)
        {
            if (!inRun) return;
            goldenHits++;
            Emit(new AnalyticsEvent("golden_gate_hit")
                .With("multiplier", multiplier)
                .With("level", level));
        }

        /// <summary>
        /// A cabinet tilt. Worth its own event because it is the only move in the game the player can
        /// run out of: the funnel of interest is how many runs spend all three, how crowded the board is
        /// when they do, and — read against <c>run_end</c>'''s cause — how often a tilt is what killed them.
        /// </summary>
        public void RecordTilt(int remaining, int ballsOnBoard)
        {
            // Deliberately no run-total counter: run_end already carries the six params the payload
            // allows, and "remaining == 0" on the last tilt_used says the same thing.
            if (!inRun) return;
            Emit(new AnalyticsEvent("tilt_used")
                .With("remaining", remaining)
                .With("balls_on_board", ballsOnBoard)
                .With("level", level));
        }

        /// <summary>
        /// Fed from <c>ProgressionChanged</c>, which also fires on restore and on re-emit — so only a
        /// strict advance counts, and the per-level clock restarts rather than reporting the run total.
        /// </summary>
        public void RecordLevel(int newLevel)
        {
            if (!inRun || newLevel <= level) return;
            level = newLevel;
            Emit(new AnalyticsEvent("level_up")
                .With("level", newLevel)
                .WithRounded("seconds_in_level", levelMs / 1000.0));
            levelMs = 0;
        }

        public void RecordMilestone(int milestoneLevel, double arenaScale)
        {
            if (!inRun) return;
            Emit(new AnalyticsEvent("milestone")
                .With("level", milestoneLevel)
                .WithRounded("arena_scale", arenaScale));
        }

        public void EndRun(double score, GameOverCause cause)
        {
            if (!inRun) return;
            inRun = false;
            Emit(new AnalyticsEvent("run_end")
                .With("cause", CauseName(cause))
                .With("score", score)
                .With("level", level)
                .WithRounded("duration_s", runMs / 1000.0)
                .With("drops", drops)
                .With("golden_hits", goldenHits));
            Flush();
        }

        /// <summary>
        /// The run was left rather than lost — MENU, or the app going away mid-run. Deliberately emits
        /// nothing: counting it as a `run_end` would put a third, invisible outcome into the cause
        /// funnel and make "where do runs end" unanswerable again.
        /// </summary>
        public void Abandon()
        {
            if (!inRun) return;
            inRun = false;
            Flush();
        }

        public void Flush()
        {
            for (int i = 0; i < sinks.Length; i++)
            {
                try { sinks[i]?.Flush(); }
                catch { /* a sink may never take a run down */ }
            }
        }

        void Emit(in AnalyticsEvent e)
        {
            for (int i = 0; i < sinks.Length; i++)
            {
                try { sinks[i]?.Track(e); }
                catch { /* as above: one broken sink must not silence the rest */ }
            }
        }

        static string CauseName(GameOverCause cause) =>
            cause == GameOverCause.Stalemate ? "stalemate" : "death_line";
    }
}
