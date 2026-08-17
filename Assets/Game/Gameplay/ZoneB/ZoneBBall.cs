using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneB
{
    /// <summary>
    /// One ball in the split arena. Small, numerous and short-lived: it falls, splits at gates,
    /// and dies in a collector.
    ///
    /// Like Zone A's ball it carries no rules — it reports gate and collector touches to
    /// <see cref="ZoneBSystem"/>, which decides what they mean.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ZoneBBall : MonoBehaviour
    {
        private Rigidbody2D _body;
        private CircleCollider2D _collider;
        private IBallView _view;
        private ZoneBSystem _system;
        private float _graceMs;

        public BallSpec Spec { get; private set; }

        /// <summary>True while this ball is out of play (collected or split) and awaiting the pool.</summary>
        public bool Consumed { get; set; }

        /// <summary>Freshly-split balls ignore gates until their grace expires.</summary>
        public bool IgnoresGates => _graceMs > 0f;

        public Vector2 Position => DesignSpace.ToDesign(_body.position);

        internal void Bind(ZoneBSystem system, IBallView view)
        {
            _system = system;
            _view = view;
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
        }

        internal void Configure(BallSpec spec, TierMaterial material, PhysicsMaterial2D surface, float graceMs)
        {
            Spec = spec;
            Consumed = false;
            _graceMs = graceMs;
            RestMs = 0f;

            _collider.radius = Tuning.ZoneBBallRadius;
            _collider.sharedMaterial = surface;
            _body.linearDamping = Tuning.ZoneBFrictionAir * Tuning.StepsPerSecond;

            _view.SetTier(spec.Tier, Tuning.ZoneBBallRadius, material);
            _view.SetVisible(true);
        }

        internal void Place(Vector2 designPosition, Vector2 designVelocity)
        {
            _body.position = DesignSpace.ToWorld(designPosition);
            _body.linearVelocity = DesignSpace.VelocityToWorld(designVelocity);
            _body.angularVelocity = 0f;
            SyncView();
        }

        internal void Hide()
        {
            _view.SetVisible(false);
            _body.simulated = false;
        }

        internal void Wake() => _body.simulated = true;

        /// <summary>
        /// How long this ball has been sitting still (ms). A ball that has stopped is a ball the
        /// funnel will never deliver, so the system drains it rather than letting it hold the round
        /// — and the trap-door — open forever.
        /// </summary>
        public float RestMs { get; private set; }

        internal void Tick(float deltaMs)
        {
            if (_graceMs > 0f) _graceMs = Mathf.Max(0f, _graceMs - deltaMs);
            RestMs = _body.linearVelocity.sqrMagnitude < RestSpeedSquared ? RestMs + deltaMs : 0f;
            SyncView();
        }

        /// <summary>
        /// Below this speed (design units/second) a ball counts as parked. Set well under a real
        /// fall — a ball in play moves at hundreds of units a second — but above the crawl of a
        /// ball nudging along a shelf, which would otherwise hold a round open indefinitely.
        /// </summary>
        private const float RestSpeed = 45f;
        private const float RestSpeedSquared = RestSpeed * RestSpeed;

        internal void SyncView() => _view.SetPose(Position, _body.rotation);

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Consumed || _system == null) return;
            var gate = collision.collider.GetComponent<Gate>();
            if (gate != null) _system.ReportGateHit(this, gate);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Consumed || _system == null) return;
            var collector = other.GetComponent<Collector>();
            if (collector != null) _system.ReportCollected(this, collector);
        }
    }
}
