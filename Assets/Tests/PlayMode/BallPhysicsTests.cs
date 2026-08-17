using System.Collections;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Gameplay;
using RichCoast.Gameplay.ZoneA;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// The physical guarantees Zone A rests on: a dropped ball lands and stays inside the arena,
    /// and a busy board never leaks a ball through a wall. Both are the kind of failure that is
    /// invisible in the rules tests and fatal in play.
    /// </summary>
    public class BallPhysicsTests
    {
        private GameRoot _root;

        [UnitySetUp]
        public IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _root = Object.FindFirstObjectByType<GameRoot>();
        }

        [UnityTest]
        public IEnumerator ADroppedBallComesToRestOnTheFunnelFloor()
        {
            var arena = new ArenaGeometry(1f);
            Assert.That(_root.ZoneA.DebugDrop(arena.CenterX), Is.True);

            yield return new WaitForSeconds(2f);

            var ball = FindBall();
            Assert.That(ball, Is.Not.Null, "the dropped ball vanished");

            var position = ball.Position;
            var radius = _root.Context.Tiers.RadiusForTier(1);

            Assert.That(position.x, Is.InRange(arena.MinX, arena.MaxX), "the ball left through a side wall");
            Assert.That(ball.Speed, Is.LessThan(arena.RestSpeed), "the ball never settled");

            // Resting exactly one radius above the floor. Checking the precise resting height —
            // not merely "somewhere inside" — is what catches a collider whose size has drifted
            // from the radius the rules use (a scaled transform, a stale collider).
            Assert.That(position.y, Is.EqualTo(arena.FloorY - radius).Within(radius * 0.25f),
                "the ball did not come to rest on the funnel floor at its own radius");
        }

        [UnityTest]
        public IEnumerator ABusyBoardKeepsEveryBallInsideTheArena()
        {
            var arena = new ArenaGeometry(1f);

            // Six balls into the same region: merges cascade, blasts fire, the pile shoves itself
            // around. This is the state that finds containment bugs.
            for (var i = 0; i < 6; i++)
            {
                _root.ZoneA.DebugDrop(Layout.Width * (0.35f + 0.06f * i));
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(2f);

            var escaped = 0;
            foreach (var ball in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                if (!ball.gameObject.activeInHierarchy) continue;
                var position = ball.Position;
                var inside = position.x >= arena.MinX && position.x <= arena.MaxX &&
                             position.y >= arena.CeilingY && position.y <= arena.FloorY;
                if (!inside) escaped++;
            }

            Assert.That(escaped, Is.EqualTo(0), "balls leaked out of the arena");
        }

        /// <summary>
        /// The first live ZONE A ball. Typed on <see cref="Ball"/> rather than Rigidbody2D: Zone B's
        /// gates are kinematic bodies too, and picking one of those up instead would quietly assert
        /// nothing about Zone A.
        /// </summary>
        private static Ball FindBall()
        {
            foreach (var ball in Object.FindObjectsByType<Ball>(FindObjectsSortMode.None))
            {
                if (ball.gameObject.activeInHierarchy) return ball;
            }
            return null;
        }
    }
}
