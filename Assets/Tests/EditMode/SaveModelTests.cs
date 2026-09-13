using NUnit.Framework;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Tests.EditMode
{
    /// <summary>
    /// The save model. <see cref="Records.Merge"/> is the only logic here, but the JSON round-trip
    /// tests are the load-bearing ones: they pin down two JsonUtility behaviours the whole save layer
    /// is built on — that large scores survive intact, and that a nested class can never be null.
    /// </summary>
    public class SaveModelTests
    {
        [Test]
        public void FreshSaveHasSoundAndHapticsOnAndNoRun()
        {
            var save = new SaveData();
            Assert.AreEqual(SaveSchema.Current, save.schemaVersion);
            Assert.IsTrue(save.settings.soundOn);
            Assert.IsTrue(save.settings.hapticsOn);
            Assert.IsFalse(save.hasRun);
            Assert.AreEqual(0, save.records.bestScore, 1e-9);
        }

        [Test]
        public void MergeTakesAHigherScoreAndReportsIt()
        {
            var r = new Records();
            Assert.IsTrue(r.Merge(1200, 7));
            Assert.AreEqual(1200, r.bestScore, 1e-9);
            Assert.AreEqual(7, r.bestLevel);
            Assert.AreEqual(1, r.runsPlayed);
        }

        [Test]
        public void MergeIgnoresALowerScoreButStillCountsTheRun()
        {
            var r = new Records();
            r.Merge(1200, 7);
            Assert.IsFalse(r.Merge(800, 4));
            Assert.AreEqual(1200, r.bestScore, 1e-9);
            Assert.AreEqual(7, r.bestLevel);
            Assert.AreEqual(2, r.runsPlayed);
        }

        [Test]
        public void MergeTracksBestLevelIndependentlyOfBestScore()
        {
            // A long, low-scoring run can beat the best LEVEL without beating the best SCORE.
            var r = new Records();
            r.Merge(5000, 3);
            r.Merge(100, 19);
            Assert.AreEqual(5000, r.bestScore, 1e-9);
            Assert.AreEqual(19, r.bestLevel);
        }

        /// <summary>
        /// THE GATE. Scores reach 3^19 ≈ 1.16e9 per ball and compound from there. If this fails, the
        /// score fields must become round-trip-formatted strings — a lossy best score is not shippable.
        /// </summary>
        [Test]
        public void LargeScoresSurviveAJsonRoundTripExactly()
        {
            const double big = 987654321987.65432;
            var save = new SaveData();
            save.records.bestScore = big;
            save.SetRun(new RunSnapshot { score = big, barTarget = 1e9, barFilled = 123456.789 });

            var round = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));

            Assert.AreEqual(big, round.records.bestScore, 0.0, "best score lost precision through JSON");
            Assert.AreEqual(big, round.run.score, 0.0, "run score lost precision through JSON");
            Assert.AreEqual(123456.789, round.run.barFilled, 0.0, "bar fill lost precision through JSON");
        }

        [Test]
        public void HasRunSurvivesARoundTripAndClearRunResetsIt()
        {
            // JsonUtility cannot express a null nested class, which is exactly why hasRun exists.
            var save = new SaveData();
            save.SetRun(new RunSnapshot { level = 12 });
            var round = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.IsTrue(round.hasRun);
            Assert.AreEqual(12, round.run.level);

            round.ClearRun();
            var cleared = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(round));
            Assert.IsFalse(cleared.hasRun);
            Assert.IsNotNull(cleared.run, "JsonUtility always materialises the nested object");
        }

        [Test]
        public void TheBoardListSurvivesARoundTrip()
        {
            var save = new SaveData();
            save.SetRun(new RunSnapshot
            {
                board =
                {
                    new BallSpawn { tier = 3, x = 120.5, yFromTop = 400.25 },
                    new BallSpawn { tier = 7, x = 260.75, yFromTop = 100.125 },
                },
            });

            var round = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));

            Assert.AreEqual(2, round.run.board.Count);
            Assert.AreEqual(3, round.run.board[0].tier);
            Assert.AreEqual(120.5, round.run.board[0].x, 0.0);
            Assert.AreEqual(400.25, round.run.board[0].yFromTop, 0.0);
            Assert.AreEqual(7, round.run.board[1].tier);
        }
    }
}
