using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// One orthographic camera that fits the BOARD WIDTH to the screen width: extra vertical room on
    /// taller phones becomes headroom, never letterboxing. Two framings, blended by <see cref="Pan"/>:
    /// <list type="bullet">
    /// <item><b>A (pan 0)</b> — frames the grown tray (<c>10 × ViewScale</c> units wide) with its top
    /// edge pinned just above the ceiling with room for the HUD bar (inset by the safe area); Zone C
    /// and the top of Zone B show underneath, at 1/ViewScale once the arena has grown.</item>
    /// <item><b>B (pan 1)</b> — frames Zone B at its native 10-unit width with its bottom edge (the
    /// score bar) pinned to the screen bottom (inset by the safe area); a sliver of Zone A stays
    /// visible under the HUD.</item>
    /// </list>
    /// On the 390×844 design screen the two differ by exactly <c>DesignSpace.PanDistance</c> at scale 1.
    /// <see cref="ViewScale"/> is the milestone zoom: <see cref="ArenaGrowth"/> tweens it up to the
    /// new <see cref="BoardGeometry.Scale"/> while the physics have already snapped there, so the
    /// zoom-out is one smooth ortho-size tween of this same camera.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        BoardGeometry geometry;
        float lastAspect = -1f;
        float lastViewScale = -1f;
        float lastPan = -1f;
        float pan;
        float viewScale = 1f;

        /// <summary>0 = A framing (top pinned), 1 = B framing (bottom pinned). Tweened by the PhaseDirector.</summary>
        public float Pan
        {
            get => pan;
            set => pan = Mathf.Clamp01(value);
        }

        /// <summary>The arena scale the A framing currently shows (tweened toward <see cref="BoardGeometry.Scale"/> by the milestone zoom).</summary>
        public float ViewScale
        {
            get => viewScale;
            set => viewScale = Mathf.Max(0.01f, value);
        }

        public void Init(Camera cam, BoardGeometry geometry)
        {
            Cam = cam;
            this.geometry = geometry;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Themed.Bind(cam, ThemeKey.Paper);
            viewScale = geometry.Scale;
            Apply();
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            if (!Mathf.Approximately(Cam.aspect, lastAspect) || !Mathf.Approximately(viewScale, lastViewScale) || !Mathf.Approximately(pan, lastPan)) Apply();
        }

        public void Apply()
        {
            lastAspect = Cam.aspect;
            lastViewScale = viewScale;
            lastPan = pan;
            // Framed half-width: the grown tray in A, Zone B's native width in B.
            float baseHalf = BoardGeometry.Units(DesignSpace.Width / 2);
            float halfWidth = baseHalf * Mathf.Lerp(viewScale, 1f, pan);
            Cam.orthographicSize = halfWidth / Cam.aspect;
            // Safe-area insets (screen px → world units at this framing).
            float unitsPerPx = (halfWidth * 2f) / Screen.width;
            float safeTop = (Screen.height - (Screen.safeArea.y + Screen.safeArea.height)) * unitsPerPx;
            float safeBottom = Screen.safeArea.y * unitsPerPx;
            Cam.transform.position = new Vector3(0f, Mathf.Lerp(FramingAY(safeTop), FramingBY(safeTop, safeBottom), pan), -10f);
        }

        /// <summary>Camera y for the A framing: the top edge pinned above the (shown) tray + HUD.</summary>
        float FramingAY(float safeTop)
        {
            float top = (BoardGeometry.Units(DesignSpace.BoardHeight) + geometry.HudHeight) * viewScale + safeTop;
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
