using System.Collections.Generic;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    /// <summary>
    /// The save contract, the counterpart to <see cref="ZoneBGenerator"/>'s Validate. A snapshot that
    /// passes here MUST rebuild into a playable board — this is the only thing standing between a
    /// bit-rotted or hand-edited file and an unplayable run.
    /// <para>Deliberately permissive where a false rejection would cost a player a legitimate run
    /// (see <see cref="MergedTiersAboveTheDrawWindowAreLegal"/>), strict only about what actually
    /// breaks restore.</para>
    /// </summary>
    public class SaveSchemaTests
    {
        static ProgressionCurve Curve() => ProgressionCurve.Default;

        /// <summary>A snapshot shaped like a real mid-run capture.</summary>
        static RunSnapshot Valid()
        {
            var window = Curve().WindowForLevel(5);
            return new RunSnapshot
            {
                level = 5,
                score = 12345.0,
                ballBuffer = 4,
                arenaScale = 1f,
                currentTier = window.min,
                nextTier = window.min,
                barFilled = 10,
                barTarget = 100,
                zoneBTotal = 500,
                board = new List<BallSpawn>
                {
                    new BallSpawn { tier = window.min, x = 100, yFromTop = 400 },
                    new BallSpawn { tier = window.max + 3, x = 250, yFromTop = 300 },
                },
            };
        }

        static void AssertRejected(RunSnapshot run, string because)
        {
            Assert.IsFalse(SaveSchema.ValidateRun(run, Curve(), out var reason), because);
            Assert.IsNotEmpty(reason, "a rejection must say why");
        }

        [Test]
        public void ARealisticSnapshotValidates()
        {
            Assert.IsTrue(SaveSchema.ValidateRun(Valid(), Curve(), out var reason), reason);
        }

        [Test]
        public void MergedTiersAboveTheDrawWindowAreLegal()
        {
            // Merging is HOW you get above the window; rejecting that would discard every real run.
            var run = Valid();
            run.board.Add(new BallSpawn { tier = Curve().WindowForLevel(5).max + 10, x = 50, yFromTop = 200 });
            Assert.IsTrue(SaveSchema.ValidateRun(run, Curve(), out var reason), reason);
        }

        [Test]
        public void RejectsANullRun()
        {
            Assert.IsFalse(SaveSchema.ValidateRun(null, Curve(), out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void RejectsLevelBelowOne()
        {
            var run = Valid();
            run.level = 0;
            AssertRejected(run, "level 0 has no stage");
        }

        [Test]
        public void RejectsNonFiniteScore()
        {
            var run = Valid();
            run.score = double.NaN;
            AssertRejected(run, "a NaN score would poison every total downstream");
        }

        [Test]
        public void RejectsNegativeScore()
        {
            var run = Valid();
            run.score = -1;
            AssertRejected(run, "score never decreases");
        }

        [Test]
        public void RejectsFilledAtOrAboveTarget()
        {
            var run = Valid();
            run.barFilled = run.barTarget;
            AssertRejected(run, "a full bar should have cashed in before the capture");
        }

        [Test]
        public void RejectsNonPositiveTarget()
        {
            var run = Valid();
            run.barTarget = 0;
            AssertRejected(run, "ScoreBar's constructor throws on a non-positive target");
        }

        [Test]
        public void RejectsArenaScaleBelowOne()
        {
            var run = Valid();
            run.arenaScale = 0.5f;
            AssertRejected(run, "arena growth only ever multiplies up from 1");
        }

        [Test]
        public void RejectsNegativeBallBuffer()
        {
            var run = Valid();
            run.ballBuffer = -1;
            AssertRejected(run, "a negative buffer would never refill");
        }

        [Test]
        public void RejectsATierBelowTheWindowFloor()
        {
            // Early levels draw from tier 1, so there is nothing below the floor to reject. Find the
            // first level whose milestone has actually shifted the window up, and test there.
            var curve = Curve();
            int level = 0, floor = 0;
            for (int l = 1; l <= 400 && level == 0; l++)
                if (curve.WindowForLevel(l).min > 1) { level = l; floor = curve.WindowForLevel(l).min; }
            Assert.Greater(level, 0, "no level in the curve has a window floor above tier 1");

            var window = curve.WindowForLevel(level);
            var run = new RunSnapshot
            {
                level = level,
                ballBuffer = 3,
                arenaScale = 1f,
                currentTier = window.min,
                nextTier = window.min,
                barFilled = 1,
                barTarget = curve.ScoreBarTargetForLevel(level),
                board = new List<BallSpawn>
                {
                    new BallSpawn { tier = floor - 1, x = 100, yFromTop = 300 },
                },
            };
            AssertRejected(run, $"tier {floor - 1} is blacklisted at level {level}; the drain takes those");
        }

        [Test]
        public void RejectsABallOffTheBoardHorizontally()
        {
            var run = Valid();
            run.board.Add(new BallSpawn { tier = Curve().WindowForLevel(5).min, x = DesignSpace.Width + 50, yFromTop = 300 });
            AssertRejected(run, "a ball outside the walls would restore into geometry");
        }

        [Test]
        public void RejectsABallBelowTheBoardFloor()
        {
            var run = Valid();
            run.board.Add(new BallSpawn { tier = Curve().WindowForLevel(5).min, x = 100, yFromTop = DesignSpace.BoardHeight + 100 });
            AssertRejected(run, "below the funnel is Zone C, not the board");
        }

        [Test]
        public void RejectsANullBoardList()
        {
            var run = Valid();
            run.board = null;
            AssertRejected(run, "a null list would throw on restore");
        }

        [Test]
        public void RejectsAQueueTierBelowOne()
        {
            var run = Valid();
            run.nextTier = 0;
            AssertRejected(run, "tier 0 does not exist on the ladder");
        }
    }
}
