using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>
    /// Layout and the two-phase camera framing, ported from <c>core/Layout.ts</c> and
    /// <c>core/phaseGeometry.ts</c>. The design-space numbers are pinned because the whole Zone A
    /// tuning table (spawn row, death line, radii) is authored against them.
    /// </summary>
    public class LayoutTests
    {
        [Test]
        public void ZonesTileTheWorldWithoutGapsOrOverlap()
        {
            Assert.That(Layout.ZoneA.Y, Is.EqualTo(0f));
            Assert.That(Layout.ZoneC.Y, Is.EqualTo(Layout.ZoneA.Bottom));
            Assert.That(Layout.ZoneB.Y, Is.EqualTo(Layout.ZoneC.Bottom));
            Assert.That(Layout.WorldHeight, Is.EqualTo(1238f));
        }

        [Test]
        public void TheWorldIsTallerThanTheDesignScreenWhichIsWhatThePanExists() =>
            Assert.That(Layout.WorldHeight, Is.GreaterThan(Layout.DesignScreenHeight));

        [Test]
        public void ZoneAKeepsTheAuthoredSplit()
        {
            // 42 HUD + 465 board = round(844 × 2/3 × 0.9): the original 2/3 split, shrunk 10% in
            // Zone B's favour.
            Assert.That(Layout.ZoneA.Height, Is.EqualTo(507f));
            Assert.That(PhaseGeometry.ArenaViewHeightA, Is.EqualTo(465f));
        }

        [Test]
        public void ScreenHeightFollowsTheDeviceAspectRatio()
        {
            // The design phone reproduces the authored height exactly...
            Assert.That(Layout.ScreenHeightForAspect(390f / 844f), Is.EqualTo(844f).Within(1e-3f));
            // ...and a taller phone simply shows more world rather than letterboxing.
            Assert.That(Layout.ScreenHeightForAspect(1080f / 2340f), Is.GreaterThan(844f));
        }

        [Test]
        public void PanDistanceIsZoneBsOverhangBelowTheScreen()
        {
            Assert.That(PhaseGeometry.DesignPanDistance, Is.EqualTo(394f));
            Assert.That(PhaseGeometry.ArenaViewHeightBFor(Layout.DesignScreenHeight), Is.EqualTo(71f));
        }

        [Test]
        public void TheArenaViewportShrinksByExactlyTheScrollSoTheSeamStaysLocked()
        {
            var pan = PhaseGeometry.DesignPanDistance;
            for (var p = 0f; p <= pan; p += pan / 8f)
            {
                var framing = PhaseGeometry.FramingForPan(p, pan);
                Assert.That(framing.ScrollY + framing.ArenaViewportH, Is.EqualTo(PhaseGeometry.ArenaViewHeightA).Within(1e-3f),
                    $"seam drifted at pan {p}");
            }
        }

        [Test]
        public void FramingClampsOutOfRangePanValues()
        {
            var pan = PhaseGeometry.DesignPanDistance;
            Assert.That(PhaseGeometry.FramingForPan(-50f, pan).ScrollY, Is.EqualTo(0f));
            Assert.That(PhaseGeometry.FramingForPan(pan + 50f, pan).ScrollY, Is.EqualTo(pan));
        }

        [Test]
        public void ArenaCenterKeepsTheFunnelFloorPinnedToTheViewportBottom()
        {
            // The visible world spans viewportH·s and ends at the Zone A/C boundary, so the floor
            // never drifts when a milestone zoom changes the arena scale.
            const float viewportH = 465f;
            foreach (var scale in new[] { 1f, 1.5f, 3f })
            {
                var center = PhaseGeometry.ArenaCenterY(viewportH, scale);
                Assert.That(center + viewportH * 0.5f * scale, Is.EqualTo(Layout.ZoneA.Bottom).Within(1e-3f));
            }
        }
    }
}
