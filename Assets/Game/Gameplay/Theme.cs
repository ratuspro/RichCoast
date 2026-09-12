using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The ACTIVE environment palette (port of the Phaser <c>Theme</c>): every player-facing surface
    /// colour that isn't a ball material (those are the tier-identity signal and never theme).
    /// Mutable — <see cref="ThemeDirector"/> re-writes it through <see cref="Apply"/> as the run
    /// crosses milestones, cross-fading between the authored <see cref="Palettes"/>. Consumers read
    /// <c>Theme.Brass</c> etc. at use time; anything that BAKES a colour into a renderer binds it with
    /// <see cref="Themed"/> so <c>GameEvents.ThemeChanged</c> restyles it in step with the fade.
    /// </summary>
    public static class Theme
    {
        static readonly Color[] colors = new Color[Palette.KeyCount];
        static Palette active;

        static Theme() => Apply(Palettes.Workshop);

        /// <summary>The palette currently applied (compare with <c>Palette.SameAs</c>).</summary>
        public static Palette Active => active;

        /// <summary>Re-point the active palette; every <see cref="Theme"/> property reads the new values at once.</summary>
        public static void Apply(Palette palette)
        {
            active = palette;
            for (int i = 0; i < Palette.KeyCount; i++) colors[i] = BallArt.Rgb(palette[(ThemeKey)i]);
        }

        public static Color Get(ThemeKey key) => colors[(int)key];

        public static Color Paper => colors[(int)ThemeKey.Paper];
        public static Color PaperZoneA => colors[(int)ThemeKey.PaperZoneA];
        public static Color PaperZoneC => colors[(int)ThemeKey.PaperZoneC];
        public static Color Pine => colors[(int)ThemeKey.Pine];
        public static Color PineDark => colors[(int)ThemeKey.PineDark];
        public static Color PineShadow => colors[(int)ThemeKey.PineShadow];
        public static Color Brass => colors[(int)ThemeKey.Brass];
        public static Color BrassBright => colors[(int)ThemeKey.BrassBright];
        public static Color Ink => colors[(int)ThemeKey.Ink];
        public static Color InkSoft => colors[(int)ThemeKey.InkSoft];
        public static Color Cream => colors[(int)ThemeKey.Cream];
        public static Color Danger => colors[(int)ThemeKey.Danger];
        public static Color Scrim => colors[(int)ThemeKey.Scrim];
        /// <summary>High-multiplier gate sign paint (low multipliers are brass).</summary>
        public static Color GatePaint => colors[(int)ThemeKey.GatePaint];
        /// <summary>Zone B score-bar groove background.</summary>
        public static Color Groove => colors[(int)ThemeKey.Groove];
    }
}
