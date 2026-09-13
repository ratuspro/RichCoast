using System;
using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The live merge board (port of the Phaser <c>Board.ts</c>): owns every dropped ball, merges
    /// same-tier pairs on contact, fires the neighbour-shoving blast + merge juice, caps runaway
    /// speeds, and watches for overflow. Contacts are flagged in the collision callback but
    /// resolved in <see cref="Tick"/>, so all world mutation happens at one safe point and a body
    /// can't be claimed by two merges in the same step (the <c>Consumed</c> flag dedupes).
    /// </summary>
    public sealed class Board
    {
        readonly HashSet<Ball> balls = new HashSet<Ball>();
        readonly List<(Ball a, Ball b)> pending = new List<(Ball, Ball)>();
        readonly BallFactory factory;
        readonly BoardGeometry geometry;
        readonly GameFeelSO feel;
        bool over;
        bool dangerActive;
        bool merging;

        public event Action GameOver;
        public event Action Emptied;
        public event Action<bool> DangerChanged;
        /// <summary>A merge happened: (merged ball, merge point).</summary>
        public event Action<Ball, Vector2> Merged;

        public Board(BallFactory factory, BoardGeometry geometry, GameFeelSO feel)
        {
            this.factory = factory;
            this.geometry = geometry;
            this.feel = feel;
        }

        public int BallCount => balls.Count;
        public bool IsOver => over;
        public IEnumerable<Ball> Balls => balls;

        /// <summary>A ball taken off the board by the blacklist drain: where it was and how big it looked.</summary>
        public readonly struct DrainedBall
        {
            public readonly Vector2 Position;
            public readonly int Tier;
            public readonly float Diameter;

            public DrainedBall(Vector2 position, int tier, float diameter)
            {
                Position = position;
                Tier = tier;
                Diameter = diameter;
            }
        }

        /// <summary>Drop a fresh ball into the board at the spawn row.</summary>
        public Ball SpawnDropped(float x, int tier)
        {
            if (over) return null;
            var ball = factory.Spawn(this, new Vector2(x, geometry.SpawnY), tier);
            balls.Add(ball);
            return ball;
        }

        /// <summary>
        /// Put a ball back at an exact resting position (save restore) rather than at the spawn row.
        /// Captures only ever happen with the board settled, so it starts at rest and stays there.
        /// </summary>
        public Ball Restore(float x, float y, int tier)
        {
            if (over) return null;
            var ball = factory.Spawn(this, new Vector2(x, y), tier);
            ball.Body.linearVelocity = Vector2.zero;
            ball.Body.angularVelocity = 0f;
            ball.RestMs = 0f;
            balls.Add(ball);
            return ball;
        }

        /// <summary>Flag a mergeable contact; the actual world mutation is deferred to <see cref="Tick"/>.</summary>
        public void ReportContact(Ball a, Ball b)
        {
            if (over) return;
            if (!balls.Contains(a) || !balls.Contains(b)) return;
            if (a.Consumed || b.Consumed) return;
            if (!MergeLogic.CanMerge(a.Tier, b.Tier)) return;
            a.Consumed = true;
            b.Consumed = true;
            pending.Add((a, b));
        }

        /// <summary>Per-frame: resolve merges, then scan for overflow / the danger warning.</summary>
        public void Tick(float deltaMs)
        {
            if (over) return;
            ResolveMerges();
            ScanOverflow(deltaMs);
        }

        /// <summary>Per-physics-step: the anti-tunnel speed cap.</summary>
        public void FixedTick()
        {
            float cap = feel.maxBallSpeed;
            float capSq = cap * cap;
            foreach (var ball in balls)
            {
                var v = ball.Body.linearVelocity;
                if (v.sqrMagnitude > capSq) ball.Body.linearVelocity = v.normalized * cap;
            }
        }

        /// <summary>
        /// True when nothing on the board is still in motion: no merges waiting, every body asleep or
        /// below the rest speed. Drives the depletion settle gate.
        /// </summary>
        public bool IsSettled()
        {
            if (pending.Count > 0) return false;
            float restSpeed = feel.restSpeed;
            foreach (var ball in balls)
            {
                if (!ball.Body.IsSleeping() && ball.Speed >= restSpeed) return false;
            }
            return true;
        }

        /// <summary>Sum of the values of every ball on the board (the M1 stub's "haul").</summary>
        public double TotalValue()
        {
            double total = 0;
            foreach (var ball in balls) total += TierMath.ValueForTier(ball.Tier);
            return total;
        }

        /// <summary>
        /// Take a ball OFF the board without merging it (the trap-door suck). Any merge it was queued
        /// for is cancelled and its partner released, so the partner can merge again later.
        /// Fires <see cref="Emptied"/> if the board is now empty.
        /// </summary>
        public bool Extract(Ball ball)
        {
            if (ball == null || !balls.Contains(ball)) return false;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (a, b) = pending[i];
                if (a != ball && b != ball) continue;
                (a == ball ? b : a).Consumed = false;
                pending.RemoveAt(i);
            }
            Remove(ball);
            return true;
        }

        /// <summary>How many board balls sit below <paramref name="minTier"/> (the newly-blacklisted ones).</summary>
        public int CountBelow(int minTier)
        {
            int n = 0;
            foreach (var ball in balls) if (ball.Tier < minTier) n++;
            return n;
        }

        /// <summary>
        /// Take every ball below the new draw-window floor OFF the board (the milestone blacklist
        /// drain) without merging, returning where each was so the caller can animate its slide into
        /// Zone B. Pending merges involving them are cancelled like <see cref="Extract"/>.
        /// </summary>
        public List<DrainedBall> TakeBallsBelow(int minTier)
        {
            var drained = new List<DrainedBall>();
            foreach (var ball in new List<Ball>(balls))
            {
                if (ball.Tier >= minTier) continue;
                drained.Add(new DrainedBall(ball.Position, ball.Tier, ball.Radius * 2f));
                Extract(ball);
            }
            return drained;
        }

        /// <summary>Freeze the board (game over): bodies stop simulating, nothing more resolves.</summary>
        public void Freeze()
        {
            over = true;
            foreach (var ball in balls) ball.Body.simulated = false;
        }

        public void Clear()
        {
            foreach (var ball in new List<Ball>(balls)) factory.Despawn(ball);
            balls.Clear();
            pending.Clear();
        }

        void ResolveMerges()
        {
            if (pending.Count == 0) return;
            merging = true;
            var batch = new List<(Ball a, Ball b)>(pending);
            pending.Clear();
            foreach (var (a, b) in batch)
            {
                if (!balls.Contains(a) || !balls.Contains(b)) continue;
                var where = (a.Position + b.Position) * 0.5f;
                int tier = MergeLogic.MergedTier(a.Tier);
                Remove(a);
                Remove(b);
                var merged = factory.Spawn(this, where, tier);
                balls.Add(merged);
                merged.View.PlayBirthPop();
                ApplyBlast(where, merged);
                Merged?.Invoke(merged, where);
            }
            merging = false;
            if (balls.Count == 0) Emptied?.Invoke();
        }

        void Remove(Ball ball)
        {
            balls.Remove(ball);
            factory.Despawn(ball);
            if (!merging && balls.Count == 0) Emptied?.Invoke();
        }

        /// <summary>Push nearby balls outward from a merge point (additive velocity kick + a scale punch).</summary>
        void ApplyBlast(Vector2 origin, Ball exclude)
        {
            float radius = feel.blastRadius;
            float strength = feel.blastStrength;
            foreach (var ball in balls)
            {
                if (ball == exclude) continue;
                var dv = BallMath.BlastImpulse(new Vec2(ball.Position.x, ball.Position.y), new Vec2(origin.x, origin.y), radius, strength);
                if (dv.X == 0 && dv.Y == 0) continue;
                ball.Body.WakeUp();
                ball.Body.linearVelocity += new Vector2((float)dv.X, (float)dv.Y);
                float k = (float)(dv.Length / strength);
                ball.View.PlayBlastPunch(k);
            }
        }

        /// <summary>End the run if any ball has rested above the death line long enough; flag the warning when close.</summary>
        void ScanOverflow(float deltaMs)
        {
            double line = DesignSpace.DeathLineY;
            double band = DesignSpace.WarnBand;
            float restSpeed = feel.restSpeed;
            bool near = false;
            foreach (var ball in balls)
            {
                double fromTop = geometry.HeightFromTopPx(ball.Position.y);
                bool resting = BallMath.IsRestingAbove(fromTop, ball.Speed, line, restSpeed);
                ball.RestMs = BallMath.NextRestMs(ball.RestMs, deltaMs, resting);
                if (BallMath.IsOverflow(ball.RestMs, feel.restMs))
                {
                    over = true;
                    GameOver?.Invoke();
                    return;
                }
                near |= BallMath.IsNearDeath(fromTop, ball.Speed, line, band, restSpeed);
            }
            SetDanger(near);
        }

        void SetDanger(bool near)
        {
            if (near == dangerActive) return;
            dangerActive = near;
            DangerChanged?.Invoke(near);
        }
    }
}
