using System;
using PrimeTween;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone A's orchestrator (port of the Phaser <c>ZoneASystem</c>): the finite ball buffer, the
    /// internal level + stage application, the score-bar cash-in → level-up → reward beat (ticked
    /// refill, and at draw-window milestones the arena zoom-out + blacklist drain), the depletion
    /// settle gate, the stalemate check, and the run's game-over. Talks to the rest of the game ONLY
    /// through <see cref="GameEvents"/>.
    /// </summary>
    public sealed class ZoneASystem
    {
        /// <summary>Contiguous settled time before the depletion gate fires (ms), and its hard timeout.</summary>
        const float SettleMs = 350f, SettleTimeoutMs = 4000f;
        /// <summary>A stalemate must persist this long before the run actually ends.</summary>
        const float StalemateGraceMs = 250f;
        const int BufferTickFullCount = 10;
        /// <summary>Keep drained columns a touch inside the Zone B side walls (design px).</summary>
        const double DrainMarginPx = 12;

        readonly Board board;
        readonly AimController aim;
        readonly DeathLineView deathLine;
        readonly BallQueue queue;
        readonly ProgressionCurve curve;
        readonly TierLadder ladder;
        readonly GameFeelSO feel;
        readonly MergeFx mergeFx;
        readonly BallFactory factory;
        readonly DropHighlight highlight;
        readonly ArenaGrowth growth;
        readonly BoardGeometry geometry;
        readonly Transform fxRoot;

        int level = 1;
        int ballBuffer;
        int burstLevels;
        bool over;
        bool cashInPending;
        bool scoreBarCashingIn;
        bool zoneBEmpty = true;
        double score;
        /// <summary>Current gameplay phase. Aiming is live only in A; the reward sequence waits for A.</summary>
        GamePhase phase = GamePhase.A;
        /// <summary>
        /// A cash-in that arrived outside phase A (or during a running zoom) defers its visible reward
        /// until the pan lands back in A. A multi-level roll-through delivers a BURST of these, so the
        /// slot accumulates: every level's zoom factor composes into the product — a milestone crossed
        /// mid-burst must not be lost to a later plain level. (The refill amount and blacklist floor are
        /// read fresh from <see cref="level"/> when the sequence runs, so only the zoom needs carrying.)
        /// </summary>
        bool deferredCashIn;
        double deferredZoomFactor = 1;
        /// <summary>True while the milestone zoom-out + drain runs — one of the two aim-freeze sources.</summary>
        bool milestoneZoomActive;

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
        public bool IsMilestoneZoomActive => milestoneZoomActive;

        public ZoneASystem(Board board, AimController aim, DeathLineView deathLine, BallQueue queue, ProgressionCurve curve, TierLadder ladder, GameFeelSO feel, MergeFx mergeFx, BallFactory factory, DropHighlight highlight, ArenaGrowth growth, BoardGeometry geometry, Transform fxRoot)
        {
            this.board = board;
            this.aim = aim;
            this.deathLine = deathLine;
            this.queue = queue;
            this.curve = curve;
            this.ladder = ladder;
            this.feel = feel;
            this.mergeFx = mergeFx;
            this.factory = factory;
            this.highlight = highlight;
            this.growth = growth;
            this.geometry = geometry;
            this.fxRoot = fxRoot;

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
            GameEvents.PhaseChanged += OnPhaseChanged;
        }

        /// <summary>Aiming is live only in phase A; a cash-in that arrived elsewhere runs its reward beat once the pan lands back in A.</summary>
        void OnPhaseChanged(GamePhase next)
        {
            phase = next;
            ApplyFreeze();
            if (phase == GamePhase.A) RunDeferredCashIn();
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
            // The door-candidate glow shows while the buffer is spent — the "out of balls" window that
            // opens as the last drop settles, rides the pan, and covers the whole B phase. Not during a
            // milestone zoom: the door is locked then and the board is mid-drain.
            highlight.Track(ballBuffer == 0 && !milestoneZoomActive ? DoorTarget.Find(board) : null, deltaMs);
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

        /// <summary>
        /// Immediate half of a cash-in (any phase): advance the stage and broadcast it synchronously —
        /// Zone B reads the new score-bar target straight away for its cascade math. The zoom factor is
        /// computed HERE, per level, while <see cref="level"/> is this event's level; a roll-through burst
        /// then composes the factors so a milestone crossed mid-burst keeps its zoom.
        /// </summary>
        void OnScoreBarFilled()
        {
            cashInPending = true;
            burstLevels += 1;
            level += 1;
            var stage = curve.GetStage(level);
            var window = curve.WindowForLevel(level);
            ApplyStage();
            aim.RefreshQueue();
            EmitProgression();
            double zoom = ladder.MilestoneZoomFactor(level, curve.WindowForLevel(level - 1), window, stage.Tightness, curve.IsTailLevel(level));
            // Deferred half: the visible reward (ticked refill; milestone zoom + drain) runs only in phase
            // A and never on top of a running zoom. A cash-in normally arrives in phase B and runs when
            // the pan lands back in A; one while already in A (e.g. drained balls filled the bar) runs now.
            if (phase == GamePhase.A && !milestoneZoomActive)
            {
                RunCashInSequence(zoom);
            }
            else
            {
                deferredCashIn = true;
                deferredZoomFactor *= zoom;
            }
        }

        void RunDeferredCashIn()
        {
            if (!deferredCashIn || milestoneZoomActive || phase != GamePhase.A) return;
            deferredCashIn = false;
            double zoom = deferredZoomFactor;
            deferredZoomFactor = 1;
            RunCashInSequence(zoom);
        }

        /// <summary>
        /// The visible reward sequence for one cash-in (possibly collapsing a whole roll-through burst):
        /// the ticked buffer refill, plus — when a milestone was crossed (<paramref name="zoomFactor"/> ≠ 1,
        /// the product of every burst level's factor) — the arena zoom-out and the blacklist drain.
        /// </summary>
        void RunCashInSequence(double zoomFactor)
        {
            // Roll-through jackpot: the burst's extra levels each pay a couple of bonus balls on top of
            // the final level's refill (a deferred burst collapses into ONE refill here).
            int bonus = Math.Max(0, burstLevels - 1) * ProgressionCurve.BurstRefillBonus;
            burstLevels = 0;
            AnimateBufferTo(ProgressionCurve.BufferForLevel(level) + bonus);
            if (Math.Abs(zoomFactor - 1) > 1e-9)
            {
                // WindowForLevel, not the stage's window: a TAIL milestone shifts the window without an
                // authored stage, so the stage's floor would under-drain the blacklist there.
                BeginMilestoneZoom((float)zoomFactor, curve.WindowForLevel(level).min);
            }
            else
            {
                MaybeUnlockDrop();
            }
        }

        /// <summary>
        /// Run a milestone zoom-out: freeze Zone A input and lock Zone C (via <c>ArenaZoom</c>), grow the
        /// arena (the balls recede — see <see cref="ArenaGrowth"/>), then — once they've re-seated —
        /// drain the freshly-blacklisted tiers into Zone B, and only restore input / Zone C when that
        /// finishes. The aim ghost re-reads its (now smaller) radius up front.
        /// </summary>
        void BeginMilestoneZoom(float factor, int newMinTier)
        {
            milestoneZoomActive = true;
            ApplyFreeze();
            // The window already shifted up (ApplyStage); re-roll the in-hand + Next pieces off any
            // now-blacklisted tiers so the queue shows valid tiers when input returns.
            queue.Reroll();
            GameEvents.RaiseArenaZoom(true);
            growth.Grow(factor, () => DrainBlacklisted(newMinTier, () =>
            {
                milestoneZoomActive = false;
                ApplyFreeze();
                MaybeUnlockDrop();
                GameEvents.RaiseArenaZoom(false);
                RunDeferredCashIn();
            }));
            aim.RefreshQueue();
        }

        /// <summary>
        /// Drain every board ball below the new draw-window floor into Zone B in one synchronized slide.
        /// Mirrors Zone C's handoff: signal Zone B busy up front (so the emptying board can't read as a
        /// stalemate), animate a throwaway sprite of each ball from where it sits down to the Zone B
        /// entry, and raise <c>BallDropped</c> for it when its slide lands.
        /// </summary>
        void DrainBlacklisted(int minTier, Action onDone)
        {
            if (over || board.CountBelow(minTier) == 0)
            {
                onDone();
                return;
            }
            GameEvents.RaiseZoneBBusy();
            zoneBEmpty = false;
            var drained = board.TakeBallsBelow(minTier);
            Sfx.Instance?.Transition();

            float zbDiameter = geometry.ZoneBBallRadius * 2f;
            float entryY = geometry.ZoneBTopY - geometry.ZoneBBallRadius;
            int remaining = drained.Count;
            foreach (var d in drained)
            {
                double designX = Math.Clamp(geometry.WorldXToDesign(d.Position.x), DrainMarginPx, DesignSpace.Width - DrainMarginPx);
                var target = new Vector2(geometry.DesignXToWorld(designX), entryY);
                int tier = d.Tier;
                float startDiameter = d.Diameter;
                var start = d.Position;

                var go = new GameObject("Drain");
                go.transform.SetParent(fxRoot, false);
                go.transform.position = start;
                go.transform.localScale = new Vector3(startDiameter, startDiameter, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = BallArt.SpriteForTier(tier);
                sr.sortingOrder = 800;

                Tween.Custom(go.transform, 0f, 1f, feel.drainMs / 1000f, (tr, u) =>
                {
                    tr.position = Vector2.Lerp(start, target, u);
                    float size = Mathf.Lerp(startDiameter, zbDiameter, u);
                    tr.localScale = new Vector3(size, size, 1f);
                }, Ease.InCubic).OnComplete(go.transform, tr =>
                {
                    GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(tier), designX));
                    UnityEngine.Object.Destroy(tr.gameObject);
                    if (--remaining == 0) onDone();
                }, warnIfTargetDestroyed: false);
            }
        }

        /// <summary>One sink for the reversible aim freeze, composing its two sources: the milestone zoom-out and the game not being in phase A.</summary>
        void ApplyFreeze() => aim.SetFrozen(milestoneZoomActive || phase != GamePhase.A);

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
            if (over || phase != GamePhase.A || cashInPending || milestoneZoomActive) return;
            if (board.BallCount == 0 && zoneBEmpty) return; // stalemate path owns this
            GameEvents.RaiseZoneADepleted();
        }

        bool IsStalemate() => ballBuffer == 0 && zoneBEmpty && board.BallCount == 0 && !scoreBarCashingIn && !cashInPending && !milestoneZoomActive;

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
