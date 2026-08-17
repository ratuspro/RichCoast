using RichCoast.Core;
using RichCoast.Gameplay.ZoneA;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RichCoast.Gameplay.Stubs
{
    /// <summary>
    /// Stands in for the real trap-door until Zone C is migrated. It keeps the parts of the design
    /// that Zone A and Zone B depend on — a tap grabs the ball nearest the door, the door locks
    /// until Zone B reports empty, and the entry column is what feeds Zone B — but leaves out the
    /// sweeping marker and the suck-then-pop transit, which are the real Zone C's job.
    ///
    /// The entry column is fixed at the centre here; the real Zone C makes it a timing skill.
    /// </summary>
    public sealed class StubZoneC : IGameSystem
    {
        private readonly EventBus _bus;
        private readonly IBallSource _source;

        private bool _armed = true;
        private GamePhase _phase = GamePhase.A;
        private bool _pressed;

        public StubZoneC(EventBus bus, IBallSource source)
        {
            _bus = bus;
            _source = source;
        }

        public void Create()
        {
            _bus.Subscribe<ZoneBBusy>(OnZoneBBusy);
            _bus.Subscribe<ZoneBEmpty>(OnZoneBEmpty);
            _bus.Subscribe<PhaseChanged>(OnPhaseChanged);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ZoneBBusy>(OnZoneBBusy);
            _bus.Unsubscribe<ZoneBEmpty>(OnZoneBEmpty);
            _bus.Unsubscribe<PhaseChanged>(OnPhaseChanged);
        }

        public void Tick(float deltaMs)
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            var down = pointer.press.isPressed;
            var released = _pressed && !down;
            _pressed = down;

            if (!released || !_armed || _phase != GamePhase.B) return;

            var doorY = Layout.ZoneC.Y;
            if (!_source.TryGrabNearestBall(Layout.Width * 0.5f, doorY, out var spec)) return;

            // Lock BEFORE the ball is reported, so Zone A's stalemate check can never see an empty
            // board and an empty Zone B in the gap between the grab and the drop.
            _armed = false;
            _bus.Emit(new ZoneBBusy());
            _bus.Emit(new BallDropped(spec, Layout.Width * 0.5f));
        }

        private void OnZoneBBusy(ZoneBBusy _) => _armed = false;

        private void OnZoneBEmpty(ZoneBEmpty _) => _armed = true;

        private void OnPhaseChanged(PhaseChanged e) => _phase = e.Phase;
    }
}
