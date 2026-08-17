using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Gameplay
{
    /// <summary>
    /// The two cameras the game is framed with, and the single pan value that drives both.
    ///
    /// The world is taller than the screen and nothing in it ever moves for the camera's sake.
    /// Instead:
    ///
    ///  - The MAIN camera shows the world at 1:1 and scrolls between the two phases. Zone B and
    ///    Zone C ride it.
    ///  - The ARENA camera draws Zone A into a viewport below the HUD. Its zoom is 1/arenaScale,
    ///    so when a milestone grows the arena the balls keep their apparent size and the board
    ///    simply gains room. Its viewport shrinks as the pan proceeds, top-cropping Zone A to a
    ///    sliver in the B phase.
    ///
    /// Both are derived from <see cref="PhaseGeometry"/>, so the arena's bottom edge stays pinned
    /// to the Zone A/C seam at every point of the pan — the seam is the thing a player would
    /// notice sliding.
    /// </summary>
    public sealed class CameraRig
    {
        private readonly Camera _main;
        private readonly Camera _arena;
        private readonly float _screenHeight;
        private readonly float _panDistance;

        private float _pan;
        private float _arenaScale = 1f;

        public CameraRig(Camera main, float screenHeight)
        {
            _main = main;
            _screenHeight = screenHeight;
            _panDistance = PhaseGeometry.PanDistanceFor(screenHeight);

            _main.orthographic = true;
            _main.orthographicSize = screenHeight * 0.5f;
            // Zone A is drawn by the arena camera alone; letting the main camera draw it too would
            // show the board twice, at two different scales, once the arena grows.
            _main.cullingMask &= ~(1 << PhysicsLayers.ZoneA);

            var go = new GameObject("Arena Camera");
            go.transform.SetParent(main.transform.parent, worldPositionStays: false);
            _arena = go.AddComponent<Camera>();
            _arena.orthographic = true;
            _arena.clearFlags = CameraClearFlags.SolidColor;
            _arena.backgroundColor = main.backgroundColor;
            _arena.cullingMask = 1 << PhysicsLayers.ZoneA;
            _arena.allowHDR = false;
            _arena.allowMSAA = false;
            // Drawn after the main camera so its viewport sits on top of the scrolling world.
            _arena.depth = main.depth + 1;

            Apply();
        }

        public Camera Main => _main;
        public Camera Arena => _arena;
        public float PanDistance => _panDistance;

        /// <summary>Pan position, 0 = the A phase framing, <see cref="PanDistance"/> = the B phase.</summary>
        public float Pan
        {
            get => _pan;
            set
            {
                _pan = Mathf.Clamp(value, 0f, _panDistance);
                Apply();
            }
        }

        /// <summary>Arena scale from the milestone growth. The arena camera zooms out to match.</summary>
        public float ArenaScale
        {
            get => _arenaScale;
            set
            {
                _arenaScale = Mathf.Max(0.01f, value);
                Apply();
            }
        }

        private void Apply()
        {
            var framing = PhaseGeometry.FramingForPan(_pan, _panDistance);

            // Main camera: the whole screen, scrolled by the pan.
            var mainCenterY = _screenHeight * 0.5f + framing.ScrollY;
            _main.transform.position = new Vector3(Layout.Width * 0.5f, DesignSpace.ToWorldY(mainCenterY), -10f);

            // Arena viewport: the band between the HUD and wherever the pan has cropped it to.
            var viewportH = Mathf.Max(0f, framing.ArenaViewportH);
            var top = PhaseGeometry.HudHeight;
            _arena.rect = new Rect(
                0f,
                Mathf.Clamp01(1f - (top + viewportH) / _screenHeight),
                1f,
                Mathf.Clamp01(viewportH / _screenHeight));

            // Zoom is 1/scale: the arena grows in world units while its image stays the same size.
            _arena.orthographicSize = viewportH * 0.5f * _arenaScale;
            var centerY = PhaseGeometry.ArenaCenterY(viewportH, _arenaScale);
            _arena.transform.position = new Vector3(Layout.Width * 0.5f, DesignSpace.ToWorldY(centerY), -10f);
            _arena.enabled = viewportH > 1f;
        }
    }
}
