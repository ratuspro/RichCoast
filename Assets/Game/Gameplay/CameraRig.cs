using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// One orthographic camera that fits the BOARD WIDTH to the screen width: extra vertical room on
    /// taller phones becomes headroom below the tray (where Zone B will live), never letterboxing.
    /// The top edge is pinned just above the tray's ceiling with room for the HUD bar (inset by the
    /// safe area so a notch never covers the board). M2's phase pan and M3's milestone zoom become
    /// tweens of this one camera's position / ortho size.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        BoardGeometry geometry;
        float lastAspect = -1f;
        float lastScale = -1f;

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
            if (!Mathf.Approximately(Cam.aspect, lastAspect) || !Mathf.Approximately(geometry.Scale, lastScale)) Apply();
        }

        public void Apply()
        {
            lastAspect = Cam.aspect;
            lastScale = geometry.Scale;
            float halfWidth = geometry.HalfWidth;
            Cam.orthographicSize = halfWidth / Cam.aspect;
            // Safe-area top inset (screen px → world units at this framing).
            float unitsPerPx = (halfWidth * 2f) / Screen.width;
            float safeTop = (Screen.height - (Screen.safeArea.y + Screen.safeArea.height)) * unitsPerPx;
            float top = geometry.CeilingY + geometry.HudHeight * geometry.Scale + safeTop;
            Cam.transform.position = new Vector3(0f, top - Cam.orthographicSize, -10f);
        }
    }
}
