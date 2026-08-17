using NUnit.Framework;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Tests
{
    /// <summary>
    /// The trap-door marker. Landing on discrete columns, and moving between them without ever
    /// jumping, is what makes the entry point a skill the player can track rather than a lottery.
    /// </summary>
    public class DoorSweepTests
    {
        [Test]
        public void StartsOnTheFirstColumn()
        {
            var sweep = DoorSweep.New();
            Assert.That(sweep.Index, Is.EqualTo(0));
        }

        [Test]
        public void StepsOneColumnPerDwell()
        {
            var sweep = DoorSweep.New();
            sweep.Tick(DoorSweep.DefaultStepMs);
            Assert.That(sweep.Index, Is.EqualTo(1));

            sweep.Tick(DoorSweep.DefaultStepMs);
            Assert.That(sweep.Index, Is.EqualTo(2));
        }

        [Test]
        public void DoesNotStepBeforeTheDwellElapses()
        {
            var sweep = DoorSweep.New();
            sweep.Tick(DoorSweep.DefaultStepMs * 0.9f);
            Assert.That(sweep.Index, Is.EqualTo(0));
        }

        [Test]
        public void PingPongsOffBothEndsInsteadOfWrapping()
        {
            var sweep = DoorSweep.New();

            // Walk to the far edge...
            for (var i = 0; i < DoorSweep.Columns - 1; i++) sweep.Tick(DoorSweep.DefaultStepMs);
            Assert.That(sweep.Index, Is.EqualTo(DoorSweep.Columns - 1));

            // ...and the next step must come back, not jump to 0.
            sweep.Tick(DoorSweep.DefaultStepMs);
            Assert.That(sweep.Index, Is.EqualTo(DoorSweep.Columns - 2));
        }

        [Test]
        public void TheMarkerOnlyEverMovesToAnAdjacentColumn()
        {
            // The player aims by tracking the lit pip, so the marker must never jump — which is
            // exactly what a wrapping sweep would do at the edges.
            var sweep = DoorSweep.New();
            var previous = sweep.Index;

            for (var i = 0; i < 200; i++)
            {
                sweep.Tick(DoorSweep.DefaultStepMs);
                Assert.That(Mathf.Abs(sweep.Index - previous), Is.EqualTo(1), "the marker jumped columns");
                previous = sweep.Index;
            }
        }

        [Test]
        public void EachLapVisitsTheInteriorColumnsTwiceAndTheTurningColumnsOnce()
        {
            // The consequence of ping-pong: the marker passes through a middle column in both
            // directions but only turns on the edges once. Every visit lasts one full dwell, so
            // the edges are reachable — just rarer, which is what makes an edge entry a flex.
            var sweep = DoorSweep.New();
            var visits = new int[DoorSweep.Columns];

            var lap = (DoorSweep.Columns - 1) * 2; // there and back
            for (var i = 0; i < lap; i++)
            {
                visits[sweep.Index]++;
                sweep.Tick(DoorSweep.DefaultStepMs);
            }

            Assert.That(visits[0], Is.EqualTo(1), "left edge");
            Assert.That(visits[DoorSweep.Columns - 1], Is.EqualTo(1), "right edge");
            for (var i = 1; i < DoorSweep.Columns - 1; i++)
            {
                Assert.That(visits[i], Is.EqualTo(2), $"interior column {i}");
            }
        }

        [Test]
        public void NeverLeavesTheColumnRange()
        {
            var sweep = DoorSweep.New();
            for (var i = 0; i < 500; i++)
            {
                sweep.Tick(DoorSweep.DefaultStepMs);
                Assert.That(sweep.Index, Is.InRange(0, DoorSweep.Columns - 1));
            }
        }

        [Test]
        public void ColumnsSpanTheBoardEvenlyAndStayInsetFromTheWalls()
        {
            const float margin = 24f;

            Assert.That(DoorSweep.ColumnX(0, margin), Is.EqualTo(margin));
            Assert.That(DoorSweep.ColumnX(DoorSweep.Columns - 1, margin), Is.EqualTo(Layout.Width - margin));

            var spacing = DoorSweep.ColumnX(1, margin) - DoorSweep.ColumnX(0, margin);
            for (var i = 2; i < DoorSweep.Columns; i++)
            {
                Assert.That(DoorSweep.ColumnX(i, margin) - DoorSweep.ColumnX(i - 1, margin),
                    Is.EqualTo(spacing).Within(1e-3f), $"column {i} is unevenly spaced");
            }
        }

        [Test]
        public void ResetReturnsTheSweepToItsStartingEdge()
        {
            var sweep = DoorSweep.New();
            sweep.Tick(DoorSweep.DefaultStepMs * 3f);
            sweep.Reset();
            Assert.That(sweep.Index, Is.EqualTo(0));

            // And it resumes moving forward, not backward off the edge.
            sweep.Tick(DoorSweep.DefaultStepMs);
            Assert.That(sweep.Index, Is.EqualTo(1));
        }
    }
}
