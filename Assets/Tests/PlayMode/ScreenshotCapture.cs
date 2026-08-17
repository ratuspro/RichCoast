using System.Collections;
using System.IO;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// Renders the running game to a PNG so the layout can be eyeballed without a device.
    ///
    /// Marked <see cref="ExplicitAttribute"/>: it is a tool, not a test — it asserts almost
    /// nothing and would only slow the suite down. Run it with <c>Tools/screenshot.sh</c>, which
    /// launches the editor WITH graphics (a headless run has no framebuffer to capture).
    /// </summary>
    [Explicit("Capture tool — run via Tools/screenshot.sh")]
    public class ScreenshotCapture
    {
        /// <summary>Capture size — a portrait phone shape, matching the authored 390×844 design.</summary>
        private const int CaptureWidth = 540;
        private const int CaptureHeight = 1170;

        [UnityTest]
        public IEnumerator CaptureGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            yield return new WaitForSeconds(0.5f);

            // Drop a few balls so the shot shows an actual board rather than an empty arena.
            var zoneA = Object.FindFirstObjectByType<GameRoot>().ZoneA;
            for (var i = 0; i < 6; i++)
            {
                zoneA.DebugDrop(Layout.Width * (0.25f + 0.1f * i));
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(1.5f); // let the board settle

            var path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "game-scene.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // A batchmode run has no real backbuffer, so ScreenCapture returns nothing. Render the
            // camera into a texture instead — which also pins the output to a fixed portrait size
            // regardless of the machine running it.
            var camera = Camera.main;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var previousMode = canvas.renderMode;
            // An overlay canvas bypasses cameras entirely and would be missing from the shot.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;

            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, mipChain: false);
            texture.ReadPixels(new UnityEngine.Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            canvas.renderMode = previousMode;
            Object.Destroy(texture);
            target.Release();
            Object.Destroy(target);

            Debug.Log($"[RichCoast] Screenshot written to {path}");
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
