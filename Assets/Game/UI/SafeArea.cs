using UnityEngine;

namespace RichCoast.UI
{
    /// <summary>
    /// Keeps a full-stretch RectTransform inside <see cref="Screen.safeArea"/> (notches, home bars).
    /// Re-applies when the screen or orientation changes. Put HUD chrome under this container.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last = Rect.zero;
        Vector2Int lastScreen;

        void Update()
        {
            if (Screen.safeArea == last && lastScreen.x == Screen.width && lastScreen.y == Screen.height) return;
            Apply();
        }

        public void Apply()
        {
            last = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var rt = (RectTransform)transform;
            var min = last.position;
            var max = last.position + last.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
