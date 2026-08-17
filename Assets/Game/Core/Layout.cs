using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>Axis-aligned rectangle in design space (y grows downward, as in the original design).</summary>
    public readonly struct ZoneRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public ZoneRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public float CenterX => X + Width * 0.5f;
    }

    /// <summary>
    /// Single source of truth for world + zone geometry. Plain numbers, no scene dependency, so
    /// every zone and the tests read it without booting anything.
    ///
    /// Design space is 390 wide; the WORLD is 1238 tall — deliberately taller than the ~844 the
    /// screen shows — and the game alternates between two camera framings rather than ever
    /// moving world objects (see <see cref="PhaseGeometry"/>):
    ///
    ///  - A-phase: the full Zone A band is on screen (~60% of the screen: 42 HUD + 465 board);
    ///    Zone B is bottom-cropped.
    ///  - B-phase: Zone A shows only a 113 sliver (42 HUD + 71 board, top-cropped) and Zone B's
    ///    full 687 band exactly fills the rest of the screen.
    ///
    /// Zone A's band is round(844 × 2/3 × 0.9) — the original 2/3 split, shrunk 10% in Zone B's
    /// favour.
    ///
    /// Device fitting: width is locked to <see cref="Width"/> and the visible height follows the
    /// device aspect ratio (<see cref="ScreenHeightForAspect"/>), so taller or shorter phones get
    /// more or less of the world rather than being letterboxed. The original build assumed a
    /// fixed 390×844 screen.
    /// </summary>
    public static class Layout
    {
        /// <summary>Design width — locked portrait. World units are design pixels.</summary>
        public const float Width = 390f;

        /// <summary>The screen height the design was authored against (390×844, a 2019-era phone).</summary>
        public const float DesignScreenHeight = 844f;

        private const float ZoneAHeight = 42f + 465f;
        private const float ZoneCHeight = 44f;
        private const float ZoneBHeight = 687f;

        public static readonly ZoneRect ZoneA = new ZoneRect(0f, 0f, Width, ZoneAHeight);
        public static readonly ZoneRect ZoneC = new ZoneRect(0f, ZoneA.Bottom, Width, ZoneCHeight);
        public static readonly ZoneRect ZoneB = new ZoneRect(0f, ZoneC.Bottom, Width, ZoneBHeight);

        /// <summary>Total world height: the three bands stacked (1238).</summary>
        public static float WorldHeight => ZoneB.Bottom;

        /// <summary>
        /// Visible design-space height for a device aspect ratio, given the locked design width.
        /// A 390×844 phone yields exactly <see cref="DesignScreenHeight"/>; a taller phone shows
        /// more world, a shorter one less.
        /// </summary>
        public static float ScreenHeightForAspect(float widthOverHeight)
        {
            if (widthOverHeight <= 0f) return DesignScreenHeight;
            return Width / widthOverHeight;
        }

        /// <summary>Convenience for runtime code: the visible height on the current screen.</summary>
        public static float CurrentScreenHeight()
        {
            var w = Mathf.Max(1, Screen.width);
            var h = Mathf.Max(1, Screen.height);
            return ScreenHeightForAspect((float)w / h);
        }
    }
}
