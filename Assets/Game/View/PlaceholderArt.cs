using UnityEngine;

namespace RichCoast.View
{
    /// <summary>
    /// Grey-box art generated at runtime: one antialiased disc sprite and one flat square,
    /// tinted per-renderer. No art assets, no import settings, nothing to keep in sync — which is
    /// the point, because all of this is thrown away by the art pass.
    ///
    /// Everything shares a single texture and a single material so the placeholder board stays a
    /// handful of draw calls on the target hardware rather than one per ball.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int DiscSize = 128;

        /// <summary>Sprite pixels per world unit. Sprites are scaled by the view, so this is just a base.</summary>
        private const float PixelsPerUnit = 1f;

        private static Sprite _disc;
        private static Sprite _square;

        /// <summary>A soft-edged white disc of diameter 1 world unit at scale 1.</summary>
        public static Sprite Disc => _disc ??= BuildDisc();

        /// <summary>A white 1×1 square — walls, bars, HUD chrome.</summary>
        public static Sprite Square => _square ??= BuildSquare();

        private static Sprite BuildDisc()
        {
            var texture = new Texture2D(DiscSize, DiscSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = "PlaceholderDisc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[DiscSize * DiscSize];
            var center = (DiscSize - 1) * 0.5f;
            var radius = DiscSize * 0.5f - 1f;

            for (var y = 0; y < DiscSize; y++)
            {
                for (var x = 0; x < DiscSize; x++)
                {
                    var dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    // One-pixel feathered edge: enough to stop the crawling aliasing on a small
                    // ball without paying for MSAA, which the perf budget rules out.
                    var alpha = Mathf.Clamp01(radius - dist);
                    // A gentle top-left lift reads as a light source and keeps the grey-box balls
                    // from looking like flat stickers.
                    var lift = Mathf.Clamp01(1f - (y - center * 0.4f) / DiscSize) * 0.25f;
                    var value = (byte)(Mathf.Clamp01(0.78f + lift) * 255f);
                    pixels[y * DiscSize + x] = new Color32(value, value, value, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, DiscSize, DiscSize), new Vector2(0.5f, 0.5f),
                DiscSize * PixelsPerUnit);
            sprite.name = "PlaceholderDisc";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BuildSquare()
        {
            var texture = Texture2D.whiteTexture;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                texture.width * PixelsPerUnit);
            sprite.name = "PlaceholderSquare";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Create a sprite renderer already set up for the placeholder look.</summary>
        public static SpriteRenderer AttachRenderer(GameObject go, Sprite sprite, Color color, int sortingOrder)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
