using RichCoast.Core;
using RichCoast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.UI
{
    /// <summary>
    /// Tiny code-first uGUI builders so the HUD is constructed deterministically at runtime (no
    /// hand-authored prefab drift). Reference resolution 1080×2340 portrait, match 0.5.
    /// </summary>
    public static class UiKit
    {
        public const float RefWidth = 1080f, RefHeight = 2340f;
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

        public static Canvas Canvas(string name, Camera camera, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = Panel(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Bold)
        {
            var rt = Panel(parent, name);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static Button Button(Transform parent, string name, string label, Color fill, Color stroke, Color textColor, float textSize)
        {
            var img = Image(parent, name, fill);
            img.raycastTarget = true;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = stroke;
            outline.effectDistance = new Vector2(4f, -4f);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(fill, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(fill, Color.black, 0.2f);
            button.colors = colors;
            var text = Text(img.transform, "Label", label, textSize, textColor);
            Stretch((RectTransform)text.transform);
            return button;
        }

        /// <summary>Bind a uGUI graphic's colour to the active palette (restyled through milestone cross-fades).</summary>
        public static Game.Themed Themed(Graphic graphic, ThemeKey key) =>
            Game.Themed.Bind(graphic, key, () => graphic.color, c => graphic.color = c);

        /// <summary>Bind an outline effect's colour to the active palette.</summary>
        public static Game.Themed Themed(Outline outline, ThemeKey key) =>
            Game.Themed.Bind(outline, key, () => outline.effectColor, c => outline.effectColor = c);

        /// <summary>Anchor a rect to an edge/corner: anchors + pivot to the same point, with a size and offset.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }
    }
}
