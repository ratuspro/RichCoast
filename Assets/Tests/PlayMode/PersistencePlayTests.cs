using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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

        // ---- the live game: capture, restore and the app states ----

        static GameBootstrap Boot() => UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();

        static IEnumerator LoadWith(AppIntent intent)
        {
            GameBootstrap.PendingIntent = intent;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        static IEnumerator LoadRun() => LoadWith(AppIntent.NewRun);

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutS)
        {
            float deadline = Time.time + timeoutS;
            while (!condition() && Time.time < deadline) yield return null;
        }

        /// <summary>
        /// Drop a ball and wait for a FULL settle cycle. Waiting only for quiescence is not enough:
        /// in the instant after a spawn the body is awake but still at zero speed, so IsSettled is
        /// briefly true and the checkpoint edge never fires. Wait for motion first, then for rest.
        /// </summary>
        static IEnumerator DropAndSettle(GameBootstrap boot, float x, int tier)
        {
            boot.DebugDrop(x, tier);
            yield return WaitUntil(() => !boot.ZoneA.IsQuiescent, 5f);
            Assert.IsFalse(boot.ZoneA.IsQuiescent, "the dropped ball never started moving");
            yield return WaitUntil(() => boot.ZoneA.IsQuiescent, 15f);
            Assert.IsTrue(boot.ZoneA.IsQuiescent, "the board never settled");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADropThatSettlesWritesACheckpointContainingTheBoard()
        {
            yield return LoadRun();
            var boot = Boot();

            yield return DropAndSettle(boot, 0f, 3);

            var loaded = SaveStore.Load(Curve());
            Assert.IsTrue(loaded.hasRun, "settling must have written a checkpoint");
            Assert.GreaterOrEqual(loaded.run.board.Count, 1, "the dropped ball belongs in the snapshot");
            Assert.AreEqual(boot.ZoneA.Level, loaded.run.level);
            Assert.AreEqual(boot.ZoneA.BallBuffer, loaded.run.ballBuffer);
        }

        [UnityTest]
        public IEnumerator ACapturedSnapshotSatisfiesItsOwnContract()
        {
            yield return LoadRun();
            var boot = Boot();

            yield return DropAndSettle(boot, 0f, 2);

            // Load re-validates; hasRun surviving means a REAL capture passes SaveSchema.ValidateRun.
            Assert.IsTrue(SaveStore.Load(Curve()).hasRun, "a real capture must survive its own validation");
        }

        [UnityTest]
        public IEnumerator NoCheckpointIsTakenWhileZoneBHasBallsInFlight()
        {
            yield return LoadRun();
            var boot = Boot();

            GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(3), DesignSpace.Width / 2));
            yield return null;
            Assert.Greater(boot.ZoneB.InFlight, 0, "precondition: a ball is cascading");
            Assert.IsFalse(boot.ZoneA.IsQuiescent, "a cascading Zone B is not a safe capture point");
        }

        [UnityTest]
        public IEnumerator ARestoredRunCarriesLevelScoreBufferAndBoard()
        {
            var snap = new RunSnapshot
            {
                level = 4, score = 9999, ballBuffer = 3, arenaScale = 1f,
                currentTier = 2, nextTier = 3, barFilled = 12, barTarget = 400,
                board =
                {
                    new BallSpawn { tier = 2, x = 120, yFromTop = 300 },
                    new BallSpawn { tier = 3, x = 260, yFromTop = 300 },
                },
            };
            yield return LoadWith(AppIntent.Title);
            var boot = Boot();
            boot.StartRun(snap);
            yield return null;

            Assert.AreEqual(4, boot.ZoneA.Level);
            Assert.AreEqual(9999, boot.ZoneA.Score, 1e-6);
            Assert.AreEqual(3, boot.ZoneA.BallBuffer);
            Assert.AreEqual(2, boot.Board.BallCount);
            Assert.AreEqual(9999, boot.ZoneB.Total, 1e-6, "Zone B owns the lifetime total that Zone A mirrors");
            Assert.AreEqual(12, boot.ZoneB.BarFilled, 1e-6);
        }

        /// <summary>
        /// The step-2-before-step-5 ordering trap: RadiusForTier divides by ArenaScale, so restoring
        /// balls BEFORE the scale gives every one of them the unscaled radius. Comparing a scaled
        /// restore against an unscaled one catches exactly that, without depending on the ladder asset.
        /// </summary>
        [UnityTest]
        public IEnumerator ArenaScaleIsAppliedBeforeBallsSoRadiiAreCorrect()
        {
            RunSnapshot Snap(float scale) => new RunSnapshot
            {
                level = 1, ballBuffer = 3, arenaScale = scale,
                currentTier = 1, nextTier = 1, barTarget = 100,
                board = { new BallSpawn { tier = 3, x = DesignSpace.Width / 2, yFromTop = 300 } },
            };

            yield return LoadWith(AppIntent.Title);
            Boot().StartRun(Snap(1f));
            yield return null;
            float unscaled = 0f;
            foreach (var ball in Boot().Board.Balls) unscaled = ball.Radius;
            Assert.Greater(unscaled, 0f, "precondition: the unscaled ball restored");

            yield return LoadWith(AppIntent.Title);
            Boot().StartRun(Snap(2f));
            yield return null;
            float scaled = 0f;
            foreach (var ball in Boot().Board.Balls) scaled = ball.Radius;

            Assert.AreEqual(unscaled / 2f, scaled, 1e-3f,
                "a restored ball ignored the milestone arena scale — SetArenaScale must precede Board.Restore");
        }

        /// <summary>
        /// Without ThemeDirector.SnapTo a restored run stays painted workshop until the next milestone
        /// zoom, because BeginFade is the only thing that ever moves the director's current palette.
        /// </summary>
        [UnityTest]
        public IEnumerator ARestoredMidRunLevelPaintsItsPaletteImmediately()
        {
            var curve = Curve();
            int level = 0;
            for (int l = 2; l <= 400 && level == 0; l++)
                if (curve.PaletteNameForLevel(l) != Palettes.WorkshopName) level = l;
            Assert.Greater(level, 0, "no non-workshop palette found anywhere in the curve");

            var window = curve.WindowForLevel(level);
            var snap = new RunSnapshot
            {
                level = level, ballBuffer = 3, arenaScale = 1f,
                currentTier = window.min, nextTier = window.min,
                barFilled = 1, barTarget = curve.ScoreBarTargetForLevel(level),
            };

            yield return LoadWith(AppIntent.Title);
            var boot = Boot();
            boot.StartRun(snap);
            yield return null;

            Assert.AreEqual(curve.PaletteNameForLevel(level), boot.Themes.CurrentPaletteName,
                "a restored run must boot already wearing its palette");
        }

        [UnityTest]
        public IEnumerator AGameOverClearsTheSavedRunAndMergesRecords()
        {
            yield return LoadRun();
            GameEvents.RaiseGameOver(5555);
            yield return null;

            var loaded = SaveStore.Load(Curve());
            Assert.IsFalse(loaded.hasRun, "a dead run must not be resumable");
            Assert.AreEqual(5555, loaded.records.bestScore, 1e-6);
            Assert.AreEqual(1, loaded.records.runsPlayed);
        }

        [UnityTest]
        public IEnumerator GameOverShowsTheRecordAndOffersTheMenu()
        {
            yield return LoadRun();
            GameEvents.RaiseGameOver(1234);
            yield return null;
            Assert.IsNotNull(GameObject.Find("Best"), "game over must show the record");
            Assert.IsNotNull(GameObject.Find("MenuButton"), "game over must offer a way back to the menu");
        }

        [UnityTest]
        public IEnumerator BootingWithoutAnIntentShowsTheTitleAndNoSession()
        {
            yield return LoadWith(AppIntent.Title);
            var boot = Boot();
            Assert.AreEqual(GameBootstrap.AppState.Title, boot.State);
            Assert.IsNull(boot.Session, "no run may exist behind the title");
            Assert.IsNotNull(GameObject.Find("TitleScreen"), "the title screen must be on the overlay canvas");
        }

        [UnityTest]
        public IEnumerator TheTitleOffersContinueOnlyWhenARunWasSaved()
        {
            yield return LoadWith(AppIntent.Title);
            Assert.IsNull(GameObject.Find("Continue"), "a fresh install has nothing to continue");
            Assert.IsNotNull(GameObject.Find("Play"));

            var save = new SaveData();
            save.SetRun(new RunSnapshot { level = 2, ballBuffer = 3, barTarget = 100 });
            SaveStore.Save(save);

            yield return LoadWith(AppIntent.Title);
            Assert.IsNotNull(GameObject.Find("Continue"), "a saved run must be resumable from the title");
            Assert.IsNotNull(GameObject.Find("NewRun"));
            Assert.IsNull(GameObject.Find("Play"), "PLAY is replaced by CONTINUE + NEW RUN");
        }

        [UnityTest]
        public IEnumerator RequestQuitRaisesAConfirmAndIsIdempotent()
        {
            yield return LoadWith(AppIntent.Title);
            var boot = Boot();
            Assert.IsNull(GameObject.Find("Confirm"), "nothing should be asking yet");

            boot.RequestQuit();
            yield return null;
            Assert.IsNotNull(GameObject.Find("Confirm"), "Back must raise a quit confirmation");

            // A second Back while the dialog is up must not stack another copy.
            boot.RequestQuit();
            yield return null;
            var confirms = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                if (t.name == "Confirm") confirms++;
            Assert.AreEqual(1, confirms, "a second Back must not stack another dialog");
        }

        /// <summary>
        /// Found on a Pixel 7: balls kept dropping while the quit dialog was up. A uGUI scrim blocks
        /// only uGUI raycasts, and Zone A's aim and Zone C's trap-door both read Pointer.current
        /// directly — so a modal has to freeze them explicitly.
        /// </summary>
        [UnityTest]
        public IEnumerator AModalFreezesAimingAndDisarmsTheTrapDoor()
        {
            yield return LoadRun();
            var boot = Boot();
            Assert.IsFalse(boot.Session.Aim.IsFrozen, "precondition: phase A aiming is live");

            boot.RequestQuit();
            yield return null;
            Assert.IsTrue(boot.Session.Aim.IsFrozen, "a modal must freeze aiming — the scrim only stops uGUI");

            GameEvents.RaiseModalOpen(false);
            yield return null;
            Assert.IsFalse(boot.Session.Aim.IsFrozen, "closing the modal must return the aim");
        }

        /// <summary>
        /// The trap-door reads the pointer directly too, so a modal must disarm it. Checked in phase B,
        /// where it is genuinely armed — asserting this in phase A would pass regardless of the fix.
        /// </summary>
        [UnityTest]
        public IEnumerator AModalDisarmsTheTrapDoorInPhaseB()
        {
            yield return LoadRun();
            var boot = Boot();

            GameEvents.RaisePhaseChanged(GamePhase.B);
            yield return null;
            Assert.IsTrue(boot.ZoneC.IsArmed, "precondition: the door arms in phase B with Zone B empty");

            GameEvents.RaiseModalOpen(true);
            yield return null;
            Assert.IsFalse(boot.ZoneC.IsArmed, "a modal must disarm the trap-door");

            GameEvents.RaiseModalOpen(false);
            yield return null;
            Assert.IsTrue(boot.ZoneC.IsArmed, "closing the modal must re-arm it");
        }

        [UnityTest]
        public IEnumerator ClosingTheQuitConfirmReArmsTheGame()
        {
            yield return LoadRun();
            var boot = Boot();
            boot.RequestQuit();
            yield return null;

            var cancel = GameObject.Find("ConfirmNo");
            Assert.IsNotNull(cancel, "the confirm must offer CANCEL");
            cancel.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.IsNull(GameObject.Find("Confirm"), "CANCEL must dismiss the dialog");
            boot.DebugDrop(0f, 1);
            yield return null;
            Assert.Less(boot.ZoneA.BallBuffer, ProgressionCurve.BufferForLevel(1),
                "play must resume once the dialog is gone");
        }

        [UnityTest]
        public IEnumerator PausingPersistsASettledRun()
        {
            yield return LoadRun();
            var boot = Boot();
            yield return DropAndSettle(boot, 0f, 2);

            SaveStore.Delete();
            boot.SendMessage("OnApplicationPause", true);
            yield return null;

            Assert.IsTrue(SaveStore.Load(Curve()).hasRun, "a pause with the run settled must persist it");
        }
    }
}
