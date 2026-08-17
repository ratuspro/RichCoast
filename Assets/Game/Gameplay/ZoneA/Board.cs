using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneA
{
    /// <summary>
    /// The live Zone A board: which balls exist, which pairs merged, whether anything is resting
    /// over the death line, and whether the board has stopped moving.
    ///
    /// All the rules it applies come from the pure Core math; this class only holds the per-frame
    /// bookkeeping those rules need. Contacts are *reported* by balls during the physics step and
    /// resolved once per frame, so a cascade can never merge the same ball twice in one step.
    /// </summary>
    public sealed class Board
    {
        private readonly TierTable _tiers;
        private readonly List<Ball> _balls = new List<Ball>();
        private readonly List<(Ball a, Ball b)> _contacts = new List<(Ball, Ball)>();
        private readonly List<Ball> _doorCandidates = new List<Ball>();
        private readonly List<Ball> _pendingDespawn = new List<Ball>();

        private BallFactory _factory;
        private ArenaGeometry _arena;
        private bool _overflowFired;

        public Board(TierTable tiers, ArenaGeometry arena)
        {
            _tiers = tiers;
            _arena = arena;
        }

        /// <summary>Raised for each merge with the resulting ball, so audio/HUD can react.</summary>
        public event System.Action<Ball> Merged;

        /// <summary>Raised once when a ball has rested above the death line for the full dwell.</summary>
        public event System.Action Overflowed;

        public IReadOnlyList<Ball> Balls => _balls;
        public int Count => _balls.Count;
        public bool IsEmpty => _balls.Count == 0;

        /// <summary>True while a slow ball sits inside the warning band just below the line.</summary>
        public bool NearDeath { get; private set; }

        public ArenaGeometry Arena => _arena;

        internal void Bind(BallFactory factory) => _factory = factory;

        public void SetArena(ArenaGeometry arena) => _arena = arena;

        public Ball Spawn(int tier, Vector2 designPosition, Vector2 designVelocity)
        {
            var ball = _factory.Spawn(tier, designPosition, designVelocity);
            _balls.Add(ball);
            return ball;
        }

        /// <summary>Take a ball off the board (a trap-door grab, or a milestone blacklist drain).</summary>
        public void Remove(Ball ball)
        {
            if (!_balls.Remove(ball)) return;
            _factory.Despawn(ball);
        }

        /// <summary>
        /// Take a ball off the board but hold it back from the pool until the frame's merges are
        /// done. Recycling it immediately would let the very next contact in the list hand back a
        /// ball that is now a different ball entirely — a merge against a ghost.
        /// </summary>
        private void RemoveDeferred(Ball ball)
        {
            if (!_balls.Remove(ball)) return;
            ball.Hide();
            _pendingDespawn.Add(ball);
        }

        public void Clear()
        {
            for (var i = _balls.Count - 1; i >= 0; i--) _factory.Despawn(_balls[i]);
            _balls.Clear();
            _contacts.Clear();
            _overflowFired = false;
            NearDeath = false;
        }

        internal void ReportContact(Ball a, Ball b)
        {
            if (!MergeLogic.CanMerge(a.Tier, b.Tier)) return;
            _contacts.Add((a, b));
        }

        /// <summary>
        /// The ball a trap-door tap would grab: nearest the door mouth by edge distance. Shared by
        /// the grab itself and any highlight, so the two can never disagree.
        /// </summary>
        public Ball NearestToDoor(float mouthX, float doorY)
        {
            _doorCandidates.Clear();
            for (var i = 0; i < _balls.Count; i++) _doorCandidates.Add(_balls[i]);
            return BallMath.NearestDoorBall(_doorCandidates, mouthX, doorY);
        }

        /// <summary>Every ball is slower than the arena's rest threshold — nothing is in motion.</summary>
        public bool IsSettled()
        {
            for (var i = 0; i < _balls.Count; i++)
            {
                if (_balls[i].Speed >= _arena.RestSpeed) return false;
            }
            return true;
        }

        /// <summary>
        /// Per-frame bookkeeping: resolve merges, clamp runaway speeds, accumulate rest time over
        /// the death line and push poses at the views.
        /// </summary>
        public void Tick(float deltaMs)
        {
            ResolveMerges();
            UpdateBallStates(deltaMs);
        }

        private void ResolveMerges()
        {
            if (_contacts.Count == 0) return;

            for (var i = 0; i < _contacts.Count; i++)
            {
                var (a, b) = _contacts[i];
                // A ball can be reported in several contacts in one step; the first one wins and
                // the rest are dropped, which is what keeps a three-ball pile from double-merging.
                if (a.Consumed || b.Consumed) continue;
                if (!MergeLogic.CanMerge(a.Tier, b.Tier)) continue;

                a.Consumed = true;
                b.Consumed = true;

                var tier = MergeLogic.MergedTier(a.Tier);
                var at = MergePosition(a, b, tier);
                RemoveDeferred(a);
                RemoveDeferred(b);

                var merged = Spawn(tier, at, Vector2.zero);
                merged.PlayMergePop();
                Blast(at, merged);
                Merged?.Invoke(merged);
            }

            _contacts.Clear();

            for (var i = 0; i < _pendingDespawn.Count; i++) _factory.Despawn(_pendingDespawn[i]);
            _pendingDespawn.Clear();
        }

        /// <summary>
        /// Where the merged ball is born: the midpoint of the pair, pulled far enough inside the
        /// arena that the bigger ball cannot start life overlapping a wall. A ball spawned inside
        /// a wall is ejected by the depenetration solver hard enough to leave the board entirely.
        /// </summary>
        private Vector2 MergePosition(Ball a, Ball b, int mergedTier)
        {
            var at = BallMath.Midpoint(a.Position, b.Position);
            var radius = _tiers.RadiusForTier(mergedTier);
            return new Vector2(
                _arena.ClampSpawnX(at.x, radius),
                Mathf.Clamp(at.y, _arena.CeilingY + radius, _arena.FloorY - radius));
        }

        /// <summary>Shove the merge's neighbours outward so a fresh gap opens instead of a jam.</summary>
        private void Blast(Vector2 origin, Ball exclude)
        {
            for (var i = 0; i < _balls.Count; i++)
            {
                var ball = _balls[i];
                if (ball == exclude) continue;
                var impulse = BallMath.BlastImpulse(ball.Position, origin, _arena.BlastRadius, _arena.BlastStrength);
                if (impulse == Vector2.zero) continue;
                // The authored strength is a per-step velocity kick; the bodies work in per-second
                // units, hence the step conversion.
                ball.Velocity += impulse * Tuning.StepsPerSecond;
            }
        }

        private void UpdateBallStates(float deltaMs)
        {
            var nearDeath = false;
            var overflowed = false;

            for (var i = 0; i < _balls.Count; i++)
            {
                var ball = _balls[i];

                // Anti-tunnel backstop: no single step may carry a ball further than a wall is thick.
                var velocity = ball.Velocity;
                var clamped = BallMath.ClampSpeed(velocity, _arena.MaxSpeed);
                if (clamped != velocity) ball.Velocity = clamped;

                var position = ball.Position;
                var speed = ball.Speed;

                var resting = BallMath.IsRestingAbove(position.y, speed, _arena.DeathLineY, _arena.RestSpeed);
                ball.RestMs = BallMath.NextRestMs(ball.RestMs, deltaMs, resting);
                if (BallMath.IsOverflow(ball.RestMs, Tuning.RestMs)) overflowed = true;

                nearDeath |= BallMath.IsNearDeath(position.y, speed, _arena.DeathLineY, _arena.WarnBand, _arena.RestSpeed);

                ball.SyncView();
            }

            NearDeath = nearDeath;
            // Once only: the balls stay put after the run ends, so the raw condition holds forever.
            if (overflowed && !_overflowFired)
            {
                _overflowFired = true;
                Overflowed?.Invoke();
            }
        }
    }
}
