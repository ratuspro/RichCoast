using System.Collections;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Data;
using RichCoast.Gameplay;
using RichCoast.Gameplay.ZoneB;
using UnityEngine;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// The real split arena, driven through the seam exactly as Zone C drives it.
    ///
    /// Zone B is where the run's score comes from and where a hang is fatal — the trap-door stays
    /// locked until the arena reports empty — so these cover a whole round: the ball splits, the
    /// copies drain, the score lands, and the arena reliably returns to empty.
    ///
    /// The arena is built standalone with an explicitly chosen layout rather than by loading the
    /// game scene, because a run picks its playfield at random and a cascade test that depends on
    /// which one it drew is a coin flip, not a test.
    /// </summary>
    public class ZoneBCascadeTests
    {
        /// <summary>A column that lands squarely on a row-1 gate in Layout 1, not in a gap.</summary>
        private const float GateColumn = 182f;

        private GameObject _host;
        private EventBus _bus;
        private ZoneBSystem _zoneB;

        [SetUp]
        public void BuildArena()
        {
            _bus = new EventBus();
            _host = new GameObject("Zone B Test Host");

            Physics2D.gravity = new Vector2(0f, -Tuning.Gravity);
            var context = new GameContext(_bus, DefaultProgression.Create(), DefaultTierLadder.CreateTable(),
                Layout.DesignScreenHeight);

            _zoneB = new ZoneBSystem(context, _host.transform, DefaultZoneBLayouts.Layout1());
            _zoneB.Create();

            // The level-1 bar target, as Zone A would broadcast it.
            _bus.Emit(new ProgressionChanged(1, 1, 1, 8, context.Progression.ScoreBarTargetForLevel(1)));
        }

        [TearDown]
        public void TearDownArena()
        {
            _zoneB.Dispose();
            Object.Destroy(_host);
        }

        /// <summary>Run the arena for a stretch of game time, ticking it as GameRoot does.</summary>
        private IEnumerator RunFor(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                _zoneB.Tick(Time.deltaTime * 1000f);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Tick until the arena empties, giving up after <paramref name="seconds"/>.</summary>
        private IEnumerator RunUntilEmpty(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds && _zoneB.BallsInFlight > 0)
            {
                _zoneB.Tick(Time.deltaTime * 1000f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            // One more tick, so the round can close now that the last ball is gone.
            _zoneB.Tick(Time.deltaTime * 1000f);
        }

        [UnityTest]
        public IEnumerator ADroppedBallSplitsAtGatesIntoManyMoreBalls()
        {
            _bus.Emit(new BallDropped(BallSpec.FromTier(3), GateColumn));

            var peak = 0;
            var elapsed = 0f;
            while (elapsed < 4f)
            {
                _zoneB.Tick(Time.deltaTime * 1000f);
                peak = Mathf.Max(peak, _zoneB.BallsInFlight);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(peak, Is.GreaterThan(1), "the ball never split — no gate multiplied it");
        }

        [UnityTest]
        public IEnumerator ARoundScoresAndAlwaysReturnsToEmpty()
        {
            var scored = 0d;
            var empties = 0;
            _bus.Subscribe<ScoreChanged>(e => scored = e.Total);
            _bus.Subscribe<ZoneBEmpty>(_ => empties++);

            _bus.Emit(new BallDropped(BallSpec.FromTier(4), GateColumn));
            yield return RunFor(0.2f);
            Assert.That(_zoneB.BallsInFlight, Is.GreaterThan(0), "the ball never entered");

            // Long enough for a full cascade to drain, and for the stuck-round valve to fire if it
            // does not — a round that never closes locks the trap-door for the rest of the run.
            yield return RunUntilEmpty(20f);

            Assert.That(_zoneB.BallsInFlight, Is.EqualTo(0), "the round never drained");
            Assert.That(empties, Is.GreaterThan(0), "the trap-door was never told it could re-arm");
            Assert.That(scored, Is.GreaterThan(0d), "a drained round scored nothing");
        }

        [UnityTest]
        public IEnumerator ASplitBallCarriesTheSameValueAsItsParent()
        {
            // A ×N gate multiplies the COUNT, never the value: N copies of the same ball. The
            // payout is what grows, which is exactly what the value ladder assumes.
            const int tier = 5;
            var value = Tiers.TierToValue(tier);
            var scored = 0d;
            _bus.Subscribe<ScoreChanged>(e => scored = e.Total);

            _bus.Emit(new BallDropped(BallSpec.FromTier(tier), GateColumn));
            yield return RunUntilEmpty(20f);

            Assert.That(scored, Is.GreaterThan(0d));
            var balls = scored / value;
            Assert.That(balls, Is.EqualTo(System.Math.Round(balls)).Within(1e-6),
                "the payout was not a whole number of ball-values");
            Assert.That(balls, Is.GreaterThan(1d), "a gate cascade should pay more than the single ball sent");
        }

        [UnityTest]
        public IEnumerator TheCascadeStaysWithinTheBallCapOnLowEndHardware()
        {
            // A deep cascade is the point; an unbounded one is a dead phone.
            _bus.Emit(new BallDropped(BallSpec.FromTier(6), GateColumn));

            var elapsed = 0f;
            while (elapsed < 6f)
            {
                _zoneB.Tick(Time.deltaTime * 1000f);
                Assert.That(_zoneB.BallsInFlight, Is.LessThanOrEqualTo(Tuning.ZoneBMaxBalls));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FillingTheBarCashesInOnceTheArenaHasDrained()
        {
            // The cash-in waits for the last ball, so nothing scored after the target is crossed is
            // lost — and the pan back up must not start before that.
            var levels = 0;
            var cashedIn = 0;
            _bus.Subscribe<ScoreBarFilled>(_ => levels++);
            _bus.Subscribe<ScoreBarCashedIn>(_ => cashedIn++);

            // A tier-10 ball is worth far more than the level-1 target of 20.
            _bus.Emit(new BallDropped(BallSpec.FromTier(10), GateColumn));
            yield return RunUntilEmpty(20f);

            Assert.That(levels, Is.GreaterThan(0), "a huge payout crossed no level");
            Assert.That(levels, Is.LessThanOrEqualTo(Tuning.MaxLevelsPerCashIn), "the roll-through cap was ignored");
            Assert.That(cashedIn, Is.EqualTo(1), "the cash-in should resolve exactly once per round");
        }
    }
}
