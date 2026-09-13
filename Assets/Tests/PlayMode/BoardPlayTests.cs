using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>End-to-end smoke tests against the real Main scene: drop → settle → merge → juice.</summary>
    public class BoardPlayTests
    {
        static IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        static GameBootstrap Boot() => Object.FindFirstObjectByType<GameBootstrap>();

        [UnityTest]
        public IEnumerator SceneBootsWithHudAndEmptyBoard()
        {
            yield return LoadMain();
            var boot = Boot();
            Assert.IsNotNull(boot, "GameBootstrap missing from Main scene");
            Assert.IsNotNull(boot.Hud);
            Assert.AreEqual(0, boot.Board.BallCount);
            Assert.AreEqual(ProgressionCurve.BufferForLevel(1), boot.ZoneA.BallBuffer);
            Assert.IsNotNull(Camera.main);
        }

        [UnityTest]
        public IEnumerator TwoEqualBallsDroppedTogetherMergeIntoTheNextTier()
        {
            yield return LoadMain();
            var boot = Boot();
            boot.DebugDrop(-0.3f, 1);
            boot.DebugDrop(0.3f, 1);
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(1, boot.Board.BallCount, "the pair should have merged into one ball");
            Assert.AreEqual(2, boot.Board.Balls.First().Tier);
            Assert.AreEqual(ProgressionCurve.BufferForLevel(1) - 2, boot.ZoneA.BallBuffer);
        }

        [UnityTest]
        public IEnumerator DifferentTiersStackWithoutMerging()
        {
            yield return LoadMain();
            var boot = Boot();
            boot.DebugDrop(0f, 1);
            yield return new WaitForSeconds(1.2f);
            boot.DebugDrop(0f, 2);
            yield return new WaitForSeconds(2.0f);
            Assert.AreEqual(2, boot.Board.BallCount);
            Assert.IsFalse(boot.ZoneA.IsOver);
        }
    }

    /// <summary>
    /// Renders a populated board to Logs/game-scene.png through a 1080×2340 render texture — the
    /// headless visual check behind <c>Tools/screenshot.sh</c> (needs graphics: no -nographics).
    /// </summary>
    public class ScreenshotCapture
    {
        [UnityTest]
        public IEnumerator CaptureGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.IsNotNull(boot);
            float[] xs = { -3f, -1.5f, 0f, 1.5f, 3f, -2f, 2f };
            for (int i = 0; i < xs.Length; i++)
            {
                boot.DebugDrop(xs[i], 1 + i % 3);
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(2f);

            var cam = boot.Cam;
            var rig = cam.GetComponent<RichCoast.Game.CameraRig>();
            Capture(cam, rig, "game-scene.png");

            // B framing: pan the camera to Zone B with a few balls mid-cascade and a live score bar.
            // A FIXED arena seed keeps this shot comparable between runs — the playfield is generated
            // fresh per drop — and the middle ball goes down the golden column, so the gilded chute and
            // its burst are in frame.
            rig.Pan = 1f;
            boot.ZoneB.DebugRebuild(104);
            yield return null;
            yield return null;
            double[] columns = { 60, boot.ZoneB.GoldenMouthX, 320 };
            for (int i = 0; i < columns.Length; i++)
            {
                GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(2 + i), columns[i]));
                yield return new WaitForSeconds(0.4f);
            }
            yield return new WaitForSeconds(0.6f);
            Capture(cam, rig, "game-scene-b.png");
        }

        /// <summary>The M3 look: the board at the first milestone — arena grown ×1.92, dusk palette, window [5,8].</summary>
        [UnityTest]
        public IEnumerator CaptureMilestoneScene()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            var boot = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.IsNotNull(boot);
            while (boot.ZoneA.Level < 20) GameEvents.RaiseScoreBarFilled();
            yield return new WaitForSeconds(2.5f); // zoom + cross-fade + (empty) drain

            float[] xs = { -3.8f, -2f, 0f, 2f, 3.8f, -3f, 3f, 1f };
            for (int i = 0; i < xs.Length; i++)
            {
                boot.DebugDrop(xs[i], 5 + i % 4);
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(2.5f);

            var cam = boot.Cam;
            var rig = cam.GetComponent<RichCoast.Game.CameraRig>();
            rig.Pan = 0f;
            Capture(cam, rig, "game-scene-m3.png");
        }

        /// <summary>Render the camera (plus its screen-space canvases) into Logs/<paramref name="file"/> at the portrait target size.</summary>
        static void Capture(Camera cam, RichCoast.Game.CameraRig rig, string file)
        {
            const int w = 1080, h = 2340;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            // The rig fitted board width to the batch game view's aspect; re-fit for the portrait target.
            rig.Apply();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            var dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            var path = Path.GetFullPath(Path.Combine(dir, file));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[ScreenshotCapture] wrote {path}");
            Assert.IsTrue(File.Exists(path));
        }
    }
}
