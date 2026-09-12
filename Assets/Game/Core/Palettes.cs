using System;
using System.Collections.Generic;

namespace RichCoast.Core
{
    /// <summary>Every player-facing surface colour that isn't a ball material (those are the tier-identity signal and never theme).</summary>
    public enum ThemeKey
    {
        /// <summary>Page + canvas background, and the Zone B band.</summary>
        Paper,
        /// <summary>Zone A band — slightly lighter, the "workbench top".</summary>
        PaperZoneA,
        /// <summary>Zone C band — slightly deeper, so the trap-door band reads as a slot.</summary>
        PaperZoneC,
        /// <summary>Light pine: walls, funnel, rails, gate signs.</summary>
        Pine,
        /// <summary>Darker pine: grain lines, structural edges.</summary>
        PineDark,
        /// <summary>Deepest wood tone: outlines, inner shadow lines, dividers.</summary>
        PineShadow,
        /// <summary>Brass accents: HUD rule, door hinges, dim markers, score-bar fill.</summary>
        Brass,
        /// <summary>Polished brass: the lit marker, highlights, button strokes.</summary>
        BrassBright,
        /// <summary>Primary text on panel surfaces.</summary>
        Ink,
        /// <summary>Muted labels.</summary>
        InkSoft,
        /// <summary>Panel fill (HUD band, queue row chrome).</summary>
        Cream,
        /// <summary>Death-line / warning red (must read against Paper).</summary>
        Danger,
        /// <summary>Game-over scrim.</summary>
        Scrim,
        /// <summary>Painted face of Zone B's high-multiplier gate signs.</summary>
        GatePaint,
        /// <summary>Zone B score-bar groove background.</summary>
        Groove,
    }

    /// <summary>Pure 0xRRGGBB colour math for the milestone palette cross-fade (port of <c>themeMath.ts</c>).</summary>
    public static class ColorMath
    {
        /// <summary>Blend two 0xRRGGBB colours per channel; t in [0,1], rounded to whole channels.</summary>
        public static uint Lerp(uint a, uint b, double t)
        {
            uint Mix(int shift)
            {
                double ca = (a >> shift) & 0xff;
                double cb = (b >> shift) & 0xff;
                return (uint)Math.Round(ca + (cb - ca) * t, MidpointRounding.AwayFromZero);
            }
            return (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
        }

        /// <summary>Relative luminance (Rec. 709 weights on raw channels) — enough to order brass tones.</summary>
        public static double Luminance(uint c) =>
            0.2126 * ((c >> 16) & 0xff) + 0.7152 * ((c >> 8) & 0xff) + 0.0722 * (c & 0xff);
    }

    /// <summary>One authored environment palette: a colour per <see cref="ThemeKey"/>, immutable.</summary>
    public sealed class Palette
    {
        public static readonly int KeyCount = Enum.GetValues(typeof(ThemeKey)).Length;

        readonly uint[] rgb;

        public Palette(uint[] rgbByKey)
        {
            if (rgbByKey == null || rgbByKey.Length != KeyCount)
                throw new ArgumentException($"a palette needs exactly {KeyCount} colours", nameof(rgbByKey));
            rgb = (uint[])rgbByKey.Clone();
        }

        public uint this[ThemeKey key] => rgb[(int)key];

        /// <summary>Blend every key of two palettes — one tick of the milestone cross-fade.</summary>
        public static Palette Lerp(Palette a, Palette b, double t)
        {
            var mixed = new uint[KeyCount];
            for (int i = 0; i < KeyCount; i++) mixed[i] = ColorMath.Lerp(a.rgb[i], b.rgb[i], t);
            return new Palette(mixed);
        }

        public bool SameAs(Palette other)
        {
            if (other == null) return false;
            for (int i = 0; i < KeyCount; i++) if (rgb[i] != other.rgb[i]) return false;
            return true;
        }
    }

    /// <summary>
    /// The authored milestone palettes (a 1:1 port of <c>Theme.ts</c>'s PALETTES). <see cref="Workshop"/>
    /// is the boot look; the progression stages name one of the others on each draw-window-shift
    /// milestone (author-then-hold past the last). Ordered as the run meets them:
    /// workshop → dusk → night → dawn → gilded. Authoring rule every palette holds to (unit-checked):
    /// BrassBright is brighter than Brass.
    /// </summary>
    public static class Palettes
    {
        // Column order == ThemeKey order: Paper, PaperZoneA, PaperZoneC, Pine, PineDark, PineShadow,
        // Brass, BrassBright, Ink, InkSoft, Cream, Danger, Scrim, GatePaint, Groove.

        /// <summary>The base "Bright Workshop": warm, light toy workshop.</summary>
        public static readonly Palette Workshop = new Palette(new uint[]
        {
            0xf2e7d5, 0xf7efe0, 0xe9dcc4, 0xd9b07c, 0xa87e4f, 0x7d5a33, 0xc9973f, 0xf0c060,
            0x3f3428, 0x8a7a64, 0xfdf6ea, 0xd64545, 0x2b2115, 0x6aa84f, 0xe0d2b8,
        });

        /// <summary>Sundown copper: the workshop at dusk.</summary>
        public static readonly Palette Dusk = new Palette(new uint[]
        {
            0xe0a970, 0xeabd85, 0xd2975a, 0xb26f43, 0x84492a, 0x5c2f1a, 0xb85c2e, 0xf28448,
            0x3c2113, 0x82573c, 0xf4d8ab, 0xcc2f2f, 0x2a150c, 0x718f3a, 0xd3a468,
        });

        /// <summary>Deep night blues: dark paper, light ink, moonlit-silver accents.</summary>
        public static readonly Palette Night = new Palette(new uint[]
        {
            0x243046, 0x2b3952, 0x1d2839, 0x3e5372, 0x2c3e59, 0x18243a, 0x8ea6c4, 0xcfe0f2,
            0xe8eef8, 0x9fb0c8, 0x35486a, 0xff6b5e, 0x060a12, 0x5fae7a, 0x1a2436,
        });

        /// <summary>Pale rose morning: soft pinks, rose-gold accents.</summary>
        public static readonly Palette Dawn = new Palette(new uint[]
        {
            0xf6e3e3, 0xfaeceb, 0xecd2d3, 0xd898a0, 0xac6a76, 0x7c4551, 0xcf7f6a, 0xf5ac92,
            0x4a2f38, 0x97707c, 0xfdf3f1, 0xc93a4d, 0x2a161c, 0x6fa668, 0xe8cfd0,
        });

        /// <summary>Rich gold on dark walnut: the endgame look.</summary>
        public static readonly Palette Gilded = new Palette(new uint[]
        {
            0x4a3421, 0x56402a, 0x3c2917, 0x8a6534, 0x6a4a22, 0x2e1e0d, 0xd4a437, 0xffd766,
            0xf7e9c8, 0xc9ab7a, 0x5f4527, 0xff5c4d, 0x120b04, 0x8fae4a, 0x33230f,
        });

        public const string WorkshopName = "workshop";

        static readonly Dictionary<string, Palette> byName = new Dictionary<string, Palette>(StringComparer.OrdinalIgnoreCase)
        {
            { WorkshopName, Workshop },
            { "dusk", Dusk },
            { "night", Night },
            { "dawn", Dawn },
            { "gilded", Gilded },
        };

        public static IReadOnlyDictionary<string, Palette> ByName => byName;

        /// <summary>Resolve an authored palette name; unknown names fall back to <see cref="Workshop"/> (never a stale look).</summary>
        public static Palette Get(string name) =>
            !string.IsNullOrEmpty(name) && byName.TryGetValue(name, out var p) ? p : Workshop;

        public static bool Exists(string name) => !string.IsNullOrEmpty(name) && byName.ContainsKey(name);
    }
}
