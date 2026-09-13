using System.IO;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// Persistence, played for real. The EditMode suite proves the model and its contract; these prove
    /// the file actually round-trips and — the load-bearing part — that a damaged save can never take
    /// the game down with it, and can never cost the player their records.
    /// </summary>
    public class PersistencePlayTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Application.temporaryCachePath, "savetests-" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            SaveStore.PathOverride = Path.Combine(tempDir, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            SaveStore.PathOverride = null;
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }

        static ProgressionCurve Curve() => ProgressionCurve.Default;

        static SaveData WithRun()
        {
            var save = new SaveData();
            save.records.Merge(4242, 9);
            save.settings.soundOn = false;
            save.SetRun(new RunSnapshot { level = 3, score = 777, ballBuffer = 2, barTarget = 100, barFilled = 5 });
            return save;
        }

        [Test]
        public void MissingFileLoadsFreshDefaults()
        {
            var save = SaveStore.Load(Curve());
            Assert.IsFalse(save.hasRun);
            Assert.IsTrue(save.settings.soundOn);
            Assert.AreEqual(0, save.records.bestScore, 1e-9);
        }

        [Test]
        public void SavedDataRoundTripsThroughTheFile()
        {
            SaveStore.Save(WithRun());
            var loaded = SaveStore.Load(Curve());
            Assert.AreEqual(4242, loaded.records.bestScore, 1e-9);
            Assert.AreEqual(9, loaded.records.bestLevel);
            Assert.IsFalse(loaded.settings.soundOn);
            Assert.IsTrue(loaded.hasRun);
            Assert.AreEqual(3, loaded.run.level);
            Assert.AreEqual(777, loaded.run.score, 1e-9);
        }

        [Test]
        public void GarbledFileLoadsFreshInsteadOfThrowing()
        {
            File.WriteAllText(SaveStore.FilePath, "{ this is not json at all ");
            var save = SaveStore.Load(Curve());
            Assert.IsFalse(save.hasRun);
            Assert.IsTrue(save.settings.soundOn);
        }

        [Test]
        public void AnInvalidRunIsDroppedButRecordsAndSettingsSurvive()
        {
            var save = WithRun();
            save.run.barFilled = save.run.barTarget + 1; // fails SaveSchema.ValidateRun
            SaveStore.Save(save);

            var loaded = SaveStore.Load(Curve());
            Assert.IsFalse(loaded.hasRun, "the invalid run must be discarded");
            Assert.AreEqual(4242, loaded.records.bestScore, 1e-9, "records must survive a bad run");
            Assert.IsFalse(loaded.settings.soundOn, "settings must survive a bad run");
        }

        [Test]
        public void AVersionMismatchDropsTheRunAndKeepsRecords()
        {
            SaveStore.Save(WithRun());
            var raw = File.ReadAllText(SaveStore.FilePath)
                .Replace($"\"schemaVersion\":{SaveSchema.Current}", "\"schemaVersion\":999");
            File.WriteAllText(SaveStore.FilePath, raw);

            var loaded = SaveStore.Load(Curve());
            Assert.IsFalse(loaded.hasRun, "a run from an unknown format is not trusted");
            Assert.AreEqual(4242, loaded.records.bestScore, 1e-9);
            Assert.AreEqual(SaveSchema.Current, loaded.schemaVersion, "load re-stamps the current version");
        }

        [Test]
        public void DeleteRemovesTheFileAndLoadReturnsDefaults()
        {
            SaveStore.Save(WithRun());
            Assert.IsTrue(File.Exists(SaveStore.FilePath));
            SaveStore.Delete();
            Assert.IsFalse(File.Exists(SaveStore.FilePath));
            Assert.IsFalse(SaveStore.Load(Curve()).hasRun);
        }

        [Test]
        public void SavingTwiceLeavesNoTempFileBehind()
        {
            SaveStore.Save(WithRun());
            SaveStore.Save(WithRun());
            var strays = Directory.GetFiles(tempDir);
            Assert.AreEqual(1, strays.Length, "the atomic write must not leave temp files: " + string.Join(", ", strays));
        }

        [Test]
        public void HapticsEnabledFlagGatesPulse()
        {
            Haptics.Enabled = false;
            Assert.DoesNotThrow(() => Haptics.Pulse(20, 180), "a disabled pulse must be a silent no-op");
            Assert.IsFalse(Haptics.Enabled);
            Haptics.Enabled = true;
            Assert.IsTrue(Haptics.Enabled);
        }
    }
}
