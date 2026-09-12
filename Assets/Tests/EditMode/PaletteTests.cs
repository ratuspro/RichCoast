using System;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    /// <summary>Mirrors master's <c>themeMath.test.ts</c>: the cross-fade colour math and the palette authoring rules.</summary>
    public class PaletteTests
    {
        [Test]
        public void LerpReturnsTheEndpoints()
        {
            Assert.AreEqual(0x123456u, ColorMath.Lerp(0x123456, 0xfedcba, 0));
            Assert.AreEqual(0xfedcbau, ColorMath.Lerp(0x123456, 0xfedcba, 1));
        }

        [Test]
        public void LerpMixesEachChannelIndependentlyAtTheMidpoint()
        {
            // 0x00→0xff = 0x80 (rounded), 0xff→0x00 = 0x80, 0x40→0xc0 = 0x80
            Assert.AreEqual(0x808080u, ColorMath.Lerp(0x00ff40, 0xff00c0, 0.5));
        }

        [Test]
        public void LerpDoesNotBleedBetweenChannels()
        {
            // Only the blue channel differs; red/green must be untouched at any t.
            Assert.AreEqual(0xa1b240u, ColorMath.Lerp(0xa1b200, 0xa1b2ff, 0.25));
        }

        [Test]
        public void PaletteLerpBlendsEveryKey()
        {
            var mid = Palette.Lerp(Palettes.Workshop, Palettes.Night, 0.5);
            foreach (ThemeKey key in Enum.GetValues(typeof(ThemeKey)))
                Assert.AreEqual(ColorMath.Lerp(Palettes.Workshop[key], Palettes.Night[key], 0.5), mid[key], key.ToString());
        }

        [Test]
        public void PaletteLerpReproducesTheEndpoints()
        {
            Assert.IsTrue(Palette.Lerp(Palettes.Workshop, Palettes.Dusk, 0).SameAs(Palettes.Workshop));
            Assert.IsTrue(Palette.Lerp(Palettes.Workshop, Palettes.Dusk, 1).SameAs(Palettes.Dusk));
        }

        [Test]
        public void EveryPaletteKeepsBrassBrightBrighterThanBrass()
        {
            foreach (var pair in Palettes.ByName)
                Assert.Greater(ColorMath.Luminance(pair.Value[ThemeKey.BrassBright]), ColorMath.Luminance(pair.Value[ThemeKey.Brass]), $"palette \"{pair.Key}\"");
        }

        [Test]
        public void EveryPaletteAuthoredByTheProgressionCurveExists()
        {
            foreach (var stage in ProgressionCurve.Default.Stages)
            {
                if (stage.Palette == null) continue;
                Assert.IsTrue(Palettes.Exists(stage.Palette), $"stage @{stage.FromLevel} names unknown palette \"{stage.Palette}\"");
            }
            Assert.AreSame(Palettes.Workshop, Palettes.Get(ProgressionCurve.Default.PaletteNameForLevel(1)));
            Assert.AreSame(Palettes.Dusk, Palettes.Get(ProgressionCurve.Default.PaletteNameForLevel(20)));
            Assert.AreSame(Palettes.Gilded, Palettes.Get(ProgressionCurve.Default.PaletteNameForLevel(500)));
        }

        [Test]
        public void UnknownPaletteNamesFallBackToWorkshop()
        {
            Assert.AreSame(Palettes.Workshop, Palettes.Get(null));
            Assert.AreSame(Palettes.Workshop, Palettes.Get("nonesuch"));
            Assert.AreSame(Palettes.Dusk, Palettes.Get("DUSK"));
        }
    }
}
