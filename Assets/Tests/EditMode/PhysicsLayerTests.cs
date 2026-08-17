using NUnit.Framework;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Tests
{
    /// <summary>
    /// The layer numbers the game hard-codes must still match the names in the project's tag
    /// manager. Unity has no runtime API for naming layers, so the two are set in different places
    /// and would otherwise drift silently — and a silent drift here means Zone A's grown walls
    /// start eating Zone B's balls, but only at late milestones.
    /// </summary>
    public class PhysicsLayerTests
    {
        [Test]
        public void ZoneLayersAreNamedAsTheGameExpects()
        {
            Assert.That(LayerMask.LayerToName(PhysicsLayers.ZoneA), Is.EqualTo(PhysicsLayers.ZoneAName),
                "run Rich Coast/Apply Physics Layers");
            Assert.That(LayerMask.LayerToName(PhysicsLayers.ZoneB), Is.EqualTo(PhysicsLayers.ZoneBName),
                "run Rich Coast/Apply Physics Layers");
        }

        [Test]
        public void TheZoneLayersAreDistinct() =>
            Assert.That(PhysicsLayers.ZoneA, Is.Not.EqualTo(PhysicsLayers.ZoneB));
    }
}
