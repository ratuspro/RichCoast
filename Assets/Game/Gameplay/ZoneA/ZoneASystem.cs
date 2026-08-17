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
        bool TryGrabNearestBall(float mouthX, float doorY, out BallSpec spec);
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

        private readonly GameContext _context;
        private readonly Camera _camera;
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
        private bool _depletedAnnounced;
        private bool _runOver;
        private bool _zoneBEmpty = true;
        private double _score;
        private GamePhase _phase = GamePhase.A;

        public ZoneASystem(GameContext context, Camera camera, Transform root, HudView hud)
        {
            _context = context;
            _camera = camera;
            _root = root;
            _hud = hud;
        }

        public void Create()
        {
            _arena = new ArenaGeometry(1f);
            Physics2D.gravity = new Vector2(0f, -_arena.Gravity);

            _board = new Board(_context.Tiers, _arena);
            _factory = new BallFactory(_root, _context.Tiers, _board);
            _board.Bind(_factory);
            _board.Overflowed += OnOverflow;

            _arenaView = new ArenaBuilder(_root, _arena);
            _aim = new AimController(_camera, _arena.CenterX);
            _aimBall = FlatBallView.Create("Aim Ball", sortingOrder: 12);
            _aimBall.transform.SetParent(_root, worldPositionStays: false);

            _context.Bus.Subscribe<ScoreBarFilled>(OnScoreBarFilled);
            _context.Bus.Subscribe<ScoreChanged>(OnScoreChanged);
            _context.Bus.Subscribe<ScoreBarChanged>(OnScoreBarChanged);
            _context.Bus.Subscribe<PhaseChanged>(OnPhaseChanged);
            _context.Bus.Subscribe<ZoneBBusy>(OnZoneBBusy);
            _context.Bus.Subscribe<ZoneBEmpty>(OnZoneBEmpty);

            _hud.RestartRequested += StartRun;

            StartRun();
        }

        public void Dispose()
        {
            _context.Bus.Unsubscribe<ScoreBarFilled>(OnScoreBarFilled);
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
            _board.Tick(deltaMs);
            _arenaView.SetDeathLineWarning(_board.NearDeath);

            if (_runOver) return;

            DripRefill(deltaMs);

            _aim.Enabled = _phase == GamePhase.A && _buffer.Count > 0;
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
            _level++;
            var window = _context.Progression.WindowForLevel(_level);
            _queue.SetWindow(window);
            _buffer.BeginRefill(Progression.BufferForLevel(_level));
            BroadcastProgression();
            RefreshAimBall();
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

        public bool TryGrabNearestBall(float mouthX, float doorY, out BallSpec spec)
        {
            var ball = _board.NearestToDoor(mouthX, doorY);
            if (ball == null)
            {
                spec = default;
                return false;
            }

            spec = ball.Spec;
            _board.Remove(ball);
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
