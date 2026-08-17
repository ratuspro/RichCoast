using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneA
{
    /// <summary>
    /// Hands a ball from Zone A's board to whoever runs the trap-door. Zone C legitimately reaches
    /// into Zone A (they are one half of the game); Zone B never does — it only ever sees a
    /// <see cref="BallDropped"/> on the bus.
    /// </summary>
    public interface IBallSource
    {
        /// <summary>Pull the ball nearest the door mouth off the board. False when the board is empty.</summary>
        bool TryGrabNearestBall(float mouthX, float doorY, out GrabbedBall grabbed);

        /// <summary>The ball a tap would take right now, without taking it — for the door's highlight.</summary>
        bool TryPeekNearestBall(float mouthX, float doorY, out GrabbedBall grabbed);
    }

    /// <summary>
    /// A ball taken off the Zone A board, with the on-screen state the trap-door's transit
    /// animation needs: it starts at the ball's real position and size and ends at Zone B's.
    /// </summary>
    public readonly struct GrabbedBall
    {
        public readonly BallSpec Spec;
        public readonly Vector2 Position;
        public readonly float Radius;

        public GrabbedBall(BallSpec spec, Vector2 position, float radius)
        {
            Spec = spec;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Zone A: the merge board. Owns the arena, the ball queue, the ball buffer and the internal
    /// level counter, and drives the run's two failure conditions (overflow and stalemate).
    ///
    /// It wires the sub-modules together and translates between them and the seam; the rules
    /// themselves live in the pure Core types (<see cref="BallQueue"/>, <see cref="BallBuffer"/>,
    /// <see cref="SettleGate"/>, <see cref="BallMath"/>).
    /// </summary>
    public sealed class ZoneASystem : IGameSystem, IBallSource
    {
        /// <summary>Interval between refill slots landing (ms) — the buffer climbs, never jumps.</summary>
        private const float RefillDripMs = 70f;

        /// <summary>How long a milestone's arena growth takes to play out (ms).</summary>
        private const float MilestoneZoomMs = 900f;

        private readonly GameContext _context;
        private readonly CameraRig _rig;
        private readonly Transform _root;
        private readonly HudView _hud;

        private Board _board;
        private BallFactory _factory;
        private ArenaBuilder _arenaView;
        private AimController _aim;
        private BallQueue _queue;
        private BallBuffer _buffer;
        private FlatBallView _aimBall;
        private SettleGate _settleGate;

        private ArenaGeometry _arena;
        private int _level = 1;
        private float _refillTimer;
        private float _pendingZoom = 1f;
        private float _zoomElapsed = -1f;
        private float _zoomFrom = 1f;
        private float _zoomTo = 1f;
        private bool _depletedAnnounced;
        private bool _started;
        private bool _runOver;
        private bool _zoneBEmpty = true;
        private double _score;
        private GamePhase _phase = GamePhase.A;

        public ZoneASystem(GameContext context, CameraRig rig, Transform root, HudView hud)
        {
            _context = context;
            _rig = rig;
            _root = root;
            _hud = hud;
        }

        public void Create()
        {
            _arena = new ArenaGeometry(1f);
            // Gravity is authored at arena scale 1 and lives in the world; each body then carries
            // its own multiplier, so Zone A's milestone growth cannot disturb Zone B's fall.
            Physics2D.gravity = new Vector2(0f, -Tuning.Gravity);
            Physics2D.IgnoreLayerCollision(PhysicsLayers.ZoneA, PhysicsLayers.ZoneB, true);

            _board = new Board(_context.Tiers, _arena);
            _factory = new BallFactory(_root, _context.Tiers, _board);
            _factory.SetArenaScale(_arena.Scale);
            _board.Bind(_factory);
            _board.Overflowed += OnOverflow;

            _arenaView = new ArenaBuilder(_root, _arena);
            // Aiming reads through the ARENA camera: it is the one whose zoom and viewport the
            // board is drawn under, so a screen column means nothing without it.
            _aim = new AimController(_rig.Arena, _arena.CenterX);
            _aimBall = FlatBallView.Create("Aim Ball", sortingOrder: 12);
            _aimBall.transform.SetParent(_root, worldPositionStays: false);

            _context.Bus.Subscribe<ScoreBarFilled>(OnScoreBarFilled);
            _context.Bus.Subscribe<ScoreBarCashedIn>(OnCashedIn);
            _context.Bus.Subscribe<ScoreChanged>(OnScoreChanged);
            _context.Bus.Subscribe<ScoreBarChanged>(OnScoreBarChanged);
            _context.Bus.Subscribe<PhaseChanged>(OnPhaseChanged);
            _context.Bus.Subscribe<ZoneBBusy>(OnZoneBBusy);
            _context.Bus.Subscribe<ZoneBEmpty>(OnZoneBEmpty);

            _hud.RestartRequested += StartRun;
        }

        public void Dispose()
        {
            _context.Bus.Unsubscribe<ScoreBarFilled>(OnScoreBarFilled);
            _context.Bus.Unsubscribe<ScoreBarCashedIn>(OnCashedIn);
            _context.Bus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            _context.Bus.Unsubscribe<ScoreBarChanged>(OnScoreBarChanged);
            _context.Bus.Unsubscribe<PhaseChanged>(OnPhaseChanged);
            _context.Bus.Unsubscribe<ZoneBBusy>(OnZoneBBusy);
            _context.Bus.Unsubscribe<ZoneBEmpty>(OnZoneBEmpty);
            if (_board != null) _board.Overflowed -= OnOverflow;
            if (_hud != null) _hud.RestartRequested -= StartRun;
        }

        // -------------------------------------------------------------------
        // Run lifecycle
        // -------------------------------------------------------------------

        private void StartRun()
        {
            _board.Clear();
            _level = 1;
            _score = 0;
            _runOver = false;
            _depletedAnnounced = false;
            _zoneBEmpty = true;
            _refillTimer = 0f;
            _pendingZoom = 1f;
            _zoomElapsed = -1f;
            SetArenaScale(1f);
            _settleGate.Reset();

            var window = _context.Progression.WindowForLevel(_level);
            _queue = new BallQueue(window, Random.Range(int.MinValue, int.MaxValue));
            _buffer = new BallBuffer(Progression.BufferForLevel(_level));

            _aim.SetAim(_arena.CenterX);
            _aim.Enabled = true;
            _hud.HideGameOver();

            BroadcastProgression();
            _context.Bus.Emit(new BallBufferChanged(_buffer.Count));
            _hud.SetScore(0);
            RefreshAimBall();
        }

        private void EndRun()
        {
            if (_runOver) return;
            _runOver = true;
            _aim.Enabled = false;
            _hud.ShowGameOver(_score, _level);
            _context.Bus.Emit(new GameOver(_score, _level));
        }

        // -------------------------------------------------------------------
        // Frame
        // -------------------------------------------------------------------

        public void Tick(float deltaMs)
        {
            // The opening broadcast waits for the first tick: Create() runs system by system, so
            // anything emitted there would miss every system built after this one — Zone B would
            // never hear the level-1 score-bar target.
            if (!_started)
            {
                _started = true;
                StartRun();
            }

            _board.Tick(deltaMs);
            _arenaView.SetDeathLineWarning(_board.NearDeath);
            AdvanceMilestoneZoom(deltaMs);

            if (_runOver) return;

            DripRefill(deltaMs);

            _aim.Enabled = _phase == GamePhase.A && _buffer.Count > 0 && _zoomElapsed < 0f;
            _aim.Tick(deltaMs);
            RefreshAimBall();

            if (_aim.ReleasedThisFrame) Drop();

            CheckDepleted(deltaMs);
            CheckStalemate();
        }

        /// <summary>Balls currently on the board. Exposed for the debug harness and PlayMode tests.</summary>
        public int BallCount => _board.Count;

        /// <summary>Highest tier on the board, or 0 when it is empty. Debug/tests only.</summary>
        public int HighestTier
        {
            get
            {
                var highest = 0;
                for (var i = 0; i < _board.Balls.Count; i++) highest = Mathf.Max(highest, _board.Balls[i].Tier);
                return highest;
            }
        }

        /// <summary>
        /// Drop at a given column without going through the pointer — the debug harness's DROP
        /// button, and how PlayMode tests exercise the real physics. Obeys every normal rule.
        /// </summary>
        public bool DebugDrop(float designX)
        {
            if (_runOver || _phase != GamePhase.A) return false;
            _aim.SetAim(designX);
            var before = _buffer.Count;
            Drop();
            return _buffer.Count < before;
        }

        private void Drop()
        {
            if (!_buffer.Spend()) return;

            var tier = _queue.Take();
            var radius = _context.Tiers.RadiusForTier(tier);
            var x = _arena.ClampSpawnX(_aim.AimX, radius);

            _board.Spawn(tier, new Vector2(x, _arena.SpawnY), Vector2.zero);
            _aim.StartCooldown();
            _settleGate.Reset();

            _context.Bus.Emit(new BallBufferChanged(_buffer.Count));
            RefreshAimBall();
        }

        /// <summary>
        /// The buffer climbs one slot at a time so a refill reads as a payout rather than a number
        /// swap — and so dropping unlocks on the very first slot instead of waiting for the rest.
        /// </summary>
        private void DripRefill(float deltaMs)
        {
            if (_buffer.PendingRefill <= 0) return;

            _refillTimer -= deltaMs;
            if (_refillTimer > 0f) return;

            _refillTimer = RefillDripMs;
            if (_buffer.TakeRefillSlot())
            {
                _depletedAnnounced = false;
                _context.Bus.Emit(new BallBufferChanged(_buffer.Count));
            }
        }

        /// <summary>
        /// Out of fuel AND the board has stopped moving: hand the game over to the Zone B phase.
        /// The settle gate is what stops the camera being yanked away mid-cascade.
        /// </summary>
        private void CheckDepleted(float deltaMs)
        {
            if (_depletedAnnounced || _phase != GamePhase.A || _buffer.HasSupply) return;

            if (_settleGate.Advance(deltaMs, _board.IsSettled()))
            {
                _depletedAnnounced = true;
                _context.Bus.Emit(new ZoneADepleted());
            }
        }

        /// <summary>
        /// Nothing left to play: no fuel, no balls on the board, nothing in flight in Zone B. Note
        /// the buffer counts BANKED refill slots as supply — that is the last-chance window, where
        /// a ball still in Zone B tops the bar and saves the run.
        /// </summary>
        private void CheckStalemate()
        {
            if (_buffer.HasSupply || !_board.IsEmpty || !_zoneBEmpty) return;
            EndRun();
        }

        private void OnOverflow() => EndRun();

        // -------------------------------------------------------------------
        // Seam
        // -------------------------------------------------------------------

        private void OnScoreBarFilled(ScoreBarFilled _)
        {
            // One event per level, so a multi-level roll-through arrives as a burst; each level is
            // applied in turn and the burst bonus is paid on the crossings beyond the first.
            var previousWindow = _context.Progression.WindowForLevel(_level);
            _level++;
            var window = _context.Progression.WindowForLevel(_level);

            // Zoom factors COMPOSE by product, so a burst that overshoots a milestone level still
            // carries that milestone's growth even though the burst ended on a plain level.
            _pendingZoom *= BallMath.MilestoneZoomFactor(
                _context.Tiers,
                _level,
                previousWindow,
                window,
                _context.Progression.GetStage(_level).Tightness,
                _context.Progression.IsTailLevel(_level));

            _queue.SetWindow(window);
            _buffer.BeginRefill(Progression.BufferForLevel(_level));
            BroadcastProgression();
            RefreshAimBall();
        }

        /// <summary>
        /// A cash-in has fully resolved. If it crossed a milestone, the arena grows now — after the
        /// whole roll-through, so a burst produces one growth beat rather than several.
        /// </summary>
        private void OnCashedIn(ScoreBarCashedIn _)
        {
            if (Mathf.Approximately(_pendingZoom, 1f)) return;

            BeginMilestoneZoom(_arena.Scale * _pendingZoom);
            _pendingZoom = 1f;
        }

        private void BeginMilestoneZoom(float targetScale)
        {
            _zoomFrom = _arena.Scale;
            _zoomTo = targetScale;
            _zoomElapsed = 0f;

            // Input stays locked for the whole beat: the boundary geometry is mid-tween, so both
            // aiming and the trap-door would be aiming at something that is no longer there.
            _aim.Enabled = false;
            _context.Bus.Emit(new ArenaZoom(true));

            DrainBlacklistedBalls();
        }

        /// <summary>
        /// Balls whose tier just fell out of the draw window slide into Zone B rather than lingering
        /// on a board that can no longer produce their match — otherwise the board slowly fills with
        /// tiers the player can never merge again.
        /// </summary>
        private void DrainBlacklistedBalls()
        {
            var floor = _context.Progression.WindowForLevel(_level).Min;
            for (var i = _board.Balls.Count - 1; i >= 0; i--)
            {
                var ball = _board.Balls[i];
                if (ball.Tier >= floor) continue;

                var spec = ball.Spec;
                var x = Mathf.Clamp(ball.Position.x, Layout.ZoneB.X + 20f, Layout.ZoneB.Right - 20f);
                _board.Remove(ball);
                _context.Bus.Emit(new BallDropped(spec, x));
            }
        }

        private void AdvanceMilestoneZoom(float deltaMs)
        {
            if (_zoomElapsed < 0f) return;

            _zoomElapsed += deltaMs;
            var t = Mathf.Clamp01(_zoomElapsed / MilestoneZoomMs);
            // Ease at both ends: the growth is a deliberate beat, not a jolt.
            var eased = t * t * (3f - 2f * t);
            SetArenaScale(Mathf.Lerp(_zoomFrom, _zoomTo, eased));

            if (t < 1f) return;

            _zoomElapsed = -1f;
            _context.Bus.Emit(new ArenaZoom(false));
        }

        private void SetArenaScale(float scale)
        {
            _arena = new ArenaGeometry(scale);
            _board.SetArena(_arena);
            _factory.SetArenaScale(scale);
            _arenaView.SetArenaScale(scale);
            _rig.ArenaScale = scale;
        }

        private void OnScoreChanged(ScoreChanged e)
        {
            _score = e.Total;
            _hud.SetScore(e.Total);
        }

        private void OnScoreBarChanged(ScoreBarChanged e) => _hud.SetScoreBar(e.Filled, e.Target);

        private void OnPhaseChanged(PhaseChanged e)
        {
            _phase = e.Phase;
            if (_phase == GamePhase.A) _settleGate.Reset();
        }

        private void OnZoneBBusy(ZoneBBusy _) => _zoneBEmpty = false;

        private void OnZoneBEmpty(ZoneBEmpty _) => _zoneBEmpty = true;

        private void BroadcastProgression()
        {
            var window = _context.Progression.WindowForLevel(_level);
            _context.Bus.Emit(new ProgressionChanged(
                _level,
                window.Min,
                window.Max,
                Progression.BufferForLevel(_level),
                _context.Progression.ScoreBarTargetForLevel(_level)));
        }

        public bool TryGrabNearestBall(float mouthX, float doorY, out GrabbedBall grabbed)
        {
            if (!TryPeekNearestBall(mouthX, doorY, out grabbed)) return false;
            _board.Remove(_board.NearestToDoor(mouthX, doorY));
            return true;
        }

        public bool TryPeekNearestBall(float mouthX, float doorY, out GrabbedBall grabbed)
        {
            var ball = _board.NearestToDoor(mouthX, doorY);
            if (ball == null)
            {
                grabbed = default;
                return false;
            }

            grabbed = new GrabbedBall(ball.Spec, ball.Position, ball.Radius);
            return true;
        }

        // -------------------------------------------------------------------
        // Presentation
        // -------------------------------------------------------------------

        /// <summary>Keep the ghost ball on the aim column and showing the tier actually in hand.</summary>
        private void RefreshAimBall()
        {
            var tier = _queue.Current;
            var radius = _context.Tiers.RadiusForTier(tier);
            _aimBall.SetTier(tier, radius, _context.Tiers.MaterialForTier(tier));
            _aimBall.SetVisible(_phase == GamePhase.A && !_runOver && _buffer.Count > 0);
            _aimBall.SetPose(new Vector2(_arena.ClampSpawnX(_aim.AimX, radius), _arena.SpawnY), 0f);

            _hud.SetBuffer(_buffer.Count);
            _hud.SetNextBall(_queue.Next, _context.Tiers.MaterialForTier(_queue.Next));
        }
    }
}
