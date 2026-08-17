using RichCoast.Core;
using RichCoast.Gameplay.ZoneA;
using RichCoast.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RichCoast.Gameplay.ZoneC
{
    /// <summary>
    /// The trap-door at the Zone A/B boundary: a vacuum tunnel the player fires by hand.
    ///
    /// While armed, a marker sweeps the nine boundary columns; a tap freezes it, and that column
    /// becomes the ball's entry point into Zone B. The ball taken is the one nearest the door by
    /// EDGE distance, so a big ball whose surface reaches closer wins over a small ball whose
    /// centre is nearer. A brief suck-then-pop transit carries it from its Zone A position and
    /// size down to the frozen column at Zone B's much smaller scale.
    ///
    /// The door locks the instant a tap is accepted — before the transit finishes — so Zone A's
    /// stalemate check can never see an empty board while a ball is still in the tunnel. It stays
    /// locked until Zone B reports empty, and through any milestone arena zoom, because the
    /// boundary geometry is mid-tween then.
    /// </summary>
    public sealed class ZoneCSystem : IGameSystem
    {
        /// <summary>Inset of the outermost columns, so a ball never enters inside a side wall.</summary>
        private const float ColumnMargin = 24f;

        /// <summary>Transit duration (ms): long enough to read as a journey, short enough to keep tempo.</summary>
        private const float TransitMs = 260f;

        /// <summary>Radius a ball is drawn at once it reaches Zone B — the arena's small ball size.</summary>
        public const float ZoneBBallRadius = 10f;

        private readonly GameContext _context;
        private readonly EventBus _bus;
        private readonly IBallSource _source;
        private readonly Transform _root;

        private DoorSweep _sweep = DoorSweep.New();
        private DoorMarkerView _marker;
        private FlatBallView _transitView;

        private bool _zoneBEmpty = true;
        private bool _zoomActive;
        private GamePhase _phase = GamePhase.A;
        private bool _pressed;

        private bool _transiting;
        private float _transitElapsed;
        private Vector2 _transitFrom;
        private Vector2 _transitTo;
        private float _transitFromRadius;
        private BallSpec _transitSpec;

        public ZoneCSystem(GameContext context, IBallSource source, Transform root)
        {
            _context = context;
            _bus = context.Bus;
            _source = source;
            _root = root;
        }

        /// <summary>The door may fire: in the B phase, nothing in flight, no zoom running, no transit.</summary>
        public bool IsArmed => _phase == GamePhase.B && _zoneBEmpty && !_zoomActive && !_transiting;

        /// <summary>Current entry column in design space — the marker's position.</summary>
        public float CurrentColumnX => _sweep.CurrentX(ColumnMargin);

        public void Create()
        {
            _marker = DoorMarkerView.Create(_root, ColumnMargin);
            _transitView = FlatBallView.Create("Transit Ball", sortingOrder: 20);
            _transitView.transform.SetParent(_root, worldPositionStays: false);
            _transitView.SetVisible(false);

            _bus.Subscribe<ZoneBBusy>(OnZoneBBusy);
            _bus.Subscribe<ZoneBEmpty>(OnZoneBEmpty);
            _bus.Subscribe<PhaseChanged>(OnPhaseChanged);
            _bus.Subscribe<ArenaZoom>(OnArenaZoom);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ZoneBBusy>(OnZoneBBusy);
            _bus.Unsubscribe<ZoneBEmpty>(OnZoneBEmpty);
            _bus.Unsubscribe<PhaseChanged>(OnPhaseChanged);
            _bus.Unsubscribe<ArenaZoom>(OnArenaZoom);
        }

        public void Tick(float deltaMs)
        {
            if (_transiting)
            {
                AdvanceTransit(deltaMs);
                return;
            }

            var armed = IsArmed;
            _marker.SetArmed(armed);

            if (!armed)
            {
                _pressed = Pointer.current != null && Pointer.current.press.isPressed;
                return;
            }

            _sweep.Tick(deltaMs);
            _marker.SetColumn(_sweep.Index);

            if (ReleasedThisFrame()) Fire();
        }

        /// <summary>Fire on release rather than press, so a tap and a drag both resolve the same way.</summary>
        private bool ReleasedThisFrame()
        {
            var pointer = Pointer.current;
            if (pointer == null) return false;

            var down = pointer.press.isPressed;
            var released = _pressed && !down;
            _pressed = down;
            return released;
        }

        private void Fire()
        {
            var doorY = Layout.ZoneC.Y;
            if (!_source.TryGrabNearestBall(Layout.Width * 0.5f, doorY, out var grabbed)) return;

            // Lock BEFORE the transit runs: between the grab and the drop the ball exists in
            // neither zone, and an unguarded stalemate check would call that a dead run.
            _zoneBEmpty = false;
            _bus.Emit(new ZoneBBusy());

            _transiting = true;
            _transitElapsed = 0f;
            _transitSpec = grabbed.Spec;
            _transitFrom = grabbed.Position;
            _transitFromRadius = grabbed.Radius;
            _transitTo = new Vector2(_sweep.CurrentX(ColumnMargin), Layout.ZoneB.Y);

            _marker.SetFrozen(_sweep.Index);
            _transitView.SetTier(_transitSpec.Tier, _transitFromRadius, TierMaterialFor(_transitSpec.Tier));
            _transitView.SetPose(_transitFrom, 0f);
            _transitView.SetVisible(true);
        }

        private void AdvanceTransit(float deltaMs)
        {
            _transitElapsed += deltaMs;
            var t = Mathf.Clamp01(_transitElapsed / TransitMs);

            // Suck, then pop: ease in as the tunnel takes hold, so the ball accelerates away from
            // the board rather than drifting off it.
            var eased = t * t;
            var position = Vector2.Lerp(_transitFrom, _transitTo, eased);
            var radius = Mathf.Lerp(_transitFromRadius, ZoneBBallRadius, eased);

            _transitView.SetTier(_transitSpec.Tier, radius, TierMaterialFor(_transitSpec.Tier));
            _transitView.SetPose(position, 0f);

            if (t < 1f) return;

            _transiting = false;
            _transitView.SetVisible(false);
            _bus.Emit(new BallDropped(_transitSpec, _transitTo.x));
        }

        private TierMaterial TierMaterialFor(int tier) => _context.Tiers.MaterialForTier(tier);

        private void OnZoneBBusy(ZoneBBusy _) => _zoneBEmpty = false;

        private void OnZoneBEmpty(ZoneBEmpty _)
        {
            _zoneBEmpty = true;
            _sweep.Reset();
        }

        private void OnPhaseChanged(PhaseChanged e) => _phase = e.Phase;

        private void OnArenaZoom(ArenaZoom e) => _zoomActive = e.Active;
    }
}
