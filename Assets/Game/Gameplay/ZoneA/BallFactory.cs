using System.Collections.Generic;
using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneA
{
    /// <summary>
    /// Builds and pools Zone A balls. Nothing is instantiated once the run is going: a merge
    /// cascade returns two balls and takes one, all from the same pool, so the heaviest gameplay
    /// moment allocates nothing and triggers no GC.
    ///
    /// Also owns the per-tier physics surfaces, which are shared assets rather than one material
    /// per ball — a budget GPU/CPU does not need hundreds of identical PhysicsMaterial2D objects.
    /// </summary>
    public sealed class BallFactory
    {
        private readonly Transform _parent;
        private readonly TierTable _tiers;
        private readonly Board _board;
        private readonly Stack<Ball> _pool = new Stack<Ball>();
        private readonly Dictionary<int, PhysicsMaterial2D> _surfaces = new Dictionary<int, PhysicsMaterial2D>();

        private int _created;

        public BallFactory(Transform parent, TierTable tiers, Board board)
        {
            _parent = parent;
            _tiers = tiers;
            _board = board;
        }

        /// <summary>Total balls ever built — the pool's high-water mark, useful when profiling.</summary>
        public int PoolSize => _created;

        public Ball Spawn(int tier, Vector2 designPosition, Vector2 designVelocity)
        {
            var ball = _pool.Count > 0 ? _pool.Pop() : CreateBall();
            var radius = _tiers.RadiusForTier(tier);

            ball.Configure(tier, radius, MassForTier(tier, radius), SurfaceForTier(tier), _tiers.MaterialForTier(tier));
            ball.gameObject.SetActive(true);
            ball.SetSimulated(true);
            ball.Place(designPosition, designVelocity);
            return ball;
        }

        public void Despawn(Ball ball)
        {
            ball.Hide(); // also hides the view, which lives outside this GameObject
            ball.gameObject.SetActive(false);
            _pool.Push(ball);
        }

        private Ball CreateBall()
        {
            _created++;

            // The view is a SIBLING of the body, never a child of it. A view sizes itself by
            // scaling its own transform, and a collider inherits its transform's scale — so
            // parenting the two would silently scale the physics along with the sprite. Keeping
            // them separate is also what the view seam promises: gameplay pushes a pose at the
            // view and owes it nothing else.
            var view = FlatBallView.Create($"Ball {_created} View", sortingOrder: 10);
            view.transform.SetParent(_parent, worldPositionStays: false);

            var go = new GameObject($"Ball {_created}");
            go.transform.SetParent(_parent, worldPositionStays: false);
            go.layer = _parent.gameObject.layer;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.useAutoMass = false;
            body.freezeRotation = false;
            // Continuous detection is the belt to the speed clamp's braces: a merge blast can fling
            // a small ball hard, and a tunnelled ball is an unrecoverable bug rather than a glitch.
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            go.AddComponent<CircleCollider2D>();

            var ball = go.AddComponent<Ball>();
            ball.Bind(_board, view);
            return ball;
        }

        /// <summary>
        /// Mass from the tapered density: mass ∝ density·r², and the taper makes that grow roughly
        /// linearly in radius above the taper tier, keeping big-ball shoves gentle.
        /// </summary>
        private float MassForTier(int tier, float radius) =>
            BallMath.DensityForTier(_tiers, tier) * Mathf.PI * radius * radius;

        private PhysicsMaterial2D SurfaceForTier(int tier)
        {
            if (_surfaces.TryGetValue(tier, out var surface)) return surface;

            var material = _tiers.MaterialForTier(tier);
            surface = new PhysicsMaterial2D($"Tier{tier}")
            {
                friction = BallMath.FrictionForTier(_tiers, tier),
                bounciness = Tuning.Restitution * material.Def.Physics.RestitutionMult,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _surfaces[tier] = surface;
            return surface;
        }
    }
}
