using System.Collections;
using NUnit.Framework;
using RichCoast.Core;
using RichCoast.Gameplay;
using RichCoast.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// Boots the real game scene and lets it run. The EditMode suite proves the rules are right;
    /// this proves they are actually wired up — that the scene loads, the systems construct, the
    /// physics world is configured, and a dropped ball falls and merges like it should.
    ///
    /// PlayMode tests fail on any logged error, so this doubles as a "nothing throws on boot" check.
    /// </summary>
    public class GameSceneSmokeTests
    {
        private const string GameScene = "Game";

        [UnitySetUp]
        public IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync(GameScene, LoadSceneMode.Single);
            yield return null; // let Awake/Start run
        }

        [UnityTest]
        public IEnumerator SceneBootsWithAContextAndAConfiguredPhysicsWorld()
        {
            var root = Object.FindFirstObjectByType<GameRoot>();
            Assert.That(root, Is.Not.Null, "the scene has no GameRoot");
            Assert.That(root.Context, Is.Not.Null, "GameRoot did not build a context");
            Assert.That(root.Context.Progression.Stages, Is.Not.Empty, "progression data did not load");

            yield return null;

            // Gravity is set from the arena scale, so a wrong sign or a missing setup is visible here.
            Assert.That(Physics2D.gravity.y, Is.LessThan(0f), "gravity must pull toward the funnel floor");
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / Tuning.StepsPerSecond).Within(1e-5f),
                "the ported tuning assumes a 60 Hz step");
        }

        [UnityTest]
        public IEnumerator TheHudAndArenaExistAndTheRunStartsWithAFullBuffer()
        {
            var hud = Object.FindFirstObjectByType<HudView>();
            Assert.That(hud, Is.Not.Null, "no HUD in the scene");

            var walls = GameObject.Find("Zone A");
            Assert.That(walls, Is.Not.Null, "Zone A never built its arena");
            Assert.That(walls.GetComponentsInChildren<BoxCollider2D>().Length, Is.EqualTo(4),
                "the arena should be four boundary walls: ceiling, both sides, floor");

            var bus = Object.FindFirstObjectByType<GameRoot>().Context.Bus;
            var count = -1;
            bus.Subscribe<BallBufferChanged>(e => count = e.Count);

            // The run starts full: the level-1 refill is the tutorial supply.
            bus.Emit(new ScoreBarFilled());
            yield return new WaitForSeconds(0.5f);

            Assert.That(count, Is.GreaterThan(0), "a cash-in should drip refill slots into the buffer");
        }

        [UnityTest]
        public IEnumerator TwoDroppedBallsFallAndMergeIntoOneOfTheNextTier()
        {
            // The real physics end to end: gravity pulls the balls down, the colliders touch, the
            // board resolves the contact and the pool hands back one ball a tier higher.
            var zoneA = Object.FindFirstObjectByType<GameRoot>().ZoneA;
            Assert.That(zoneA, Is.Not.Null);

            var column = Layout.Width * 0.5f;
            Assert.That(zoneA.DebugDrop(column), Is.True, "the run should start with a full buffer");
            yield return new WaitForSeconds(0.8f); // let the first ball settle on the floor

            Assert.That(zoneA.DebugDrop(column), Is.True);
            yield return new WaitForSeconds(1.5f);

            // Level 1's draw window is [1,1], so both balls are tier 1 and the merge is certain.
            Assert.That(zoneA.BallCount, Is.EqualTo(1), "the pair should have merged into a single ball");
            Assert.That(zoneA.HighestTier, Is.EqualTo(2), "a merge steps the tier up exactly one");
        }

        [UnityTest]
        public IEnumerator ABallDroppedIntoZoneBIsReportedBusyThenEmpty()
        {
            var bus = Object.FindFirstObjectByType<GameRoot>().Context.Bus;
            var busy = false;
            var empty = false;
            var scored = 0d;

            bus.Subscribe<ZoneBBusy>(_ => busy = true);
            bus.Subscribe<ZoneBEmpty>(_ => empty = true);
            bus.Subscribe<ScoreChanged>(e => scored = e.Total);

            bus.Emit(new BallDropped(BallSpec.FromTier(4), Layout.Width * 0.5f));
            yield return null;
            Assert.That(busy, Is.True, "Zone B must report busy so the trap-door locks");

            empty = false;
            yield return new WaitForSeconds(1.5f);

            Assert.That(empty, Is.True, "Zone B must report empty so the trap-door can re-arm");
            Assert.That(scored, Is.GreaterThan(0d), "the drained ball should have scored");
        }
    }
}
