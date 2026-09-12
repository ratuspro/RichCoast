using System;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class ZoneBLayoutTests
    {
        [Test]
        public void TwoLayoutsAndPickCoversBoth()
        {
            Assert.AreEqual(2, ZoneBLayouts.All.Length);
            Assert.AreEqual("LAYOUT_1", ZoneBLayouts.Pick(0).Name);
            Assert.AreEqual("LAYOUT_1", ZoneBLayouts.Pick(0.49).Name);
            Assert.AreEqual("LAYOUT_2", ZoneBLayouts.Pick(0.5).Name);
            Assert.AreEqual("LAYOUT_2", ZoneBLayouts.Pick(0.999).Name);
            Assert.AreEqual("LAYOUT_2", ZoneBLayouts.Pick(1.0).Name, "an inclusive 1.0 must not index out of range");
        }

        [Test]
        public void EveryElementLiesInsideTheBand()
        {
            foreach (var layout in ZoneBLayouts.All)
            {
                foreach (var g in layout.Gates)
                {
                    double half = g.Length / 2;
                    Assert.That(g.Cx - half, Is.GreaterThanOrEqualTo(0), $"{layout.Name} gate left edge");
                    Assert.That(g.Cx + half, Is.LessThanOrEqualTo(DesignSpace.Width), $"{layout.Name} gate right edge");
                    Assert.That(g.Cy, Is.InRange(0, DesignSpace.ZoneBHeight), $"{layout.Name} gate y");
                }
                foreach (var w in layout.Walls)
                {
                    Assert.That(w.X1, Is.InRange(0, DesignSpace.Width));
                    Assert.That(w.X2, Is.InRange(0, DesignSpace.Width));
                    Assert.That(w.Y1, Is.InRange(0, DesignSpace.ZoneBHeight));
                    Assert.That(w.Y2, Is.InRange(0, DesignSpace.ZoneBHeight));
                    Assert.That(Math.Abs(w.X2 - w.X1) + Math.Abs(w.Y2 - w.Y1), Is.GreaterThan(0), "a wall must have length");
                }
                foreach (var c in layout.Collectors)
                {
                    Assert.That(c.X, Is.GreaterThanOrEqualTo(0));
                    Assert.That(c.X + c.Width, Is.LessThanOrEqualTo(DesignSpace.Width));
                    Assert.That(c.Y + c.Height, Is.LessThanOrEqualTo(DesignSpace.ZoneBHeight + 1e-9));
                }
            }
        }

        [Test]
        public void MultipliersAreBalancedAndEveryLayoutHasACollectorAndFunnel()
        {
            foreach (var layout in ZoneBLayouts.All)
            {
                Assert.That(layout.Gates.Length, Is.GreaterThanOrEqualTo(6));
                foreach (var g in layout.Gates) Assert.That(g.Multiplier, Is.InRange(2, 4), $"{layout.Name} gate x{g.Multiplier}");
                Assert.AreEqual(1, layout.Collectors.Length);
                int ramps = 0;
                foreach (var w in layout.Walls) if (w.FillBelow) ramps++;
                Assert.AreEqual(2, ramps, "two funnel ramps feed the collector");
                // The collector's bottom is flush with the band bottom (it sits on the score bar).
                var c = layout.Collectors[0];
                Assert.AreEqual(DesignSpace.ZoneBHeight, c.Y + c.Height, 1e-9);
            }
        }

        [Test]
        public void GatePosesFollowTheirKind()
        {
            var s = GateDef.Static(10, 20, 0.3, 50, 2);
            Assert.AreEqual((10.0, 20.0, 0.3), s.PoseAt(12345));

            var t = GateDef.Translating(0, 0, 100, 0, 0, 40, 2, periodMs: 1000);
            Assert.AreEqual(50, t.PoseAt(0).x, 1e-9);     // sin(0) → midpoint
            Assert.AreEqual(100, t.PoseAt(250).x, 1e-9);  // sin(pi/2) → B end
            Assert.AreEqual(0, t.PoseAt(750).x, 1e-9);    // sin(3pi/2) → A end

            var r = GateDef.Rotating(5, 5, 30, 3, speedRadPerMs: 0.001);
            Assert.AreEqual(1.0, r.PoseAt(1000).angle, 1e-9);
            Assert.AreEqual(5.0, r.PoseAt(1000).x);
        }
    }
}
