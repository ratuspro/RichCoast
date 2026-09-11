using System;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone A's orchestrator (port of the Phaser <c>ZoneASystem</c>, M1 scope): the finite ball
    /// buffer, the internal level + stage application, the score-bar cash-in → level-up → ticked
    /// refill beat, the depletion settle gate, the stalemate check, and the run's game-over.
    /// Talks to the rest of the game ONLY through <see cref="GameEvents"/>.
    /// </summary>
    public sealed class ZoneASystem
    {
        /// <summary>Contiguous settled time before the depletion gate fires (ms), and its hard timeout.</summary>
        const float SettleMs = 350f, SettleTimeoutMs = 4000f;
        /// <summary>A stalemate must persist this long before the run actually ends.</summary>
        const float StalemateGraceMs = 250f;
        const int BufferTickFullCount = 10;

        readonly Board board;
        readonly AimController aim;
        readonly DeathLineView deathLine;
        readonly BallQueue queue;
        readonly ProgressionCurve curve;
        readonly GameFeelSO feel;
        readonly MergeFx mergeFx;
        readonly BallFactory factory;

        int level = 1;
        int ballBuffer;
        int burstLevels;
        bool over;
        bool cashInPending;
        bool scoreBarCashingIn;
        bool zoneBEmpty = true;
        double score;

        // Depletion settle gate.
        bool gateArmed;
        float gateSettledMs, gateElapsedMs;
        // Ticked buffer refill.
        int ticksRemaining, tickIndex;
        float tickTimerMs, tickIntervalMs;
        // Stalemate grace.
        float stalemateMs = -1f;

        public int Level => level;
        public int BallBuffer => ballBuffer;
        public bool IsOver => over;
        public double Score => score;

        public ZoneASystem(Board board, AimController aim, DeathLineView deathLine, BallQueue queue, ProgressionCurve curve, GameFeelSO feel, MergeFx mergeFx, BallFactory factory)
        {
            this.board = board;
            this.aim = aim;
            this.deathLine = deathLine;
            this.queue = queue;
            this.curve = curve;
            this.feel = feel;
            this.mergeFx = mergeFx;
            this.factory = factory;

            ApplyStage();
            aim.RefreshQueue();
            ballBuffer = ProgressionCurve.BufferForLevel(level);

            board.GameOver += HandleGameOver;
            board.Emptied += CheckLoss;
            board.DangerChanged += deathLine.SetDanger;
            board.Merged += OnMerged;
            aim.Dropped += OnDrop;

            GameEvents.ScoreChanged += total => score = total;
            GameEvents.ScoreBarChanged += (filled, target) => scoreBarCashingIn = filled >= target;
            GameEvents.ScoreBarFilled += OnScoreBarFilled;
            GameEvents.ZoneBBusy += () => zoneBEmpty = false;
            GameEvents.ZoneBEmpty += () => { zoneBEmpty = true; CheckLoss(); };
        }

        /// <summary>Announce the initial state (after every listener has subscribed).</summary>
        public void Start()
        {
            EmitBuffer();
            EmitProgression();
        }

        public void Tick(float deltaMs)
        {
            if (over) return;
            board.Tick(deltaMs);
            AdvanceDepletionGate(deltaMs);
            AdvanceRefill(deltaMs);
            AdvanceStalemate(deltaMs);
        }

        /// <summary>Debug/test hook: drop a specific tier at a world x, bypassing input (still spends the buffer).</summary>
        public void DebugDrop(float x, int tier) => OnDrop(x, tier);

        void OnDrop(float x, int tier)
        {
            if (over) return;
            board.SpawnDropped(x, tier);
            Sfx.Instance?.Drop();
            Haptics.Pulse(feel.dropHapticMs, feel.dropHapticAmp);
            if (ballBuffer <= 0) return;
            ballBuffer -= 1;
            EmitBuffer();
            if (ballBuffer == 0)
            {
                aim.SetDropLocked(true);
                CheckLoss();
                gateArmed = true;
                gateSettledMs = gateElapsedMs = 0f;
            }
        }

        void OnMerged(Ball merged, Vector2 where)
        {
            mergeFx.Play(where, merged.Tier, merged.Radius);
            Sfx.Instance?.Merge(merged.Tier);
            bool heavy = merged.Tier >= feel.heavyHapticFromTier;
            Haptics.Pulse(heavy ? feel.heavyHapticMs : feel.mergeHapticMs, heavy ? feel.heavyHapticAmp : feel.mergeHapticAmp);
        }

        /// <summary>Immediate half of a cash-in (any phase): advance the stage and broadcast it synchronously.</summary>
        void OnScoreBarFilled()
        {
            cashInPending = true;
            burstLevels += 1;
            level += 1;
            ApplyStage();
            aim.RefreshQueue();
            EmitProgression();
            // Milestone zoom / blacklist drain = M3. M1 runs the refill beat straight away (phase A).
            RunCashInSequence();
        }

        void RunCashInSequence()
        {
            int bonus = Math.Max(0, burstLevels - 1) * ProgressionCurve.BurstRefillBonus;
            burstLevels = 0;
            AnimateBufferTo(ProgressionCurve.BufferForLevel(level) + bonus);
        }

        void ApplyStage()
        {
            var stage = curve.GetStage(level);
            var window = curve.WindowForLevel(level);
            queue.SetWindow(window.min, window.max);
            // Seed only on the stage's OWN level (a stage holds for many levels).
            if (stage.BufferBalls != null && stage.FromLevel == level) queue.Seed(stage.BufferBalls);
        }

        /// <summary>Refill one slot at a time so the HUD count visibly ticks up (the reward beat). Never confiscates.</summary>
        void AnimateBufferTo(int newCapacity)
        {
            if (newCapacity <= ballBuffer)
            {
                MaybeUnlockDrop();
                ticksRemaining = 0;
                cashInPending = false;
                return;
            }
            ticksRemaining = newCapacity - ballBuffer;
            tickIndex = 0;
            tickIntervalMs = ticksRemaining <= BufferTickFullCount
                ? feel.bufferTickMs
                : Mathf.Max(feel.bufferTickMinMs, Mathf.Round(feel.bufferTickMs * BufferTickFullCount / ticksRemaining));
            tickTimerMs = 0f;
        }

        void AdvanceRefill(float deltaMs)
        {
            if (ticksRemaining <= 0) return;
            tickTimerMs += deltaMs;
            while (ticksRemaining > 0 && tickTimerMs >= tickIntervalMs)
            {
                tickTimerMs -= tickIntervalMs;
                ballBuffer += 1;
                ticksRemaining -= 1;
                EmitBuffer();
                Sfx.Instance?.BufferTick(tickIndex++);
                MaybeUnlockDrop();
                if (ticksRemaining == 0) cashInPending = false;
            }
        }

        void MaybeUnlockDrop()
        {
            if (ballBuffer > 0)
            {
                aim.SetDropLocked(false);
                gateArmed = false;
            }
        }

        /// <summary>Wait for the last drop to settle (with a hard timeout), then hand off via ZoneADepleted.</summary>
        void AdvanceDepletionGate(float deltaMs)
        {
            if (!gateArmed) return;
            gateElapsedMs += deltaMs;
            gateSettledMs = board.IsSettled() ? gateSettledMs + deltaMs : 0f;
            if (gateSettledMs < SettleMs && gateElapsedMs < SettleTimeoutMs) return;
            gateArmed = false;
            if (over || cashInPending) return;
            if (board.BallCount == 0 && zoneBEmpty) return; // stalemate path owns this
            GameEvents.RaiseZoneADepleted();
        }

        bool IsStalemate() => ballBuffer == 0 && zoneBEmpty && board.BallCount == 0 && !scoreBarCashingIn && !cashInPending;

        void CheckLoss()
        {
            if (over || stalemateMs >= 0f || !IsStalemate()) return;
            stalemateMs = 0f;
        }

        void AdvanceStalemate(float deltaMs)
        {
            if (stalemateMs < 0f) return;
            stalemateMs += deltaMs;
            if (stalemateMs < StalemateGraceMs) return;
            stalemateMs = -1f;
            if (IsStalemate()) HandleGameOver();
        }

        void EmitBuffer() => GameEvents.RaiseBallBufferChanged(ballBuffer);

        void EmitProgression()
        {
            var window = curve.WindowForLevel(level);
            GameEvents.RaiseProgressionChanged(new ProgressionChangedEvent(level, window.min, window.max, ProgressionCurve.BufferForLevel(level), curve.ScoreBarTargetForLevel(level)));
        }

        void HandleGameOver()
        {
            if (over) return;
            over = true;
            aim.Disable();
            deathLine.SetDanger(false);
            board.Freeze();
            Sfx.Instance?.GameOver();
            Haptics.Pulse(60, 255);
            GameEvents.RaiseGameOver(score);
        }
    }
}
