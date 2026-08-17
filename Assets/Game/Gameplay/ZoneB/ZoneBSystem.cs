using System.Collections.Generic;
using RichCoast.Core;
using RichCoast.Data;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneB
{
    /// <summary>
    /// Zone B: the split arena. A ball enters at the column the player froze, gates multiply it,
    /// walls route the copies, and collectors cash them out. The player has no control once the
    /// ball is in — the skill was choosing which ball to send and where.
    ///
    /// Zone B owns scoring: the running total and the score bar both live here, and Zone A learns
    /// about level-ups only through <see cref="ScoreBarFilled"/>.
    /// </summary>
    public sealed class ZoneBSystem : IGameSystem
    {
        /// <summary>How long a round may make no progress before the survivors are swept (ms).</summary>
        private const float StuckRoundMs = 3500f;

        /// <summary>How long a single ball may sit motionless before it is drained (ms).</summary>
        private const float ParkedBallMs = 900f;

        private readonly GameContext _context;
        private readonly Transform _root;
        private readonly ScoreBar _bar = new ScoreBar();

        private readonly List<ZoneBBall> _live = new List<ZoneBBall>();
        private readonly List<ZoneBBall> _pool = new List<ZoneBBall>();
        private readonly List<(ZoneBBall ball, Gate gate)> _splits = new List<(ZoneBBall, Gate)>();
        private readonly List<(ZoneBBall ball, Collector collector)> _collected = new List<(ZoneBBall, Collector)>();
        private readonly Dictionary<int, PhysicsMaterial2D> _surfaces = new Dictionary<int, PhysicsMaterial2D>();
        private readonly List<Gate> _gates = new List<Gate>();

        private ZoneBLayout _layout;
        private double _total;
        private double _roundScore;
        private bool _busy;
        private int _created;
        private float _sinceProgressMs;

        /// <param name="layout">
        /// Overrides the per-run random pick. Used by tests and the debug harness, so a cascade can
        /// be reproduced instead of depending on which playfield the run happened to draw.
        /// </param>
        public ZoneBSystem(GameContext context, Transform root, ZoneBLayout layout = null)
        {
            _context = context;
            _root = root;
            _layout = layout;
        }

        /// <summary>Balls currently in flight — the trap-door's lock depends on this reaching zero.</summary>
        public int BallsInFlight => _live.Count;

        public void Create()
        {
            // One layout per run, picked uniformly — the player learns both, and which one they get
            // is part of a run's character.
            _layout ??= DefaultZoneBLayouts.PickRandom(new System.Random(Random.Range(int.MinValue, int.MaxValue)));

            ZoneBWalls.Build(_root, _layout);
            foreach (var def in _layout.Gates) _gates.Add(Gate.Create(_root, def));
            foreach (var def in _layout.Collectors) Collector.Create(_root, def);

            _context.Bus.Subscribe<BallDropped>(OnBallDropped);
            _context.Bus.Subscribe<ProgressionChanged>(OnProgressionChanged);
            _context.Bus.Emit(new ZoneBEmpty());
        }

        public void Dispose()
        {
            _context.Bus.Unsubscribe<BallDropped>(OnBallDropped);
            _context.Bus.Unsubscribe<ProgressionChanged>(OnProgressionChanged);
        }

        public void Tick(float deltaMs)
        {
            for (var i = 0; i < _gates.Count; i++) _gates[i].Tick(deltaMs);

            ResolveSplits();
            ResolveCollected();

            for (var i = 0; i < _live.Count; i++) _live[i].Tick(deltaMs);
            DrainParkedBalls();

            GuardAgainstAStuckRound(deltaMs);

            if (_busy && _live.Count == 0) FinishRound();
        }

        // -------------------------------------------------------------------
        // Seam
        // -------------------------------------------------------------------

        private void OnBallDropped(BallDropped e)
        {
            _busy = true;
            _context.Bus.Emit(new ZoneBBusy());
            Spawn(e.Ball, new Vector2(e.X, Layout.ZoneB.Y), Vector2.zero, fromSplit: false);
        }

        /// <summary>Zone A owns the level counter and tells the bar what it must reach.</summary>
        private void OnProgressionChanged(ProgressionChanged e) => _bar.SetTarget(e.ScoreBarTarget);

        // -------------------------------------------------------------------
        // Contacts (reported during the physics step, resolved once per frame)
        // -------------------------------------------------------------------

        internal void ReportGateHit(ZoneBBall ball, Gate gate)
        {
            if (ball.IgnoresGates) return;
            _splits.Add((ball, gate));
        }

        internal void ReportCollected(ZoneBBall ball, Collector collector) => _collected.Add((ball, collector));

        /// <summary>
        /// Drain balls that have come to a standstill — parked on a gate, wedged against a rail, or
        /// settled in a corner the funnel cannot reach. They are cashed at face value: a parked
        /// ball is a miss, and crucially it must not be re-split, because a ball resting ON a gate
        /// would otherwise multiply forever and turn a rare hang into a runaway cascade.
        /// </summary>
        private void DrainParkedBalls()
        {
            var drained = 0d;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var ball = _live[i];
                if (ball.RestMs < ParkedBallMs) continue;

                drained += ball.Spec.Value;
                Despawn(ball);
            }

            if (drained <= 0d) return;

            _total += drained;
            _roundScore += drained;
            _bar.Add(drained);
            _sinceProgressMs = 0f;
            _context.Bus.Emit(new ScoreChanged(_total));
            _context.Bus.Emit(new ScoreBarChanged(_bar.Filled, _bar.Target));
        }

        private void ResolveSplits()
        {
            if (_splits.Count == 0) return;

            for (var i = 0; i < _splits.Count; i++)
            {
                var (ball, gate) = _splits[i];
                if (ball.Consumed) continue; // already collected or split earlier this frame

                var spec = ball.Spec;
                var at = ball.Position;

                // Copies are born just BELOW the bar, not at the point of contact. A ball touches a
                // gate from above, so spawning at the contact point puts the copies inside the bar
                // itself: they get shoved back out on top, land, split again, and the cascade never
                // moves past the first row.
                var clearance = Tuning.GateThickness * 0.5f + Tuning.ZoneBBallRadius + 1f;
                var releaseY = gate.DesignCenter.y + clearance;
                Despawn(ball);

                // A ×N gate REPLACES the ball with N copies of the same value — the value ladder is
                // untouched, only the count grows.
                for (var copy = 0; copy < gate.Multiplier; copy++)
                {
                    if (_live.Count >= Tuning.ZoneBMaxBalls) break;

                    // Fan the copies out so they take different routes down instead of stacking
                    // into one column and re-hitting the same gate together.
                    var t = gate.Multiplier > 1 ? copy / (float)(gate.Multiplier - 1) - 0.5f : 0f;
                    var fan = t * Tuning.SplitSpread;
                    var position = new Vector2(at.x + Mathf.Sin(fan) * Tuning.SplitOffset, releaseY);
                    var velocity = new Vector2(Mathf.Sin(fan), Mathf.Cos(fan)) * (Tuning.SplitSpeed * Tuning.StepsPerSecond);

                    Spawn(spec, position, velocity, fromSplit: true);
                }
            }

            _splits.Clear();
            _sinceProgressMs = 0f;
        }

        private void ResolveCollected()
        {
            if (_collected.Count == 0) return;

            for (var i = 0; i < _collected.Count; i++)
            {
                var (ball, collector) = _collected[i];
                if (ball.Consumed) continue;

                var scored = ball.Spec.Value * collector.ScoreMultiplier;
                Despawn(ball);

                _total += scored;
                _roundScore += scored;
                _bar.Add(scored);
            }

            _collected.Clear();
            _sinceProgressMs = 0f;
            _context.Bus.Emit(new ScoreChanged(_total));
            _context.Bus.Emit(new ScoreBarChanged(_bar.Filled, _bar.Target));
        }

        /// <summary>
        /// The round is over once the last ball has drained. Only now is the cash-in resolved, so
        /// nothing scored after the target was crossed is lost.
        /// </summary>
        private void FinishRound()
        {
            _busy = false;

            // One event per level. Zone A raises the target between crossings, so a monster drain
            // self-limits to a few celebratory level-ups instead of wrapping a flat target.
            var levels = 0;
            while (_bar.CrossedTarget() && levels < Tuning.MaxLevelsPerCashIn)
            {
                _bar.ConsumeLevel();
                levels++;
                _context.Bus.Emit(new ScoreBarFilled());
            }
            if (_bar.CrossedTarget()) _bar.ForfeitOverflow();

            _context.Bus.Emit(new ScoreBarChanged(_bar.Filled, _bar.Target));

            if (levels > 0)
            {
                var launch = _layout.Collectors.Length > 0
                    ? _layout.Collectors[0].Center
                    : new Vector2(Layout.ZoneB.CenterX, Layout.ZoneB.Y + Layout.ZoneB.Height * 0.5f);
                _context.Bus.Emit(new ScoreHarvested(_roundScore, launch.x, launch.y));
                _context.Bus.Emit(new ScoreBarCashedIn());
            }

            _roundScore = 0;
            _context.Bus.Emit(new ZoneBEmpty());
        }

        /// <summary>
        /// Safety valve. The round only ends when the last ball drains, and the trap-door stays
        /// locked until it does — so a single ball parked somewhere the funnel cannot reach would
        /// freeze the whole run. If nothing has split or drained for a while, the survivors are
        /// swept into the drain at face value and the round closes.
        /// </summary>
        private void GuardAgainstAStuckRound(float deltaMs)
        {
            if (!_busy || _live.Count == 0)
            {
                _sinceProgressMs = 0f;
                return;
            }

            _sinceProgressMs += deltaMs;
            if (_sinceProgressMs < StuckRoundMs) return;

            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var ball = _live[i];
                var scored = ball.Spec.Value; // face value: a swept ball is a miss, not a jackpot
                Despawn(ball);
                _total += scored;
                _roundScore += scored;
                _bar.Add(scored);
            }

            _sinceProgressMs = 0f;
            _context.Bus.Emit(new ScoreChanged(_total));
            _context.Bus.Emit(new ScoreBarChanged(_bar.Filled, _bar.Target));
        }

        // -------------------------------------------------------------------
        // Ball pool
        // -------------------------------------------------------------------

        private void Spawn(BallSpec spec, Vector2 designPosition, Vector2 designVelocity, bool fromSplit)
        {
            var ball = _pool.Count > 0 ? TakeFromPool() : CreateBall();
            ball.gameObject.SetActive(true);
            ball.Wake();
            ball.Configure(spec, _context.Tiers.MaterialForTier(spec.Tier), SurfaceForTier(spec.Tier),
                fromSplit ? Tuning.SplitGraceMs : 0f);
            ball.Place(designPosition, designVelocity);
            _live.Add(ball);
        }

        private void Despawn(ZoneBBall ball)
        {
            ball.Consumed = true;
            ball.Hide();
            ball.gameObject.SetActive(false);
            _live.Remove(ball);
            _pool.Add(ball);
        }

        private ZoneBBall TakeFromPool()
        {
            var last = _pool.Count - 1;
            var ball = _pool[last];
            _pool.RemoveAt(last);
            return ball;
        }

        private ZoneBBall CreateBall()
        {
            _created++;

            // Sibling, not child: a view scales its own transform and a collider inherits transform
            // scale, so parenting them would scale the physics with the sprite.
            var view = FlatBallView.Create($"ZoneB Ball {_created} View", sortingOrder: 8);
            view.transform.SetParent(_root, worldPositionStays: false);
            view.gameObject.layer = PhysicsLayers.ZoneB;

            var go = new GameObject($"ZoneB Ball {_created}");
            go.transform.SetParent(_root, worldPositionStays: false);
            go.layer = PhysicsLayers.ZoneB;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.useAutoMass = false;
            // Zone B balls are all the same size, so mass is uniform: the cascade's feel comes from
            // the layout, not from heavy balls bullying light ones.
            body.mass = 1f;
            body.gravityScale = 1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            go.AddComponent<CircleCollider2D>();

            var ball = go.AddComponent<ZoneBBall>();
            ball.Bind(this, view);
            return ball;
        }

        private PhysicsMaterial2D SurfaceForTier(int tier)
        {
            if (_surfaces.TryGetValue(tier, out var surface)) return surface;

            var feel = _context.Tiers.MaterialForTier(tier).Def.Physics;
            surface = new PhysicsMaterial2D($"ZoneB Tier{tier}")
            {
                // Capped so exotic tiers stay lively without ping-ponging the cascade forever.
                bounciness = Mathf.Min(Tuning.ZoneBRestitutionMax, Tuning.ZoneBRestitution * feel.RestitutionMult),
                friction = Tuning.ZoneBFriction * feel.FrictionMult,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _surfaces[tier] = surface;
            return surface;
        }
    }
}
