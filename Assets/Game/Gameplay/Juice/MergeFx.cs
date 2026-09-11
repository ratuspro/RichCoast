using PrimeTween;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The merge pop: a ring flash that expands from the merged ball's rim and fades, plus a burst
    /// of accent-tinted sparks off the rim (a pooled ParticleSystem). Sizes/speeds scale with the
    /// arena so the spray reads the same at every milestone.
    /// </summary>
    public sealed class MergeFx
    {
        readonly ParticleSystem sparks;
        readonly ParticleSystemRenderer sparksRenderer;
        readonly Transform parent;
        readonly GameFeelSO feel;
        readonly BoardGeometry geometry;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();

        public MergeFx(Transform parent, GameFeelSO feel, BoardGeometry geometry)
        {
            this.parent = parent;
            this.feel = feel;
            this.geometry = geometry;

            var go = new GameObject("MergeSparks");
            go.transform.SetParent(parent, false);
            sparks = go.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = feel.burstLifeMs / 1000f;
            main.startSpeed = 0f;
            main.maxParticles = 512;
            main.gravityModifier = 1.2f;
            var emission = sparks.emission;
            emission.enabled = false;
            var col = sparks.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            sparksRenderer = go.GetComponent<ParticleSystemRenderer>();
            sparksRenderer.material = new Material(Shader.Find("Sprites/Default"));
            sparksRenderer.material.mainTexture = BallArt.SoftDot.texture;
            sparksRenderer.sortingOrder = 30;
        }

        public void Play(Vector2 origin, int mergedTier, float radius)
        {
            float s = geometry.Scale;
            var accent = BallArt.Rgb(Materials.ForTier(mergedTier).Def.AccentColor);

            // Sparks along the merged ball's rim, flying outward.
            for (int i = 0; i < feel.burstParticles; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                emit.position = origin + dir * radius;
                emit.velocity = dir * Random.Range(feel.burstSpeedMin, feel.burstSpeedMax) * s;
                emit.startSize = Random.Range(0.12f, 0.22f) * s;
                emit.startColor = Color.Lerp(accent, Color.white, Random.value * 0.5f);
                emit.startLifetime = feel.burstLifeMs / 1000f * Random.Range(0.6f, 1f);
                sparks.Emit(emit, 1);
            }

            // Flash ring: a soft dot that expands from the rim and fades (additive-ish white).
            var ring = new GameObject("Flash");
            ring.transform.SetParent(parent, false);
            ring.transform.position = origin;
            var sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = BallArt.SoftDot;
            sr.color = new Color(1f, 1f, 1f, 0.85f);
            sr.sortingOrder = 29;
            float from = radius * 2f;
            float to = radius * 2f * feel.flashRingScale;
            Tween.Custom(sr, 0f, 1f, feel.flashRingMs / 1000f, (r, t) =>
            {
                float d = Mathf.Lerp(from, to, t);
                r.transform.localScale = new Vector3(d, d, 1f);
                r.color = new Color(1f, 1f, 1f, 0.85f * (1f - t));
            }, Ease.OutCubic).OnComplete(() => Object.Destroy(ring));
        }
    }
}
