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
    /// taller phone the B framing simply reveals more of Zone A. The camera never zooms: milestone
    /// arena growth shrinks Zone A's balls in place (<see cref="ArenaGrowth"/>), so every zone keeps
    /// its screen size for the whole run.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        BoardGeometry geometry;
        float lastAspect = -1f;
        float lastPan = -1f;
        float pan;
        float shakeMs, shakeDurationMs, shakeAmp, shakePhase;

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
            cam.clearFlags = CameraClearFlags.SolidColor;
            Themed.Bind(cam, RichCoast.Core.ThemeKey.Paper);
            Apply();
        }

        /// <summary>
        /// Rattle the camera — the cabinet being shaken. A transient offset added ON TOP of whatever
        /// framing the pan computed, deliberately not a second transform: <see cref="Pan"/> owns the
        /// camera's position, and a rival writer would fight it mid-pan.
        /// </summary>
        public void Shake(float amplitudeDesignPx, float ms)
        {
            shakeAmp = BoardGeometry.Units(amplitudeDesignPx);
            shakeDurationMs = Mathf.Max(1f, ms);
            shakeMs = shakeDurationMs;
            shakePhase = UnityEngine.Random.Range(0f, 10f);
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            bool shaking = shakeMs > 0f;
            // Decrement BEFORE the Apply so the frame the shake expires re-centres the camera; the
            // aspect/pan cache would otherwise leave it parked at the last offset.
            if (shaking) shakeMs -= Time.deltaTime * 1000f;
            if (shaking || !Mathf.Approximately(Cam.aspect, lastAspect) || !Mathf.Approximately(pan, lastPan)) Apply();
        }

        float ShakeFalloff => shakeMs <= 0f ? 0f : shakeMs / shakeDurationMs;

        Vector2 ShakeOffset()
        {
            float k = ShakeFalloff;
            if (k <= 0f) return Vector2.zero;
            float t = shakeDurationMs - shakeMs;
            return new Vector2(shakeAmp * k * Mathf.Sin(t * 0.085f + shakePhase),
                               shakeAmp * 0.45f * k * Mathf.Sin(t * 0.131f + shakePhase * 2f));
        }

        public void Apply()
        {
            lastAspect = Cam.aspect;
            lastPan = pan;
            float halfWidth = geometry.HalfWidth;
            Cam.orthographicSize = halfWidth / Cam.aspect;
            // Safe-area insets (screen px → world units at this framing).
            float unitsPerPx = (halfWidth * 2f) / Screen.width;
            float safeTop = (Screen.height - (Screen.safeArea.y + Screen.safeArea.height)) * unitsPerPx;
            float safeBottom = Screen.safeArea.y * unitsPerPx;
            var shake = ShakeOffset();
            Cam.transform.position = new Vector3(shake.x,
                Mathf.Lerp(FramingAY(safeTop), FramingBY(safeTop, safeBottom), pan) + shake.y, -10f);
        }

        /// <summary>Camera y for the A framing: the top edge pinned above the tray + HUD.</summary>
        float FramingAY(float safeTop)
        {
            float top = geometry.CeilingY + geometry.HudHeight + safeTop;
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
