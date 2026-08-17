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
        /// <summary>
        /// Capture size. The aspect matches the design world (390×1238), so the whole world —
        /// Zone A, the trap door and Zone B — is in frame at once, which is what makes the shot
        /// useful for checking layout.
        /// </summary>
        private const int CaptureWidth = 480;
        private const int CaptureHeight = 1524;

        [UnityTest]
        public IEnumerator CaptureGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            yield return new WaitForSeconds(0.5f);

            // Drop a few balls so the shot shows an actual board rather than an empty arena, and
            // send one into Zone B so the cascade is visible too.
            var root = Object.FindFirstObjectByType<GameRoot>();
            for (var i = 0; i < 6; i++)
            {
                root.ZoneA.DebugDrop(Layout.Width * (0.25f + 0.1f * i));
                yield return new WaitForSeconds(0.35f);
            }
            root.Context.Bus.Emit(new BallDropped(BallSpec.FromTier(4), Layout.Width * 0.47f));
            yield return new WaitForSeconds(1.2f);

            var path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "game-scene.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            // Shot through a camera of its own rather than the game's.
            //
            // Two reasons. A batchmode run has no real backbuffer, so ScreenCapture returns nothing
            // and the camera has to render into a texture. And the game's own rig frames the world
            // against the device screen — which offscreen is a landscape stub, so the live cameras
            // would show almost nothing. This one renders the whole design world at a fixed
            // portrait size, so the picture means the same thing on every machine.
            var capture = new GameObject("Capture Camera").AddComponent<Camera>();
            capture.orthographic = true;
            capture.orthographicSize = CaptureHeight * 0.5f * (Layout.Width / CaptureWidth);
            capture.transform.position = new Vector3(
                Layout.Width * 0.5f,
                DesignSpace.ToWorldY(capture.orthographicSize),
                -10f);
            capture.clearFlags = CameraClearFlags.SolidColor;
            capture.backgroundColor = Camera.main.backgroundColor;
            capture.cullingMask = ~0; // every zone, whichever camera normally draws it

            var canvas = Object.FindFirstObjectByType<Canvas>();
            var previousMode = canvas.renderMode;
            // An overlay canvas bypasses cameras entirely and would be missing from the shot.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = capture;
            canvas.planeDistance = 1f;

            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;

            capture.targetTexture = target;
            capture.Render();
            RenderTexture.active = target;

            var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, mipChain: false);
            texture.ReadPixels(new UnityEngine.Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = previousActive;
            canvas.renderMode = previousMode;
            Object.Destroy(texture);
            Object.Destroy(capture.gameObject);
            target.Release();
            Object.Destroy(target);

            Debug.Log($"[RichCoast] Screenshot written to {path}");
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
