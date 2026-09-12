using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>Marks a gate slab's collider; a ball touching it splits into <see cref="Multiplier"/> copies.</summary>
    public sealed class ZoneBGate : MonoBehaviour
    {
        public int Multiplier;
    }

    /// <summary>Marks a collector's trigger; a ball entering drains for value × <see cref="ScoreMultiplier"/>.</summary>
    public sealed class ZoneBCollector : MonoBehaviour
    {
        public int ScoreMultiplier = 1;
    }

    /// <summary>
    /// A small (fixed-radius) Zone B ball: tier + value, its Box2D body, and the timers the arena
    /// mutates (post-split gate grace, stuck watchdog). Contacts are only REPORTED to the
    /// <see cref="ZoneBSystem"/>, which resolves splits/drains at one safe point per frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ZoneBBall : MonoBehaviour
    {
        public int Tier { get; private set; }
        public double Value { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public CircleCollider2D Collider { get; private set; }
        public SpriteRenderer Face { get; private set; }

        /// <summary>Remaining ms during which gates are ignored (fresh split copy). ≤0 = normal.</summary>
        public float GraceMs;
        /// <summary>Accumulated ms below the rest speed (stuck watchdog).</summary>
        public float SlowMs;
        /// <summary>Claimed by a pending split/drain this frame (dedupe).</summary>
        public bool Claimed;

        ZoneBSystem system;

        public void Configure(ZoneBSystem owner, int tier, double value, float radius)
        {
            system = owner;
            Tier = tier;
            Value = value;
            GraceMs = 0f;
            SlowMs = 0f;
            Claimed = false;
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<CircleCollider2D>();
            Collider.radius = radius;
            if (Face == null) Face = GetComponentInChildren<SpriteRenderer>();
            Face.sprite = BallArt.SpriteForTier(tier);
            Face.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        }

        public Vector2 Position => Body.position;
        public float Speed => Body.linearVelocity.magnitude;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (system == null) return;
            var gate = collision.collider.GetComponent<ZoneBGate>();
            if (gate != null) system.ReportSplit(this, gate.Multiplier);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (system == null) return;
            var collector = other.GetComponent<ZoneBCollector>();
            if (collector != null) system.ReportDrain(this, collector.ScoreMultiplier);
        }
    }
}
