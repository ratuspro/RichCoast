using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Procedural ball faces — a CPU port of the Phaser <c>MaterialPainter</c> base recipe: a
    /// top-left-lit sphere in the material's base colour, a glossy highlight, a darkened rim, one
    /// gold ring per ladder cycle, plus a simplified per-family detail pass. No image assets.
    ///
    /// One <see cref="Sprite"/> per TIER is cached at a fixed texel size and sized in world units by
    /// the caller's transform scale (sprite = 1 unit diameter at PPU = size), so any radius shares
    /// a texture. Everything is deterministic (seeded per tier) so regenerated faces are stable.
    /// </summary>
    public static class BallArt
    {
        public const int TextureSize = 192;

        static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, PhysicsMaterial2D> physicsMaterials = new Dictionary<int, PhysicsMaterial2D>();
        static Sprite whitePixel;
        static Sprite softDot;

        public static Color Rgb(uint hex, float a = 1f) =>
            new Color(((hex >> 16) & 0xff) / 255f, ((hex >> 8) & 0xff) / 255f, (hex & 0xff) / 255f, a);

        /// <summary>A 4×4 white sprite (PPU 4 → exactly 1 world unit) for rectangles, rails, and lines.</summary>
        public static Sprite WhitePixel
        {
            get
            {
                if (whitePixel != null) return whitePixel;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "white4", filterMode = FilterMode.Bilinear };
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                whitePixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                whitePixel.name = "white";
                return whitePixel;
            }
        }

        /// <summary>A soft radial white dot (1 unit across) — tinted for sparks, glows and trails.</summary>
        public static Sprite SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "softdot", filterMode = FilterMode.Bilinear };
                var px = new Color[n * n];
                float r = n / 2f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = d < 0.6f ? 1f : Mathf.Clamp01(1f - (d - 0.6f) / 0.4f);
                    px[y * n + x] = new Color(1, 1, 1, a * a);
                }
                tex.SetPixels(px);
                tex.Apply();
                softDot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                softDot.name = "softdot";
                return softDot;
            }
        }

        /// <summary>A crisp anti-aliased white disc (1 unit across) — for markers, studs and hard dots.</summary>
        public static Sprite Disc
        {
            get
            {
                if (disc != null) return disc;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "disc", filterMode = FilterMode.Bilinear };
                var px = new Color[n * n];
                float r = n / 2f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d); // one-texel AA edge
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
                tex.SetPixels(px);
                tex.Apply();
                disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                disc.name = "disc";
                return disc;
            }
        }

        static Sprite disc;

        /// <summary>The cached face sprite for a tier (1 unit diameter at scale 1).</summary>
        public static Sprite SpriteForTier(int tier)
        {
            if (sprites.TryGetValue(tier, out var s) && s != null) return s;
            s = Paint(tier);
            sprites[tier] = s;
            return s;
        }

        /// <summary>Per-tier Box2D material: the ladder's friction ramp + restitution × material feel.</summary>
        public static PhysicsMaterial2D PhysicsMaterialForTier(int tier, TierLadder ladder)
        {
            if (physicsMaterials.TryGetValue(tier, out var m) && m != null) return m;
            m = new PhysicsMaterial2D($"ball-t{tier}")
            {
                friction = (float)ladder.FrictionForTier(tier),
                bounciness = (float)ladder.RestitutionForTier(tier),
            };
            physicsMaterials[tier] = m;
            return m;
        }

        // --- painter ---------------------------------------------------------------------

        static Sprite Paint(int tier)
        {
            var mat = Materials.ForTier(tier);
            var def = mat.Def;
            int n = TextureSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = $"ball-t{tier}", filterMode = FilterMode.Trilinear };
            var px = new Color[n * n];

            float c = n / 2f;
            float lineW = Mathf.Max(2f, c * 0.08f);
            float r = c - lineW / 2f; // painted disc radius, rim stroke centred on its edge
            var baseCol = Rgb(def.BaseColor);
            var accent = Rgb(def.AccentColor);
            var lit = Color.Lerp(baseCol, Color.white, 0.28f);
            var dark = Color.Lerp(baseCol, Color.black, 0.22f);
            var rim = Color.Lerp(baseCol, Color.black, 0.5f);
            var rng = new System.Random(tier * 7919);
            var detail = BuildDetail(def, r, rng);

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = x + 0.5f - c;
                float fy = c - (y + 0.5f); // y-up in the painter's frame
                float d = Mathf.Sqrt(fx * fx + fy * fy);
                float edge = d - (r + lineW / 2f);
                if (edge > 0.75f) { px[y * n + x] = Color.clear; continue; }
                float coverage = Mathf.Clamp01(0.75f - edge); // 1 inside, fading over ~1.5 texel at the edge

                // Base: soft top-left-lit sphere (gradient centred up-left, darkening to the rim).
                float gx = fx + r * 0.35f, gy = fy - r * 0.4f;
                float g = Mathf.Clamp01((Mathf.Sqrt(gx * gx + gy * gy) - r * 0.15f) / (r * 0.85f));
                Color col = g < 0.7f ? Color.Lerp(lit, baseCol, g / 0.7f) : Color.Lerp(baseCol, dark, (g - 0.7f) / 0.3f);

                // Detail pass (clipped to the disc).
                if (d < r) col = detail(fx, fy, col);

                // Glossy highlight up-left.
                float hx = fx + r * 0.4f, hy = fy - r * 0.45f;
                float h = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / (r * 0.45f));
                col = Color.Lerp(col, Color.white, 0.3f * h * h);

                // Rim: darkened base colour.
                float rimT = Mathf.Clamp01(1f - Mathf.Abs(d - r) / (lineW / 2f));
                col = Color.Lerp(col, rim, 0.75f * rimT);

                // One gold ring per completed ladder cycle.
                for (int i = 1; i <= mat.Cycle; i++)
                {
                    float ringR = r - lineW * (0.5f + i * 1.6f);
                    float t = Mathf.Clamp01(1f - Mathf.Abs(d - ringR) / Mathf.Max(0.75f, lineW * 0.3f));
                    col = Color.Lerp(col, Rgb(0xf2b024), 0.9f * t);
                }

                col.a = coverage;
                px[y * n + x] = col;
            }

            tex.SetPixels(px);
            tex.Apply(true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
            sprite.name = $"ball-t{tier}";
            return sprite;
        }

        delegate Color DetailFn(float fx, float fy, Color under);

        /// <summary>Simplified per-family detail: enough to make the tiers read as materials at a glance.</summary>
        static DetailFn BuildDetail(MaterialDef def, float r, System.Random rng)
        {
            var accent = Rgb(def.AccentColor);
            var baseCol = Rgb(def.BaseColor);
            switch (def.Detail)
            {
                case MaterialDetail.Grain:
                    return (fx, fy, under) =>
                    {
                        float cx = fx + r * 0.25f, cy = fy + r * 0.1f;
                        float d = Mathf.Sqrt(cx * cx + cy * cy);
                        float w = Mathf.Max(1.5f, r * 0.045f);
                        float t = 0f;
                        for (int i = 0; i < 3; i++)
                        {
                            float ring = r * (0.3f + i * 0.28f);
                            t = Mathf.Max(t, Mathf.Clamp01(1f - Mathf.Abs(d - ring) / w));
                        }
                        float ang = Mathf.Atan2(cy, cx);
                        if (ang < -0.6f || ang > Mathf.PI * 0.75f) t = 0f;
                        return Color.Lerp(under, accent, 0.8f * t);
                    };
                case MaterialDetail.Speckle:
                case MaterialDetail.Specks:
                {
                    int count = def.Detail == MaterialDetail.Speckle ? Mathf.Max(10, Mathf.RoundToInt(r * 0.15f)) : 9;
                    var dots = new List<Vector3>();
                    for (int i = 0; i < count; i++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2;
                        float dd = Mathf.Sqrt((float)rng.NextDouble()) * r * 0.85f;
                        float dr = def.Detail == MaterialDetail.Speckle ? r * (0.05f + (float)rng.NextDouble() * 0.07f) : r * (0.02f + (float)rng.NextDouble() * 0.045f);
                        dots.Add(new Vector3(Mathf.Cos(a) * dd, Mathf.Sin(a) * dd, Mathf.Max(dr, 1.2f)));
                    }
                    return (fx, fy, under) =>
                    {
                        float t = 0f;
                        foreach (var dot in dots)
                        {
                            float dx = fx - dot.x, dy = fy - dot.y;
                            t = Mathf.Max(t, Mathf.Clamp01(1f - (Mathf.Sqrt(dx * dx + dy * dy) - dot.z + 0.75f) / 1.5f));
                        }
                        return Color.Lerp(under, accent, 0.85f * t);
                    };
                }
                case MaterialDetail.Sheen:
                    return (fx, fy, under) =>
                    {
                        // Diagonal brushed band catching the light.
                        float v = (fx + fy) * 0.7071f; // rotate -45°
                        float band = Mathf.Clamp01(1f - Mathf.Abs(v + r * 0.22f) / (r * 0.32f));
                        return Color.Lerp(under, accent, 0.55f * band);
                    };
                case MaterialDetail.Rivets:
                    return (fx, fy, under) =>
                    {
                        const int n = 7;
                        float t = 0f;
                        for (int i = 0; i < n; i++)
                        {
                            float a = i / (float)n * Mathf.PI * 2 + Mathf.PI / 2;
                            float dx = fx - Mathf.Cos(a) * r * 0.78f, dy = fy - Mathf.Sin(a) * r * 0.78f;
                            t = Mathf.Max(t, Mathf.Clamp01(1f - (Mathf.Sqrt(dx * dx + dy * dy) - r * 0.06f + 0.75f) / 1.5f));
                        }
                        return Color.Lerp(under, accent, 0.85f * t);
                    };
                case MaterialDetail.Glint:
                    return (fx, fy, under) =>
                    {
                        float d = Mathf.Sqrt(fx * fx + fy * fy);
                        float ang = Mathf.Atan2(fy, fx);
                        bool inArc = ang > Mathf.PI * 0.1f && ang < Mathf.PI * 0.45f;
                        float t = inArc ? Mathf.Clamp01(1f - Mathf.Abs(d - r * 0.8f) / Mathf.Max(1.5f, r * 0.03f)) : 0f;
                        return Color.Lerp(under, accent, 0.8f * t);
                    };
                case MaterialDetail.Crescent:
                    return (fx, fy, under) =>
                    {
                        float d = Mathf.Sqrt(fx * fx + fy * fy);
                        float ang = Mathf.Atan2(-fy, fx);
                        bool inArc = ang > Mathf.PI * 0.05f && ang < Mathf.PI * 0.55f;
                        float t = inArc ? Mathf.Clamp01(1f - Mathf.Abs(d - r * 0.72f) / Mathf.Max(2f, r * 0.07f)) : 0f;
                        return Color.Lerp(under, accent, 0.6f * t);
                    };
                case MaterialDetail.Facets:
                    return (fx, fy, under) =>
                    {
                        float d = Mathf.Sqrt(fx * fx + fy * fy);
                        if (d < r * 0.45f)
                        {
                            var table = Color.Lerp(baseCol, Color.white, 0.2f);
                            float seam = Mathf.Clamp01(1f - Mathf.Abs(d - r * 0.45f) / Mathf.Max(1f, r * 0.02f));
                            return Color.Lerp(Color.Lerp(under, table, 0.9f), Color.white, 0.5f * seam);
                        }
                        float ang = Mathf.Repeat(Mathf.Atan2(fy, fx) - 0.3f, Mathf.PI * 2);
                        int wedge = Mathf.FloorToInt(ang / (Mathf.PI * 2 / 5));
                        var fill = wedge % 2 == 0 ? Color.Lerp(under, accent, 0.5f) : Color.Lerp(under, Color.Lerp(baseCol, Color.black, 0.25f), 0.4f);
                        float edge = Mathf.Repeat(ang, Mathf.PI * 2 / 5) * d;
                        float line = Mathf.Clamp01(1f - edge / Mathf.Max(1f, r * 0.02f));
                        return Color.Lerp(fill, Color.white, 0.4f * line);
                    };
                case MaterialDetail.Matte:
                    return (fx, fy, under) =>
                    {
                        float t = 0f;
                        foreach (float dy in new[] { -0.35f, 0.05f, 0.45f })
                        {
                            float yy = -fy - r * dy + r * 0.08f * (1f - fx * fx / (r * r));
                            t = Mathf.Max(t, Mathf.Clamp01(1f - Mathf.Abs(yy) / Mathf.Max(1.5f, r * 0.04f)));
                        }
                        return Color.Lerp(under, accent, 0.7f * t);
                    };
                case MaterialDetail.Glow:
                    return (fx, fy, under) =>
                    {
                        float d = Mathf.Sqrt(fx * fx + fy * fy) / r;
                        float a = d < 0.55f ? Mathf.Lerp(0.95f, 0.25f, d / 0.55f) : Mathf.Lerp(0.25f, 0f, (d - 0.55f) / 0.45f);
                        return Color.Lerp(under, accent, a);
                    };
                case MaterialDetail.Corona:
                    return (fx, fy, under) =>
                    {
                        float d = Mathf.Sqrt(fx * fx + fy * fy) / r;
                        var halo = Color.Lerp(baseCol, Color.white, 0.3f);
                        if (d < 0.35f) return Color.Lerp(under, accent, Mathf.Lerp(1f, 0.55f, d / 0.35f));
                        if (d < 0.6f) return Color.Lerp(under, accent, Mathf.Lerp(0.55f, 0f, (d - 0.35f) / 0.25f));
                        if (d < 0.85f) return Color.Lerp(under, halo, Mathf.Lerp(0f, 0.35f, (d - 0.6f) / 0.25f));
                        return Color.Lerp(under, halo, Mathf.Lerp(0.35f, 0f, (d - 0.85f) / 0.15f));
                    };
                case MaterialDetail.Crust:
                {
                    // Dark cooled plates: a few random cracks from the rim toward the centre.
                    var cracks = new List<Vector4>();
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2;
                        float x0 = Mathf.Cos(a) * r * 0.9f, y0 = Mathf.Sin(a) * r * 0.9f;
                        float x1 = ((float)rng.NextDouble() - 0.5f) * r * 0.6f, y1 = ((float)rng.NextDouble() - 0.5f) * r * 0.6f;
                        cracks.Add(new Vector4(x0, y0, x1, y1));
                    }
                    float w = Mathf.Max(1.5f, r * 0.045f);
                    return (fx, fy, under) =>
                    {
                        float t = 0f;
                        foreach (var k in cracks)
                        {
                            var p = new Vector2(fx, fy);
                            var a = new Vector2(k.x, k.y);
                            var b = new Vector2(k.z, k.w);
                            float seg = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                            float dist = Vector2.Distance(p, a + (b - a) * seg);
                            t = Mathf.Max(t, Mathf.Clamp01(1f - dist / w));
                        }
                        return Color.Lerp(under, accent, 0.9f * t);
                    };
                }
                case MaterialDetail.Gloss:
                default:
                    return (fx, fy, under) =>
                    {
                        float gx = fx - r * 0.25f, gy = fy + r * 0.3f;
                        float t = Mathf.Clamp01(1f - Mathf.Sqrt(gx * gx + gy * gy) / (r * 0.8f));
                        return Color.Lerp(under, accent, 0.45f * t);
                    };
            }
        }
    }
}
