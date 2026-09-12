using System;
using System.Collections;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>The M3 milestone beat against the real Main scene: arena zoom-out, blacklist drain, palette swap, door lock.</summary>
    public class MilestonePlayTests
    {
        static IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        static GameBootstrap Boot() => UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();

        static IEnumerator WaitUntil(Func<bool> condition, float timeoutS)
        {
            float deadline = Time.time + timeoutS;
            while (!condition() && Time.time < deadline) yield return null;
        }

        /// <summary>Cash the bar in enough times (in phase A) to land exactly on the first authored milestone.</summary>
        static void AdvanceToLevel(GameBootstrap boot, int target)
        {
            while (boot.ZoneA.Level < target) GameEvents.RaiseScoreBarFilled();
        }

        [UnityTest]
        public IEnumerator PlainLevelUpsNeverZoomOrRecolour()
        {
            yield return LoadMain();
            var boot = Boot();
            int zooms = 0;
            GameEvents.ArenaZoom += _ => zooms++;
            AdvanceToLevel(boot, 19);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(19, boot.ZoneA.Level);
            Assert.AreEqual(0, zooms, "no ArenaZoom before a draw-window milestone");
            Assert.That(boot.Geometry.Scale, Is.EqualTo(1f).Within(1e-5f));
            Assert.IsTrue(Theme.Active.SameAs(Palettes.Workshop), "the workshop look holds until a milestone");
        }

        [UnityTest]
        public IEnumerator TheFirstMilestoneGrowsTheArenaDrainsBlacklistedBallsAndSwapsToDusk()
        {
            yield return LoadMain();
            var boot = Boot();
            var rig = boot.Cam.GetComponent<CameraRig>();
            float orthoBefore = boot.Cam.orthographicSize;

            // Two obsolete balls (tiers 1 and 2 are blacklisted once the window shifts to [5,8]), apart so they never merge.
            boot.DebugDrop(-3f, 1);
            boot.DebugDrop(3f, 2);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(2, boot.Board.BallCount);

            bool zoomStarted = false, zoomEnded = false, zoneBBusy = false;
            int themeTicks = 0;
            GameEvents.ArenaZoom += active => { if (active) zoomStarted = true; else zoomEnded = true; };
            GameEvents.ZoneBBusy += () => zoneBBusy = true;
            GameEvents.ThemeChanged += () => themeTicks++;

            AdvanceToLevel(boot, 20);
            yield return null;
            Assert.AreEqual(20, boot.ZoneA.Level);
            Assert.IsTrue(zoomStarted, "level 20 is a draw-window milestone: the zoom-out must start");
            Assert.IsTrue(boot.ZoneA.IsMilestoneZoomActive);
            Assert.IsFalse(boot.ZoneC.IsArmed, "the door is locked during the zoom");

            // The physics snapped to the new scale up front: neutral growth 4→8 (71/34) × tightness 0.92.
            var ladder = TierLadder.Default;
            float expected = (float)(ladder.NeutralGrowth(4, 8) * 0.92);
            Assert.That(boot.Geometry.Scale, Is.EqualTo(expected).Within(1e-4f));
            Assert.That(rig.ViewScale, Is.LessThan(expected), "the camera zoom is tweened, not snapped");

            yield return WaitUntil(() => zoomEnded, 8f);
            Assert.IsTrue(zoomEnded, "ArenaZoom(false) must follow once the drain has landed");
            Assert.IsFalse(boot.ZoneA.IsMilestoneZoomActive);
            Assert.That(rig.ViewScale, Is.EqualTo(expected).Within(1e-4f), "the camera landed on the new scale");
            Assert.That(boot.Cam.orthographicSize, Is.GreaterThan(orthoBefore), "the A framing zoomed out");

            Assert.AreEqual(0, boot.Board.BallCount, "both blacklisted balls drained off the board");
            Assert.IsTrue(zoneBBusy, "the drain signals Zone B busy up front");
            Assert.That(boot.ZoneB.InFlight + (boot.ZoneB.Total > 0 ? 1 : 0), Is.GreaterThan(0), "the drained balls entered Zone B");

            Assert.Greater(themeTicks, 0, "the palette cross-fade ran with the zoom");
            Assert.IsTrue(Theme.Active.SameAs(Palettes.Dusk), "level 20 authors the dusk palette");
            Assert.AreEqual("dusk", boot.Themes.CurrentPaletteName);
            Assert.That(boot.Cam.backgroundColor, Is.EqualTo(Theme.Paper), "bound surfaces restyled to the new palette");
            Assert.IsFalse(boot.ZoneA.IsOver);
        }

        [UnityTest]
        public IEnumerator AMilestoneCashInThatArrivesInPhaseBWaitsForThePanBackToA()
        {
            yield return LoadMain();
            var boot = Boot();
            int zooms = 0;
            GameEvents.ArenaZoom += active => { if (active) zooms++; };

            // Deplete the buffer so the game pans to B, then cash in to the milestone from there.
            int buffer = boot.ZoneA.BallBuffer;
            float[] xs = { -3.5f, 3.5f, -1.5f, 1.5f, -3f, 3f, 0f, -2.5f, 2.5f, 0.5f };
            for (int i = 0; i < buffer; i++)
            {
                boot.DebugDrop(xs[i % xs.Length], 1 + i % 3);
                yield return new WaitForSeconds(0.3f);
            }
            yield return WaitUntil(() => boot.Phases.Phase == GamePhase.B, 12f);
            Assert.AreEqual(GamePhase.B, boot.Phases.Phase);

            AdvanceToLevel(boot, 19);
            GameEvents.RaiseScoreBarFilled(); // level 20 — the milestone — while in B
            yield return null;
            Assert.AreEqual(20, boot.ZoneA.Level);
            Assert.AreEqual(0, zooms, "the zoom is deferred until the pan lands back in A");
            Assert.IsFalse(boot.ZoneA.IsMilestoneZoomActive);

            GameEvents.RaiseScoreBarCashedIn(); // Zone B's whole roll finished → pan up
            yield return WaitUntil(() => boot.Phases.Phase == GamePhase.A, 5f);
            Assert.AreEqual(GamePhase.A, boot.Phases.Phase);
            yield return null;
            Assert.AreEqual(1, zooms, "the deferred milestone zoom runs once phase A lands");
            Assert.IsTrue(boot.ZoneA.IsMilestoneZoomActive);
        }
    }
}
