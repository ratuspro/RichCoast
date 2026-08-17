using System.Collections;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Gameplay;
using RichCoast.Gameplay.ZoneA;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// The milestone beat: every 20 levels the arena grows to make room for balls that have
    /// outgrown it, the camera zooms out to match so apparent ball size holds steady, and tiers
    /// that just fell out of the draw window drain away instead of clogging a board that can no
    /// longer produce their match.
    ///
    /// Levels are driven straight through the seam here, the way a Zone B cash-in drives them.
    /// </summary>
    public class MilestoneZoomTests
    {
        private GameRoot _root;
        private EventBus _bus;

        [UnitySetUp]
        public IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            _root = Object.FindFirstObjectByType<GameRoot>();
            _bus = _root.Context.Bus;
            yield return null; // the opening broadcasts land on the first tick
        }

        /// <summary>Roll the run forward, as a cash-in crossing that many levels would.</summary>
        private void CrossLevels(int levels)
        {
            for (var i = 0; i < levels; i++) _bus.Emit(new ScoreBarFilled());
            _bus.Emit(new ScoreBarCashedIn());
        }

        [UnityTest]
        public IEnumerator OrdinaryLevelsDoNotGrowTheArena()
        {
            var camera = FindArenaCamera();
            var before = camera.orthographicSize;

            CrossLevels(3);
            yield return new WaitForSeconds(1.5f);

            Assert.That(camera.orthographicSize, Is.EqualTo(before).Within(0.01f),
                "the arena grew on a level that is not a milestone");
        }

        [UnityTest]
        public IEnumerator AMilestoneGrowsTheArenaAndZoomsTheCameraToMatch()
        {
            var camera = FindArenaCamera();
            var before = camera.orthographicSize;

            // Level 20 is the first milestone: the window jumps [1,4] → [5,8].
            CrossLevels(19);
            yield return new WaitForSeconds(2f);

            Assert.That(camera.orthographicSize, Is.GreaterThan(before * 1.2f),
                "the arena camera never zoomed out for the milestone");

            // The authored growth is the neutral ball match × the stage's tightness, so a
            // window-max ball keeps roughly its apparent size rather than swelling.
            var tiers = _root.Context.Tiers;
            var expected = BallMath.NeutralGrowth(tiers, 4, 8) * _root.Context.Progression.GetStage(20).Tightness;
            Assert.That(camera.orthographicSize / before, Is.EqualTo(expected).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator AMilestoneLocksInputWhileTheArenaIsMidGrowth()
        {
            var zoomStates = new System.Collections.Generic.List<bool>();
            _bus.Subscribe<ArenaZoom>(e => zoomStates.Add(e.Active));

            CrossLevels(19);
            yield return null;

            Assert.That(zoomStates, Does.Contain(true), "nothing told Zone C to lock the trap-door");

            yield return new WaitForSeconds(2f);
            Assert.That(zoomStates[zoomStates.Count - 1], Is.False, "the zoom never reported finishing");
        }

        [UnityTest]
        public IEnumerator AMilestoneDrainsTiersThatJustLeftTheDrawWindow()
        {
            // Fill the board with tier-1 balls, then cross into the [5,8] window: every one of them
            // is now unmergeable and must leave.
            for (var i = 0; i < 4; i++)
            {
                _root.ZoneA.DebugDrop(Layout.Width * (0.3f + 0.12f * i));
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(1f);
            Assert.That(_root.ZoneA.BallCount, Is.GreaterThan(0), "no balls were on the board to drain");

            var dropped = 0;
            _bus.Subscribe<BallDropped>(_ => dropped++);

            CrossLevels(19);
            yield return new WaitForSeconds(2f);

            Assert.That(_root.ZoneA.BallCount, Is.EqualTo(0), "blacklisted balls stayed on the board");
            Assert.That(dropped, Is.GreaterThan(0), "the drained balls never reached Zone B");
        }

        /// <summary>The camera that draws Zone A — the one whose zoom the milestone changes.</summary>
        private static Camera FindArenaCamera()
        {
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.name == "Arena Camera") return camera;
            }
            Assert.Fail("the scene has no arena camera");
            return null;
        }
    }
}
