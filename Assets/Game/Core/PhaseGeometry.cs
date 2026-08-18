using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>Camera framing for one point along the A↔B pan.</summary>
    public readonly struct PhaseFraming
    {
        /// <summary>Main camera scroll (Zone C, Zone B and the backdrop ride this).</summary>
        public readonly float ScrollY;

        /// <summary>Arena camera viewport height; shrinking it top-crops Zone A.</summary>
        public readonly float ArenaViewportH;

        public PhaseFraming(float scrollY, float arenaViewportH)
        {
            ScrollY = scrollY;
            ArenaViewportH = arenaViewportH;
        }
    }

    /// <summary>
    /// Camera framing for the two gameplay phases — pure numbers, no scene dependency.
    ///
    /// The world is taller than the screen and the game "pans" between two framings instead of
    /// ever moving world objects. A single tween proxy <c>pan ∈ [0, PanDistance]</c> drives BOTH
    /// cameras through <see cref="FramingForPan"/>, so the arena-bottom / Zone-C seam stays
    /// pixel-locked mid-pan.
    ///
    /// Ported from the original <c>core/phaseGeometry.ts</c>, with the screen height now a
    /// parameter instead of a fixed 844 so the pan adapts to the device aspect ratio.
    /// </summary>
    public static class PhaseGeometry
    {
        /// <summary>Height of the HUD chrome bar; the arena viewport starts just below it.</summary>
        public const float HudHeight = 42f;

        /// <summary>Arena camera viewport height in the A-phase: the full Zone A board band (465).</summary>
        public static float ArenaViewHeightA => Layout.ZoneA.Height - HudHeight;

        /// <summary>
        /// How far the main camera scrolls between phases: Zone B's world overhang below the
        /// screen, so the B-phase brings Zone B's bottom edge exactly flush with the screen
        /// bottom. On the design screen this is 1238 − 844 = 394.
        /// </summary>
        public static float PanDistanceFor(float screenHeight)
        {
            var overhang = Mathf.Max(0f, Layout.WorldHeight - screenHeight);
            // Never pan so far that Zone A is cropped away entirely. On a portrait phone the
            // overhang (394) is comfortably short of that, but a wide editor game view would
            // otherwise leave the arena camera with a zero-height viewport — and a camera with no
            // viewport cannot answer the screen-to-world question aiming depends on.
            return Mathf.Min(overhang, ArenaViewHeightA - MinArenaViewportHeight);
        }

        /// <summary>Sliver of Zone A that stays on screen in the B phase, whatever the aspect.</summary>
        public const float MinArenaViewportHeight = 40f;

        /// <summary>Pan distance on the authored design screen (394) — the value the tests pin.</summary>
        public static float DesignPanDistance => PanDistanceFor(Layout.DesignScreenHeight);

        /// <summary>
        /// Arena camera viewport height in the B-phase: whatever the pan leaves of Zone A on
        /// screen (top-cropped). HUD + this = the B-phase Zone A sliver (42 + 71 on design).
        /// </summary>
        public static float ArenaViewHeightBFor(float screenHeight) =>
            ArenaViewHeightA - PanDistanceFor(screenHeight);

        /// <summary>
        /// Framing for an in-between pan value: 0 = A-phase, <paramref name="panDistance"/> =
        /// B-phase. The arena viewport shrinks by exactly the scroll, which is what keeps the
        /// seam locked.
        /// </summary>
        public static PhaseFraming FramingForPan(float pan, float panDistance)
        {
            var p = Mathf.Clamp(pan, 0f, panDistance);
            return new PhaseFraming(p, ArenaViewHeightA - p);
        }

        /// <summary>
        /// Arena-camera world-space centre y for a viewport height and arena scale <paramref name="s"/>
        /// (camera zoom is 1/s): the funnel floor stays pinned to the viewport's bottom edge, so
        /// the visible world spans <c>viewportH · s</c> ending at the Zone A/C boundary.
        /// </summary>
        public static float ArenaCenterY(float viewportH, float s) =>
            Layout.ZoneA.Bottom - viewportH * 0.5f * s;
    }
}
