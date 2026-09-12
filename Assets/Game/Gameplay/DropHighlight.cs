using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Drop-candidate glow: a soft brass halo that breathes UNDER the ball a trap-door tap would grab
    /// (port of Zone A's <c>updateDropHighlight</c>). Position, size and a cyclic pulse are recomputed
    /// each frame — the candidate changes as balls settle, and a fresh nearest is picked the instant
    /// one is sucked away. It sits below the ball sprites so it reads as a rim glow, never a tint.
    /// </summary>
    public sealed class DropHighlight
    {
        const float ScaleMin = 1.15f, ScaleMax = 1.45f;
        const float AlphaMin = 0.3f, AlphaMax = 0.85f;
        const float PulseMs = 1100f;

        readonly SpriteRenderer glow;
        float phaseMs;

        public DropHighlight(Transform parent)
        {
            var go = new GameObject("DropGlow");
            go.transform.SetParent(parent, false);
            glow = go.AddComponent<SpriteRenderer>();
            glow.sprite = BallArt.SoftDot;
            glow.sortingOrder = 9; // just under the ball faces (10)
            glow.enabled = false;
        }

        /// <summary>Track <paramref name="ball"/> (or hide when null).</summary>
        public void Track(Ball ball, float deltaMs)
        {
            if (ball == null)
            {
                glow.enabled = false;
                return;
            }
            phaseMs += deltaMs;
            float breathe = 0.5f + 0.5f * Mathf.Sin(phaseMs * 2f * Mathf.PI / PulseMs);
            float scale = Mathf.Lerp(ScaleMin, ScaleMax, breathe);
            float d = ball.Radius * 2f * scale;
            glow.transform.position = ball.Position;
            glow.transform.localScale = new Vector3(d, d, 1f);
            var c = Theme.BrassBright;
            glow.color = new Color(c.r, c.g, c.b, Mathf.Lerp(AlphaMin, AlphaMax, breathe));
            glow.enabled = true;
        }
    }
}
