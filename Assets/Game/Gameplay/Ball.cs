using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// A live Zone A ball: its tier, Box2D body, and the per-frame bookkeeping the <see cref="Board"/>
    /// mutates (merge dedupe + rest-time accumulation). Contacts are only REPORTED here; the board
    /// resolves merges at one safe point per frame. The visual (sprite + squash/stretch) is the
    /// child <see cref="BallView"/>, so physics scale and juice scale never fight.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Ball : MonoBehaviour
    {
        public int Tier { get; private set; }
        public float Radius { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public CircleCollider2D Collider { get; private set; }
        public BallView View { get; private set; }

        /// <summary>Claimed by a pending merge this step (dedupe).</summary>
        public bool Consumed;
        /// <summary>Accumulated ms resting above the death line.</summary>
        public double RestMs;

        Board board;

        public void Configure(Board owner, int tier, float radius)
        {
            board = owner;
            Tier = tier;
            Radius = radius;
            Consumed = false;
            RestMs = 0;
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<CircleCollider2D>();
            Collider.radius = radius;
            if (View == null) View = GetComponentInChildren<BallView>();
            View.SetTier(tier, radius);
        }

        public float Speed => Body.linearVelocity.magnitude;
        public Vector2 Position => Body.position;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (board == null) return;
            float impact = collision.relativeVelocity.magnitude;
            View.OnImpact(impact, collision.GetContact(0).normal);
            var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<Ball>() : null;
            if (other != null) board.ReportContact(this, other);
        }
    }
}
