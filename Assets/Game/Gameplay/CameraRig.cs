using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// One orthographic camera that fits the BOARD WIDTH to the screen width: extra vertical room on
    /// taller phones becomes headroom, never letterboxing. Two framings, blended by <see cref="Pan"/>:
    /// <list type="bullet">
    /// <item><b>A (pan 0)</b> — the top edge is pinned just above the tray's ceiling with room for the
    /// HUD bar (inset by the safe area); Zone C and the top of Zone B show underneath.</item>
    /// <item><b>B (pan 1)</b> — Zone B's bottom edge (the score bar) is pinned to the screen bottom
    /// (inset by the safe area); a sliver of Zone A stays visible under the HUD.</item>
    /// </list>
    /// On the 390×844 design screen the two differ by exactly <c>DesignSpace.PanDistance</c>; on a
    /// taller phone the B framing simply reveals more of Zone A. M3's milestone zoom becomes an
    /// ortho-size tween of this same camera.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        BoardGeometry geometry;
        float lastAspect = -1f;
        float lastScale = -1f;
        float lastPan = -1f;
        float pan;

        /// <summary>0 = A framing (top pinned), 1 = B framing (bottom pinned). Tweened by the PhaseDirector.</summary>
        public float Pan
        {
            get => pan;
            set => pan = Mathf.Clamp01(value);
        }

        public void Init(Camera cam, BoardGeometry geometry)
        {
            Cam = cam;
            this.geometry = geometry;
            cam.orthographic = true;
            cam.backgroundColor = Theme.Paper;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Apply();
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            if (!Mathf.Approximately(Cam.aspect, lastAspect) || !Mathf.Approximately(geometry.Scale, lastScale) || !Mathf.Approximately(pan, lastPan)) Apply();
        }

        public void Apply()
        {
            lastAspect = Cam.aspect;
            lastScale = geometry.Scale;
            lastPan = pan;
            float halfWidth = geometry.HalfWidth;
            Cam.orthographicSize = halfWidth / Cam.aspect;
            // Safe-area insets (screen px → world units at this framing).
            float unitsPerPx = (halfWidth * 2f) / Screen.width;
            float safeTop = (Screen.height - (Screen.safeArea.y + Screen.safeArea.height)) * unitsPerPx;
            float safeBottom = Screen.safeArea.y * unitsPerPx;
            Cam.transform.position = new Vector3(0f, Mathf.Lerp(FramingAY(safeTop), FramingBY(safeTop, safeBottom), pan), -10f);
        }

        /// <summary>Camera y for the A framing: the top edge pinned above the tray + HUD.</summary>
        float FramingAY(float safeTop)
        {
            float top = geometry.CeilingY + geometry.HudHeight * geometry.Scale + safeTop;
            return top - Cam.orthographicSize;
        }

        /// <summary>Camera y for the B framing: Zone B's bottom pinned to the screen bottom — never above the A framing.</summary>
        float FramingBY(float safeTop, float safeBottom)
        {
            float bottom = geometry.ZoneBBottomY - safeBottom;
            return Mathf.Min(bottom + Cam.orthographicSize, FramingAY(safeTop));
        }
    }
}
