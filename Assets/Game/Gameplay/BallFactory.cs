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
    ///
    /// <see cref="ArenaScale"/> is the milestone arena growth: the tray is fixed, so "the arena got
    /// roomier by ×s" is realised as every ball being 1/s of its ladder size. Because the draw
    /// window shifts up as fast as the balls shrink, the live tiers stay in the same world-size band
    /// for the whole run. Mass is compensated (density × s²) so a shrunken ball keeps the weight its
    /// ladder entry gives it — the tier mass hierarchy never drifts with the scale.
    /// </summary>
    public sealed class BallFactory
    {
        public const int BallLayer = 8;

        readonly Transform parent;
        readonly TierLadder ladder;
        readonly GameFeelSO feel;
        readonly Stack<Ball> pool = new Stack<Ball>();
        readonly string sortingLayer;

        /// <summary>The milestone arena-growth factor in force (1 at boot; the product of every milestone's zoom factor).</summary>
        public float ArenaScale { get; private set; } = 1f;

        public BallFactory(Transform parent, TierLadder ladder, GameFeelSO feel, string sortingLayer = "Default")
        {
            this.parent = parent;
            this.ladder = ladder;
            this.feel = feel;
            this.sortingLayer = sortingLayer;
        }

        public void SetArenaScale(float scale) => ArenaScale = Mathf.Max(0.01f, scale);

        /// <summary>World radius of a tier at the current arena scale (ladder radius ÷ scale).</summary>
        public float RadiusForTier(int tier) => BoardGeometry.Units(ladder.RadiusForTier(tier)) / ArenaScale;

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
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = feel.gravityScale;
            body.linearDamping = feel.linearDamping;
            body.angularDamping = feel.angularDamping;
            body.sleepMode = RigidbodySleepMode2D.StartAwake;
            body.WakeUp();

            ball.Collider.sharedMaterial = BallArt.PhysicsMaterialForTier(tier, ladder);
            ApplyMass(ball);
            return ball;
        }

        /// <summary>
        /// (Re)apply a ball's mass for the current arena scale. Mass comes from density × area, tapered
        /// for big tiers, × the material's density feel; ladder density is per design-px², scaled into
        /// units². The extra × scale² cancels the shrunken area, so the ball weighs what its ladder
        /// entry says at every milestone. Auto-mass must be on BEFORE the density write — Physics2D
        /// rejects (and warns about) a density set on a collider whose body isn't using auto-mass.
        /// </summary>
        public void ApplyMass(Ball ball)
        {
            double designDensity = ladder.DensityForTier(ball.Tier) * Materials.ForTier(ball.Tier).Def.Physics.DensityMult;
            ball.Body.useAutoMass = true;
            ball.Collider.density = (float)(designDensity / (DesignSpace.UnitsPerPixel * DesignSpace.UnitsPerPixel)) * 0.01f * ArenaScale * ArenaScale;
        }

        /// <summary>Return a ball to the pool (hidden, physics-inert).</summary>
        public void Despawn(Ball ball)
        {
            if (ball == null) return;
            ball.Body.simulated = true;
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

            // Body → View (squash pivot: rotates/scales in the contact frame) → Face (the sprite,
            // counter-rotated so it only ever turns with the body; see BallView).
            var viewGo = new GameObject("View");
            viewGo.transform.SetParent(go.transform, false);
            var faceGo = new GameObject("Face");
            faceGo.transform.SetParent(viewGo.transform, false);
            var sr = faceGo.AddComponent<SpriteRenderer>();
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
