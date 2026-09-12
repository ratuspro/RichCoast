using System.Collections.Generic;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    public class DoorMathTests
    {
        [Test]
        public void NearestByEdgeDistanceNotCentre()
        {
            // A big ball further away by centre but whose rim reaches closer wins.
            var balls = new List<DoorCandidate>
            {
                new DoorCandidate(0, 2.0, 0.4),  // edge distance 1.6
                new DoorCandidate(0, 3.0, 2.0),  // edge distance 1.0 ← wins
            };
            Assert.AreEqual(1, DoorMath.NearestDoorBall(balls, 0, 0));
        }

        [Test]
        public void IgnoresBallsBelowTheDoorAndReturnsMinusOneWhenEmpty()
        {
            var balls = new List<DoorCandidate> { new DoorCandidate(0, -1, 0.5) };
            Assert.AreEqual(-1, DoorMath.NearestDoorBall(balls, 0, 0));
            Assert.AreEqual(-1, DoorMath.NearestDoorBall(new List<DoorCandidate>(), 0, 0));
        }

        [Test]
        public void HorizontalOffsetCounts()
        {
            var balls = new List<DoorCandidate>
            {
                new DoorCandidate(-4, 1, 0.5),
                new DoorCandidate(0.5, 1.5, 0.5),
            };
            Assert.AreEqual(1, DoorMath.NearestDoorBall(balls, 0, 0));
        }

        [Test]
        public void SweepPingPongsAcrossNinePositions()
        {
            const double step = 110;
            var seen = new List<int>();
            for (int k = 0; k < 16; k++) seen.Add(DoorMath.SweepIndex(k * step, step, 9));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 6, 5, 4, 3, 2, 1 }, seen);
            Assert.AreEqual(0, DoorMath.SweepIndex(16 * step, step, 9), "the cycle repeats from the left edge");
            Assert.AreEqual(0, DoorMath.SweepIndex(500, 110, 1));
        }

        [Test]
        public void SweepPositionsAreEvenlySpacedInsideTheMargins()
        {
            double min = DesignSpace.SweepMargin, max = DesignSpace.Width - DesignSpace.SweepMargin;
            Assert.AreEqual(min, DoorMath.SweepPositionX(0, 9, min, max), 1e-9);
            Assert.AreEqual(max, DoorMath.SweepPositionX(8, 9, min, max), 1e-9);
            Assert.AreEqual(DesignSpace.Width / 2, DoorMath.SweepPositionX(4, 9, min, max), 1e-9);
        }

        [Test]
        public void SplitFanIsSymmetricAndALoneCopyGoesStraight()
        {
            Assert.AreEqual(0, DoorMath.SplitFanAngle(0, 1, 0.8));
            Assert.AreEqual(-0.4, DoorMath.SplitFanAngle(0, 2, 0.8), 1e-9);
            Assert.AreEqual(0.4, DoorMath.SplitFanAngle(1, 2, 0.8), 1e-9);
            Assert.AreEqual(0, DoorMath.SplitFanAngle(1, 3, 0.8), 1e-9);
            Assert.AreEqual(-0.4, DoorMath.SplitFanAngle(0, 4, 0.8), 1e-9);
            Assert.AreEqual(0.4, DoorMath.SplitFanAngle(3, 4, 0.8), 1e-9);
        }

        [Test]
        public void PanDistanceMatchesTheDesignWorldOverhang()
        {
            Assert.AreEqual(394, DesignSpace.PanDistance, 1e-9);
        }
    }
}
