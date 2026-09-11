using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Builds and pools Zone A balls: a Rigidbody2D + CircleCollider2D + child SpriteRenderer, with
    /// per-tier radius, Box2D material (friction/bounce), and density (mass) from the
    /// <see cref="TierLadder"/> × the material's physics feel. Ball objects are recycled so merges
    /// don't churn the GC on mobile.
    /// </summary>
    public sealed class BallFactory
    {
        public const int BallLayer = 8;

        readonly Transform parent;
        readonly TierLadder ladder;
        readonly GameFeelSO feel;
        readonly Stack<Ball> pool = new Stack<Ball>();
        readonly string sortingLayer;

        public BallFactory(Transform parent, TierLadder ladder, GameFeelSO feel, string sortingLayer = "Default")
        {
            this.parent = parent;
            this.ladder = ladder;
            this.feel = feel;
            this.sortingLayer = sortingLayer;
        }

        public float RadiusForTier(int tier) => BoardGeometry.Units(ladder.RadiusForTier(tier));

        /// <summary>Create (or recycle) a live, physics-driven ball at a world position.</summary>
        public Ball Spawn(Board board, Vector2 position, int tier)
        {
            Ball ball = pool.Count > 0 ? pool.Pop() : Build();
            var go = ball.gameObject;
            go.name = $"Ball t{tier}";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(true);

            float radius = RadiusForTier(tier);
            ball.Configure(board, tier, radius);

            var body = ball.Body;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = feel.gravityScale;
            body.linearDamping = feel.linearDamping;
            body.angularDamping = feel.angularDamping;
            body.sleepMode = RigidbodySleepMode2D.StartAwake;
            body.WakeUp();

            var col = ball.Collider;
            col.sharedMaterial = BallArt.PhysicsMaterialForTier(tier, ladder);
            // Mass from density × area, tapered for big tiers, × the material's density feel.
            // Ladder density is per design-px²; scale into units² so the absolute masses stay sane.
            // Auto-mass must be on BEFORE the density write — Physics2D rejects (and warns about)
            // a density set on a collider whose body isn't yet using auto-mass.
            double designDensity = ladder.DensityForTier(tier) * Materials.ForTier(tier).Def.Physics.DensityMult;
            body.useAutoMass = true;
            col.density = (float)(designDensity / (DesignSpace.UnitsPerPixel * DesignSpace.UnitsPerPixel)) * 0.01f;
            return ball;
        }

        /// <summary>Return a ball to the pool (hidden, physics-inert).</summary>
        public void Despawn(Ball ball)
        {
            if (ball == null) return;
            ball.Body.linearVelocity = Vector2.zero;
            ball.gameObject.SetActive(false);
            pool.Push(ball);
        }

        Ball Build()
        {
            var go = new GameObject("Ball");
            go.layer = BallLayer;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.None;
            go.AddComponent<CircleCollider2D>();

            var viewGo = new GameObject("View");
            viewGo.transform.SetParent(go.transform, false);
            var sr = viewGo.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = 10;
            var view = viewGo.AddComponent<BallView>();
            view.Init(feel);

            var ball = go.AddComponent<Ball>();
            go.SetActive(false);
            return ball;
        }
    }
}
