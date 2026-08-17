using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneA
{
    /// <summary>
    /// One Zone A ball: a circle body plus the view that draws it. Pooled — never destroyed —
    /// because spawning during a merge cascade is exactly when the frame budget is tightest.
    ///
    /// The ball itself holds no rules. It reports its collisions to the <see cref="Board"/> and
    /// exposes its state in DESIGN space, so every rule stays in the pure Core math.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Ball : MonoBehaviour, BallMath.IDoorCandidate
    {
        private Rigidbody2D _body;
        private CircleCollider2D _collider;
        private IBallView _view;
        private Board _board;

        /// <summary>Tier (1-based). Identity, size and value all derive from it.</summary>
        public int Tier { get; private set; }

        public BallSpec Spec => BallSpec.FromTier(Tier);

        /// <summary>Design-space radius; world units are design units, so it is also the collider radius.</summary>
        public float Radius { get; private set; }

        /// <summary>How long this ball has rested above the death line (ms). Reset by any motion.</summary>
        public float RestMs { get; set; }

        /// <summary>True between a merge being decided and the ball being returned to the pool.</summary>
        public bool Consumed { get; set; }

        public Vector2 Position => DesignSpace.ToDesign(_body.position);

        public Vector2 Velocity
        {
            get => DesignSpace.VelocityToDesign(_body.linearVelocity);
            set => _body.linearVelocity = DesignSpace.VelocityToWorld(value);
        }

        public float Speed => _body.linearVelocity.magnitude;

        internal void Bind(Board board, IBallView view)
        {
            _board = board;
            _view = view;
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
        }

        /// <summary>Take on a tier's identity: size, mass, surface and look. Used on spawn and on merge.</summary>
        internal void Configure(int tier, float radius, float mass, float gravityScale, PhysicsMaterial2D surface, TierMaterial material)
        {
            Tier = tier;
            Radius = radius;
            RestMs = 0f;
            Consumed = false;

            _collider.radius = radius;
            _collider.sharedMaterial = surface;
            _body.mass = mass;
            // The world's gravity is authored at arena scale 1; a grown arena scales it per body,
            // which keeps Zone B's much gentler fall independent of Zone A's milestones.
            _body.gravityScale = gravityScale;
            _body.linearDamping = Tuning.FrictionAir * Tuning.StepsPerSecond;

            _view.SetTier(tier, radius, material);
            _view.SetVisible(true);
        }

        internal void Place(Vector2 designPosition, Vector2 designVelocity)
        {
            _body.position = DesignSpace.ToWorld(designPosition);
            _body.rotation = 0f;
            _body.linearVelocity = DesignSpace.VelocityToWorld(designVelocity);
            _body.angularVelocity = 0f;
            SyncView();
        }

        internal void SetSimulated(bool simulated) => _body.simulated = simulated;

        internal void Hide()
        {
            _view.SetVisible(false);
            SetSimulated(false);
        }

        internal void PlayMergePop() => _view.PlayMergePop();

        /// <summary>Push the body's pose at the view. Driven once per frame, not per physics step.</summary>
        internal void SyncView() => _view.SetPose(Position, _body.rotation);

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Merge candidates are only *reported* here; the board resolves them once per frame so
            // a cascade can never process the same ball twice in one step.
            if (_board == null || Consumed) return;
            var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<Ball>() : null;
            if (other == null || other.Consumed) return;
            _board.ReportContact(this, other);
        }
    }
}
