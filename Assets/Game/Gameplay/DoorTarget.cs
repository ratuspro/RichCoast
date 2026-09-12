using System.Collections.Generic;
using RichCoast.Core;

namespace RichCoast.Game
{
    /// <summary>
    /// The Zone A ball a trap-door tap would grab — nearest the door mouth by edge distance, read
    /// straight off the live <see cref="Board"/>. The single source of truth for BOTH Zone C's actual
    /// grab and Zone A's candidate glow, so the glow can never sit on a different ball than the one
    /// the tap takes. The selection math is the pure, unit-tested <see cref="DoorMath"/>.
    /// </summary>
    public static class DoorTarget
    {
        static readonly List<DoorCandidate> scratch = new List<DoorCandidate>();
        static readonly List<Ball> order = new List<Ball>();

        /// <summary>The door mouth sits at the funnel apex — the world origin by construction.</summary>
        const double MouthX = 0, DoorY = 0;

        public static Ball Find(Board board)
        {
            scratch.Clear();
            order.Clear();
            foreach (var ball in board.Balls)
            {
                if (ball.Consumed) continue; // mid-merge: it's about to vanish
                var p = ball.Position;
                scratch.Add(new DoorCandidate(p.x, p.y, ball.Radius));
                order.Add(ball);
            }
            int i = DoorMath.NearestDoorBall(scratch, MouthX, DoorY);
            return i < 0 ? null : order[i];
        }
    }
}
