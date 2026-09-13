using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// The generated Zone B arena, played for real. The EditMode sweep proves the geometry is legal;
    /// these prove the physics agrees — that every arena actually drains, that the golden chute
    /// delivers, and that a reshuffle never lands while balls are in flight.
    /// </summary>
    public class ZoneBArenaPlayTests
    {
        /// <summary>Seeds exercised by the physics tests — a spread, not a cherry-pick.</summary>
        static readonly int[] Seeds = { 1, 17, 63, 104, 255, 777, 1492, 2026 };

        static IEnumerator LoadMain()
        {
            // The scene boots to the title unless told otherwise; these tests all want a live run.
            GameBootstrap.PendingIntent = AppIntent.NewRun;
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

        /// <summary>Lay a known arena and let the destroyed one's colliders actually go away.</summary>
        static IEnumerator Reseed(GameBootstrap boot, int seed)
        {
            boot.ZoneB.DebugRebuild(seed);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryGeneratedArenaDrainsADroppedBall()
        {
            yield return LoadMain();
            var boot = Boot();

            foreach (int seed in Seeds)
            {
                yield return Reseed(boot, seed);
                bool empty = false;
                Action onEmpty = () => empty = true;
                GameEvents.ZoneBEmpty += onEmpty;

                GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(3), DesignSpace.Width / 2));
                yield return null;
                Assert.AreEqual(1, boot.ZoneB.InFlight, $"seed {seed}: the drop must be in flight");

                yield return WaitUntil(() => empty, 20f);
                GameEvents.ZoneBEmpty -= onEmpty;
                Assert.IsTrue(empty, $"seed {seed}: arena never drained — {boot.ZoneB.InFlight} ball(s) stuck: {boot.ZoneB.DescribeBalls()}");
                Assert.AreEqual(0, boot.ZoneB.BallCount, $"seed {seed}");
            }
        }

        [UnityTest]
        public IEnumerator ADropDownTheGoldenColumnStrikesTheGildedGate()
        {
            yield return LoadMain();
            var boot = Boot();

            foreach (int seed in Seeds)
            {
                yield return Reseed(boot, seed);
                int hitMultiplier = 0;
                Action<int> onGolden = m => hitMultiplier = m;
                GameEvents.GoldenGateHit += onGolden;

                // Aim the way a player must: at the trap-door COLUMN nearest the mouth, not at the
                // mouth's own jittered centre. If the aperture maths is wrong this clips a chute rail.
                double column = NearestDoorColumn(boot.ZoneB.GoldenMouthX);
                int expected = boot.ZoneB.GoldenMultiplier;
                GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(2), column));

                yield return WaitUntil(() => hitMultiplier > 0, 8f);
                GameEvents.GoldenGateHit -= onGolden;
                Assert.AreEqual(expected, hitMultiplier,
                    $"seed {seed}: a ball down column {column:0.#} (mouth {boot.ZoneB.GoldenMouthX:0.#}) must reach the gilded gate");

                yield return WaitUntil(() => boot.ZoneB.BallCount == 0, 20f);
            }
        }

        [UnityTest]
        public IEnumerator TheGildedGateOutscoresAnOrdinaryDropOnTheSameArena()
        {
            yield return LoadMain();
            var boot = Boot();
            const int seed = 104;

            yield return Reseed(boot, seed);
            double before = boot.ZoneB.Total;
            // An ordinary drop: the far edge, which the barrier row always catches.
            GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(4), DesignSpace.SweepMargin));
            yield return WaitUntil(() => boot.ZoneB.BallCount == 0 && boot.ZoneB.InFlight == 0, 20f);
            double ordinary = boot.ZoneB.Total - before;

            yield return Reseed(boot, seed);
            before = boot.ZoneB.Total;
            GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(4), NearestDoorColumn(boot.ZoneB.GoldenMouthX)));
            yield return WaitUntil(() => boot.ZoneB.BallCount == 0 && boot.ZoneB.InFlight == 0, 20f);
            double golden = boot.ZoneB.Total - before;

            Assert.That(ordinary, Is.GreaterThan(0), "an ordinary drop still scores");
            Assert.That(golden, Is.GreaterThan(ordinary),
                $"the golden path must pay more than the ordinary cascade (golden {golden}, ordinary {ordinary})");
        }

        [UnityTest]
        public IEnumerator TheArenaReshufflesOnlyWhenNothingIsInFlight()
        {
            yield return LoadMain();
            var boot = Boot();
            int startSeed = boot.ZoneB.Seed;

            // Three overlapping drops — the pattern the milestone drain and the screenshot both produce.
            var reshuffles = new List<string>();
            int lastSeed = startSeed;
            bool sawFlight = false;

            for (int i = 0; i < 3; i++)
            {
                GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(2 + i), 60 + 130 * i));
                float until = Time.time + 0.4f;
                while (Time.time < until)
                {
                    if (boot.ZoneB.InFlight > 0) sawFlight = true;
                    if (boot.ZoneB.Seed != lastSeed)
                    {
                        reshuffles.Add($"seed changed with {boot.ZoneB.InFlight} in flight / {boot.ZoneB.BallCount} alive");
                        lastSeed = boot.ZoneB.Seed;
                    }
                    yield return null;
                }
            }

            yield return WaitUntil(() =>
            {
                if (boot.ZoneB.Seed != lastSeed)
                {
                    reshuffles.Add($"seed changed with {boot.ZoneB.InFlight} in flight / {boot.ZoneB.BallCount} alive");
                    lastSeed = boot.ZoneB.Seed;
                }
                return boot.ZoneB.InFlight == 0 && boot.ZoneB.BallCount == 0 && lastSeed != startSeed;
            }, 25f);

            Assert.IsTrue(sawFlight, "the drops never reached Zone B");
            Assert.That(boot.ZoneB.Seed, Is.Not.EqualTo(startSeed), "draining empty must lay a fresh arena");
            CollectionAssert.AreEqual(
                new[] { "seed changed with 0 in flight / 0 alive" }, reshuffles,
                "the arena must reshuffle exactly once, and only with the playfield clear");
        }

        [UnityTest]
        public IEnumerator AReshuffledArenaKeepsTheScoreBarAndItsRunningTotal()
        {
            yield return LoadMain();
            var boot = Boot();

            GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(3), DesignSpace.Width / 2));
            yield return WaitUntil(() => boot.ZoneB.InFlight == 0 && boot.ZoneB.BallCount == 0, 20f);
            yield return null;
            double banked = boot.ZoneB.Total;

            Assert.That(banked, Is.GreaterThan(0));
            // The bar, its label and the backdrop live outside the arena, so a reshuffle never touches them.
            Assert.IsNotNull(GameObject.Find("BarGroove"), "the score bar groove must survive a reshuffle");
            Assert.IsNotNull(GameObject.Find("BarLabel"), "the score bar label must survive a reshuffle");
            Assert.IsNotNull(GameObject.Find("Arena"), "a fresh playfield must exist after the reshuffle");
            Assert.AreEqual(banked, boot.ZoneB.Total, "a reshuffle must not disturb the running total");
        }

        static double NearestDoorColumn(double x)
        {
            double best = 0, bestDist = double.MaxValue;
            for (int i = 0; i < DesignSpace.SweepPositions; i++)
            {
                double c = DoorMath.SweepPositionX(i, DesignSpace.SweepPositions, DesignSpace.SweepMargin, DesignSpace.Width - DesignSpace.SweepMargin);
                double d = Math.Abs(c - x);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }
    }
}
