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

            const int w = 1080, h = 2340;
            var rt = new RenderTexture(w, h, 24);
            var cam = boot.Cam;
            cam.targetTexture = rt;
            // The rig fitted board width to the batch game view's aspect; re-fit for the portrait target.
            cam.GetComponent<RichCoast.Game.CameraRig>().Apply();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Canvas.ForceUpdateCanvases();
            cam.Render();
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            var dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            var path = Path.GetFullPath(Path.Combine(dir, "game-scene.png"));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[ScreenshotCapture] wrote {path}");
            Assert.IsTrue(File.Exists(path));
        }
    }
}
