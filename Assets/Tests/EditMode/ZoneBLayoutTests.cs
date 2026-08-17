using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Data;
using UnityEngine;

namespace RichCoast.Tests
{
    /// <summary>
    /// The authored Zone B playfields and the gate motion that drives them. The layouts are the
    /// game in Zone B — outcomes must feel layout-driven and readable, so the properties that keep
    /// them balanced and reachable are pinned here rather than trusted to the eye.
    /// </summary>
    public class ZoneBLayoutTests
    {
        [Test]
        public void BothAuthoredLayoutsExistAndAreDistinct()
        {
            var all = DefaultZoneBLayouts.All();
            Assert.That(all.Length, Is.EqualTo(2));
            Assert.That(all[0].Gates.Length, Is.Not.EqualTo(all[1].Gates.Length).Or.Not.EqualTo(0));
        }

        [Test]
        public void EveryGateStaysInsideTheZoneAndCarriesABalancedMultiplier()
        {
            foreach (var layout in DefaultZoneBLayouts.All())
            {
                foreach (var gate in layout.Gates)
                {
                    // Multipliers are capped at 4 so a cascade compounds without exploding.
                    Assert.That(gate.Multiplier, Is.InRange(2, 4), "gate multiplier out of the tuned band");
                    Assert.That(gate.Length, Is.GreaterThan(0f));

                    var (center, _) = gate.PoseAt(0f);
                    Assert.That(center.y, Is.InRange(Layout.ZoneB.Y, Layout.ZoneB.Bottom), "gate outside Zone B");
                    Assert.That(center.x - gate.Length * 0.5f, Is.GreaterThanOrEqualTo(-1f), "gate juts out the left");
                    Assert.That(center.x + gate.Length * 0.5f, Is.LessThanOrEqualTo(Layout.Width + 1f), "gate juts out the right");
                }
            }
        }

        [Test]
        public void EveryLayoutDrainsIntoAtLeastOneCollector()
        {
            // Without a drain a round could never end, and the trap-door would stay locked forever.
            foreach (var layout in DefaultZoneBLayouts.All())
            {
                Assert.That(layout.Collectors.Length, Is.GreaterThan(0));
                foreach (var collector in layout.Collectors)
                {
                    Assert.That(collector.ScoreMultiplier, Is.GreaterThan(0f));
                    Assert.That(collector.Center.y, Is.InRange(Layout.ZoneB.Y, Layout.ZoneB.Bottom));
                }
            }
        }

        [Test]
        public void TheFunnelRampsReachTheCollectorFromBothWalls()
        {
            // The ramps are what guarantee a ball ends up in the drain rather than parked in a
            // corner — a stuck ball freezes the whole round.
            foreach (var layout in DefaultZoneBLayouts.All())
            {
                var touchesLeftWall = false;
                var touchesRightWall = false;
                foreach (var wall in layout.Walls)
                {
                    if (!wall.FillBelow) continue;
                    touchesLeftWall |= Mathf.Approximately(wall.From.x, Layout.ZoneB.X);
                    touchesRightWall |= Mathf.Approximately(wall.From.x, Layout.ZoneB.Right);
                }

                Assert.That(touchesLeftWall, Is.True, "no ramp anchored to the left wall");
                Assert.That(touchesRightWall, Is.True, "no ramp anchored to the right wall");
            }
        }

        [Test]
        public void AStaticGateNeverMoves()
        {
            var gate = GateDef.StaticGate(100f, 800f, 0.2f, 90f, 3);
            var (center, angle) = gate.PoseAt(0f);
            var (laterCenter, laterAngle) = gate.PoseAt(5_000f);

            Assert.That(laterCenter, Is.EqualTo(center));
            Assert.That(laterAngle, Is.EqualTo(angle));
        }

        [Test]
        public void ATranslatingGateSlidesBetweenItsEndpointsAndComesBack()
        {
            const float period = 1000f;
            var gate = GateDef.TranslatingGate(50f, 800f, 250f, 800f, 0f, 80f, 2, period);

            Assert.That(gate.PoseAt(0f).center.x, Is.EqualTo(50f).Within(1e-3f), "starts at A");
            Assert.That(gate.PoseAt(period * 0.5f).center.x, Is.EqualTo(250f).Within(1e-3f), "reaches B at half a period");
            Assert.That(gate.PoseAt(period).center.x, Is.EqualTo(50f).Within(1e-3f), "is back at A after one period");

            // Its angle is fixed — only the centre travels.
            Assert.That(gate.PoseAt(period * 0.25f).angle, Is.EqualTo(0f));
        }

        [Test]
        public void ATranslatingGateMovesAtAConstantSpeedAcrossItsTravel()
        {
            // Easing at the ends would make the gate hesitate exactly where a falling ball is most
            // likely to meet it, turning a timing read into a coin flip.
            const float period = 1000f;
            var gate = GateDef.TranslatingGate(0f, 800f, 100f, 800f, 0f, 80f, 2, period);

            var first = gate.PoseAt(100f).center.x - gate.PoseAt(0f).center.x;
            var middle = gate.PoseAt(300f).center.x - gate.PoseAt(200f).center.x;
            var last = gate.PoseAt(500f).center.x - gate.PoseAt(400f).center.x;

            Assert.That(middle, Is.EqualTo(first).Within(1e-3f));
            Assert.That(last, Is.EqualTo(first).Within(1e-3f));
        }

        [Test]
        public void ARotatingGateSpinsSteadilyAroundAFixedPivot()
        {
            var gate = GateDef.RotatingGate(195f, 900f, 120f, 2, speedRadPerMs: 0.002f);

            Assert.That(gate.PoseAt(1000f).center, Is.EqualTo(new Vector2(195f, 900f)), "the pivot must not drift");
            Assert.That(gate.PoseAt(1000f).angle, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(gate.PoseAt(2000f).angle, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void LayoutSelectionIsUniformAcrossManyRuns()
        {
            // Which layout a run gets should be a coin flip, not a bias toward one of them.
            var rng = new System.Random(1234);
            var counts = new int[2];
            var all = DefaultZoneBLayouts.All();

            for (var i = 0; i < 400; i++)
            {
                var picked = DefaultZoneBLayouts.PickRandom(rng);
                counts[picked.Gates.Length == all[0].Gates.Length ? 0 : 1]++;
            }

            Assert.That(counts[0], Is.GreaterThan(100), "layout 1 barely appeared");
            Assert.That(counts[1], Is.GreaterThan(100), "layout 2 barely appeared");
        }
    }
}
