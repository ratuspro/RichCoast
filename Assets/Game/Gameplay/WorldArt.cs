using TMPro;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Tiny world-space drawing helpers shared by the zone builders: flat sprite rectangles, pine
    /// rails, filled polygons, and world-space TextMeshPro labels sized in design px. Everything is
    /// procedural (no image assets), sorted by <c>sortingOrder</c> on the Default layer.
    /// </summary>
    public static class WorldArt
    {
        static TMP_FontAsset font;

        public static TMP_FontAsset Font
        {
            get
            {
                if (font != null) return font;
                font = TMP_Settings.defaultFontAsset;
                if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                return font;
            }
        }

        /// <summary>An axis-aligned flat rectangle centred at <paramref name="centre"/>.</summary>
        public static SpriteRenderer Rect(Transform parent, string name, Vector2 centre, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallArt.WhitePixel;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A rectangle spanning [x0,x1] × [y0,y1].</summary>
        public static SpriteRenderer Rect(Transform parent, string name, float x0, float x1, float y0, float y1, Color color, int order) =>
            Rect(parent, name, new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f), new Vector2(x1 - x0, y1 - y0), color, order);

        /// <summary>A thick stroke from a to b (the ends extend by half the width, so joints overlap cleanly).</summary>
        public static SpriteRenderer Rail(Transform parent, Vector2 a, Vector2 b, float width, Color color, int order, string name = "Rail")
        {
            var d = b - a;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) / 2f;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(d.magnitude + width, width, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallArt.WhitePixel;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A filled convex polygon (fan-triangulated from the first point).</summary>
        public static MeshRenderer Quad(Transform parent, string name, Vector2[] points, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = name };
            var verts = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) verts[i] = points[i];
            var tris = new int[(points.Length - 2) * 3];
            for (int i = 0; i < points.Length - 2; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = i + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };
            mr.sortingOrder = order;
            return mr;
        }

        /// <summary>
        /// A world-space TextMeshPro label whose cap height reads as <paramref name="designPx"/> on
        /// the design screen. TMP's 3D text scales font size by 0.1 per world unit, hence the ×10.
        /// </summary>
        public static TextMeshPro Text(Transform parent, string name, string text, float designPx, Color color, int order, FontStyles style = FontStyles.Bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = BoardGeometry.Units(designPx) * 10f;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);
            tmp.sortingOrder = order;
            return tmp;
        }
    }
}
