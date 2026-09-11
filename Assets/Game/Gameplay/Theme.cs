using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The environment palette (the Phaser "Bright Workshop" base look, ported as a static table).
    /// Ball materials are NOT here — they're the tier-identity signal and never theme. Milestone
    /// palette cross-fades are M3; M1 keeps the single workshop mood.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Paper = BallArt.Rgb(0xf2e7d5);
        public static readonly Color PaperZoneA = BallArt.Rgb(0xf7efe0);
        public static readonly Color Pine = BallArt.Rgb(0xd9b07c);
        public static readonly Color PineDark = BallArt.Rgb(0xa87e4f);
        public static readonly Color PineShadow = BallArt.Rgb(0x7d5a33);
        public static readonly Color Brass = BallArt.Rgb(0xc9973f);
        public static readonly Color BrassBright = BallArt.Rgb(0xf0c060);
        public static readonly Color Ink = BallArt.Rgb(0x3f3428);
        public static readonly Color InkSoft = BallArt.Rgb(0x8a7a64);
        public static readonly Color Cream = BallArt.Rgb(0xfdf6ea);
        public static readonly Color Danger = BallArt.Rgb(0xd64545);
        public static readonly Color Scrim = BallArt.Rgb(0x2b2115);
    }
}
