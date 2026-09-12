using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The milestone arena growth (the Phaser build's <c>ArenaView.grow</c> beat) on a FIXED tray and
    /// ONE camera. Zone A is a power-of-three merge board with no tier ceiling, so balls grow without
    /// bound; to make room the arena must get roomier by the factor the caller computes (neutral
    /// ball-growth match × the stage's tightness). Instead of growing the walls and zooming a camera
    /// out, the board's CONTENTS recede: every live ball shrinks by 1/factor and slides toward the
    /// funnel apex (the origin), which is exactly what the zoom looked like from Zone A's side —
    /// and because the funnel V is linear through the apex, scaling about it maps the floor onto
    /// itself, so resting balls stay resting. Zone C/B, the walls, the death line and the camera never
    /// move. During the tween Zone A's bodies are frozen (input is frozen too); on landing each ball
    /// is re-seated at its new size with its mass re-applied (see <see cref="BallFactory.ApplyMass"/>),
    /// then physics resumes. Owned by Zone A; Zone C reacts to the <c>ArenaZoom</c> event for its lock.
    /// </summary>
    public sealed class ArenaGrowth
    {
        sealed class Seat
        {
            public Ball Ball;
            public Vector2 From, To, Velocity;
            public float RadiusFrom, RadiusTo;
        }

        readonly BallFactory factory;
        readonly Board board;
        readonly GameFeelSO feel;
        readonly List<Seat> seats = new List<Seat>();
        Tween tween;

        public bool IsAnimating { get; private set; }
        /// <summary>The arena-growth factor in force (the product of every milestone's zoom factor).</summary>
        public float Scale => factory.ArenaScale;

        public ArenaGrowth(BallFactory factory, Board board, GameFeelSO feel)
        {
            this.factory = factory;
            this.board = board;
            this.feel = feel;
        }

        /// <summary>Grow the arena one milestone step by <paramref name="factor"/>; <paramref name="onComplete"/> fires when the balls have re-seated.</summary>
        public void Grow(float factor, Action onComplete)
        {
            IsAnimating = true;
            // Settle any merge that's already owed at the OLD size, so nothing is born mid-tween.
            board.Tick(0f);
            factory.SetArenaScale(factory.ArenaScale * factor);

            seats.Clear();
            foreach (var ball in board.Balls)
            {
                seats.Add(new Seat
                {
                    Ball = ball,
                    From = ball.Position,
                    To = ball.Position / factor,
                    Velocity = ball.Body.linearVelocity / factor,
                    RadiusFrom = ball.Radius,
                    RadiusTo = ball.Radius / factor,
                });
                ball.Body.simulated = false;
            }

            tween.Stop();
            float seconds = feel.milestoneZoomMs / 1000f;
            tween = Tween.Custom(this, 0f, 1f, seconds, (self, u) => self.Step(u), Ease.InOutCubic)
                .OnComplete(this, self =>
                {
                    self.Land();
                    onComplete?.Invoke();
                }, warnIfTargetDestroyed: false);
        }

        void Step(float u)
        {
            foreach (var s in seats)
            {
                if (!Alive(s.Ball)) continue;
                s.Ball.transform.position = Vector2.Lerp(s.From, s.To, u);
                s.Ball.SetRadius(Mathf.Lerp(s.RadiusFrom, s.RadiusTo, u));
            }
        }

        void Land()
        {
            foreach (var s in seats)
            {
                if (!Alive(s.Ball)) continue;
                var body = s.Ball.Body;
                s.Ball.transform.position = s.To;
                s.Ball.SetRadius(s.RadiusTo);
                factory.ApplyMass(s.Ball);
                body.simulated = true;
                body.position = s.To;
                body.linearVelocity = s.Velocity;
                body.WakeUp();
            }
            seats.Clear();
            IsAnimating = false;
        }

        static bool Alive(Ball ball) => ball != null && ball.gameObject.activeSelf;
    }
}
