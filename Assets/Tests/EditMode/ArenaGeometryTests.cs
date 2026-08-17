using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests
{
    /// <summary>
    /// The arena's growth rules. These encode the promise the milestone zoom makes: the floor
    /// never moves, the arena never reaches into Zone B, and the physics feel is scale-invariant.
    /// </summary>
    public class ArenaGeometryTests
    {
        [Test]
        public void TheBaseArenaIsExactlyTheDesignBoard()
        {
            var arena = new ArenaGeometry(1f);
            Assert.That(arena.Width, Is.EqualTo(Layout.Width));
            Assert.That(arena.Height, Is.EqualTo(PhaseGeometry.ArenaViewHeightA));
            Assert.That(arena.SpawnY, Is.EqualTo(arena.CeilingY + Tuning.SpawnY));
            Assert.That(arena.DeathLineY, Is.EqualTo(arena.CeilingY + Tuning.DeathLineY));
        }

        [Test]
        public void TheFloorIsTheAnchorAndNeverMovesWithScale()
        {
            foreach (var scale in new[] { 1f, 1.9f, 4f, 12f })
            {
                Assert.That(new ArenaGeometry(scale).FloorY, Is.EqualTo(Layout.ZoneA.Bottom),
                    $"floor moved at scale {scale}");
            }
        }

        [Test]
        public void GrowingTheArenaNeverReachesIntoZoneB()
        {
            // It grows upward and outward only: the interior always ends at the Zone A/C boundary.
            foreach (var scale in new[] { 1f, 2f, 8f })
            {
                var arena = new ArenaGeometry(scale);
                Assert.That(arena.CeilingY, Is.LessThan(arena.FloorY));
                Assert.That(arena.FloorY, Is.EqualTo(Layout.ZoneC.Y));
            }
        }

        [Test]
        public void EveryAuthoredDistanceScalesWithTheArenaSoTheFeelIsUnchanged()
        {
            var baseArena = new ArenaGeometry(1f);
            var grown = new ArenaGeometry(3f);

            Assert.That(grown.Gravity, Is.EqualTo(baseArena.Gravity * 3f));
            Assert.That(grown.RestSpeed, Is.EqualTo(baseArena.RestSpeed * 3f));
            Assert.That(grown.MaxSpeed, Is.EqualTo(baseArena.MaxSpeed * 3f));
            Assert.That(grown.BlastRadius, Is.EqualTo(baseArena.BlastRadius * 3f));
            Assert.That(grown.WarnBand, Is.EqualTo(baseArena.WarnBand * 3f));
            Assert.That(grown.ScaledWallThickness, Is.EqualTo(baseArena.ScaledWallThickness * 3f));
        }

        [Test]
        public void TheAntiTunnelInvariantSurvivesEveryScale()
        {
            // A ball may never travel further in one step than a wall is thick. Both sides scale,
            // so checking the ratio once covers every milestone.
            foreach (var scale in new[] { 1f, 2.5f, 10f })
            {
                var arena = new ArenaGeometry(scale);
                var perStep = arena.MaxSpeed / Tuning.StepsPerSecond;
                Assert.That(perStep, Is.LessThan(arena.ScaledWallThickness), $"tunnel risk at scale {scale}");
            }
        }

        [Test]
        public void SpawnClampKeepsABallInsideTheGrownArena()
        {
            var arena = new ArenaGeometry(2f);
            const float radius = 34f;

            Assert.That(arena.ClampSpawnX(-9999f, radius), Is.EqualTo(arena.MinX + radius));
            Assert.That(arena.ClampSpawnX(9999f, radius), Is.EqualTo(arena.MaxX - radius));
            Assert.That(arena.ClampSpawnX(arena.CenterX, radius), Is.EqualTo(arena.CenterX));
        }

        [Test]
        public void DesignSpaceRoundTripsThroughWorldSpace()
        {
            var design = new UnityEngine.Vector2(123f, 456f);
            var world = DesignSpace.ToWorld(design);
            Assert.That(world.y, Is.EqualTo(-456f), "design space is y-down, world space is y-up");
            Assert.That(DesignSpace.ToDesign(world), Is.EqualTo(design));
        }
    }
}
