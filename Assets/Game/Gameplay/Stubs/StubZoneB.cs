using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Gameplay.Stubs
{
    /// <summary>
    /// Stands in for the real Zone B until it is migrated. It owns exactly what the contract says
    /// Zone B owns — the running total and the score bar — and speaks only through the seam, so
    /// the real arena drops in later with no change on Zone A's side.
    ///
    /// The "physics" is a timer: a dropped ball is busy for a moment, then pays out its value
    /// multiplied by a plausible gate cascade. Port of the original <c>dev/stubZoneB.ts</c>.
    /// </summary>
    public sealed class StubZoneB : IGameSystem
    {
        /// <summary>How long a dropped ball "falls" before it drains (ms).</summary>
        private const float FlightMs = 900f;

        /// <summary>Stand-in for a gate cascade. The real layouts run about ×8–14.</summary>
        private const float CascadeMultiplier = 10f;

        /// <summary>
        /// Hard cap on levels a single drain may cash in. A freak payout must not roll the bar
        /// hundreds of times; the excess is forfeited (see <see cref="ScoreBar.ForfeitOverflow"/>).
        /// </summary>
        private const int MaxLevelsPerCashIn = 6;

        private readonly EventBus _bus;
        private readonly ScoreBar _bar = new ScoreBar();

        private double _total;
        private double _pendingValue;
        private float _flightMsLeft;
        private bool _inFlight;

        public StubZoneB(EventBus bus)
        {
            _bus = bus;
        }

        public void Create()
        {
            _bus.Subscribe<BallDropped>(OnBallDropped);
            _bus.Subscribe<ProgressionChanged>(OnProgressionChanged);
            _bus.Emit(new ZoneBEmpty());
        }

        public void Dispose()
        {
            _bus.Unsubscribe<BallDropped>(OnBallDropped);
            _bus.Unsubscribe<ProgressionChanged>(OnProgressionChanged);
        }

        public void Tick(float deltaMs)
        {
            if (!_inFlight) return;

            _flightMsLeft -= deltaMs;
            if (_flightMsLeft > 0f) return;

            Drain();
        }

        private void OnBallDropped(BallDropped e)
        {
            _pendingValue += e.Ball.Value * CascadeMultiplier;
            _flightMsLeft = FlightMs;
            if (_inFlight) return;

            _inFlight = true;
            _bus.Emit(new ZoneBBusy());
        }

        /// <summary>Zone A owns the level counter and broadcasts the target this bar must hit.</summary>
        private void OnProgressionChanged(ProgressionChanged e) => _bar.SetTarget(e.ScoreBarTarget);

        private void Drain()
        {
            _inFlight = false;

            var payout = _pendingValue;
            _pendingValue = 0;
            _total += payout;
            _bar.Add(payout);

            _bus.Emit(new ScoreChanged(_total));

            // One ScoreBarFilled per level: Zone A raises the target between crossings, so the bar
            // self-limits to a few celebratory level-ups instead of wrapping a flat target.
            var levels = 0;
            while (_bar.CrossedTarget() && levels < MaxLevelsPerCashIn)
            {
                _bar.ConsumeLevel();
                levels++;
                _bus.Emit(new ScoreBarFilled());
            }
            if (_bar.CrossedTarget()) _bar.ForfeitOverflow();

            _bus.Emit(new ScoreBarChanged(_bar.Filled, _bar.Target));
            if (levels > 0)
            {
                // Launch point for the HUD's flying score token: the middle of Zone B, where the
                // real arena's haul label will sit.
                _bus.Emit(new ScoreHarvested(payout, Layout.ZoneB.CenterX, Layout.ZoneB.Y + Layout.ZoneB.Height * 0.5f));
                _bus.Emit(new ScoreBarCashedIn());
            }
            _bus.Emit(new ZoneBEmpty());
        }
    }
}
