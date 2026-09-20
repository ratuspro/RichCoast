using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RichCoast.App;
using RichCoast.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RichCoast.Tests.PlayMode
{
    /// <summary>
    /// The cabinet tilt, played for real. The mechanic exists to answer ONE failure — a board of
    /// orphans with no merge partners left, each worth a fraction of the level's bar — so the
    /// load-bearing test is that shaking actually resolves one. The rest guard the cost: three
    /// charges, no refills, and a board that is off screen or frozen cannot be shaken at all.
    /// </summary>
    public class TiltPlayTests
    {
        static IEnumerator LoadMain()
        {
            GameBootstrap.PendingIntent = AppIntent.NewRun;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        static GameBootstrap Boot() => Object.FindFirstObjectByType<GameBootstrap>();

        /// <summary>Settle a crowded mixed-tier pile — the shape a late-game board actually has.</summary>
        static IEnumerator SettledPile(GameBootstrap boot)
        {
            int[] tiers = { 2, 1, 3, 1, 2, 1, 3 };
            for (int i = 0; i < tiers.Length; i++)
            {
                boot.DebugDrop(-1.8f + i * 0.6f, tiers[i]);
                yield return new WaitForSeconds(0.55f);
            }
            float deadline = Time.time + 8f;
            while (!boot.Board.IsSettled() && Time.time < deadline) yield return null;
            yield return new WaitForSeconds(0.3f);
        }

        static Dictionary<EntityId, Vector2> Snapshot(GameBootstrap boot)
        {
            var map = new Dictionary<EntityId, Vector2>();
            foreach (var ball in boot.Board.Balls) map[ball.GetEntityId()] = ball.Position;
            return map;
        }

        /// <summary>Which balls are touching which — the graph an orphan is trapped in.</summary>
        static HashSet<string> Contacts(GameBootstrap boot)
        {
            var balls = boot.Board.Balls.ToList();
            var pairs = new HashSet<string>();
            for (int i = 0; i < balls.Count; i++)
                for (int j = i + 1; j < balls.Count; j++)
                {
                    float gap = Vector2.Distance(balls[i].Position, balls[j].Position)
                              - (balls[i].Radius + balls[j].Radius);
                    if (gap > 0.06f) continue;
                    int a = balls[i].GetEntityId().GetHashCode();
                    int b = balls[j].GetEntityId().GetHashCode();
                    pairs.Add(a < b ? $"{a}-{b}" : $"{b}-{a}");
                }
            return pairs;
        }

        /// <summary>
        /// THE payoff, in the only form that is actually guaranteed. An orphan is stranded by WHO IT
        /// TOUCHES, so what the rescue must do is re-deal the contact graph; whether a given pile then
        /// happens to bring two equal tiers together is physics, and measuring it directly gives a test
        /// that passes or fails on luck. This asserts the mechanism instead, over the full allowance,
        /// which is how a cornered player spends it.
        /// <para>One tilt is not enough — the funnel V sorts by size and a single shake largely
        /// re-forms the same pile. That is a real property of the mechanic, not a test artefact.</para>
        /// <para>Deliberately not asserted on three balls in the V with the small one at the bottom:
        /// that is a stable equilibrium, it re-forms after any shake, and a rescue that claimed to fix
        /// it would promise something the physics will not deliver.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ShakingACrowdedPileReDealsIt()
        {
            yield return LoadMain();
            var boot = Boot();
            yield return SettledPile(boot);
            Assume.That(boot.Board.BallCount, Is.GreaterThan(3), "the pile merged itself away before the tilt");

            var before = Contacts(boot);
            Assume.That(before.Count, Is.GreaterThan(0), "nothing was touching, so there was no lock to break");
            int mergedAway = 0;
            boot.Board.Merged += (_, __) => mergedAway++;

            for (int i = 0; i < boot.ZoneA.TiltCharges; i++)
            {
                boot.ZoneA.Tilt();
                yield return new WaitForSeconds(boot.feel.tiltCooldownMs / 1000f + 1.6f);
            }
            float deadline = Time.time + 6f;
            while (!boot.Board.IsSettled() && Time.time < deadline) yield return null;

            var after = Contacts(boot);
            var kept = new HashSet<string>(before);
            kept.IntersectWith(after);
            Assert.Less(kept.Count, before.Count,
                $"all {before.Count} contacts survived {boot.ZoneA.TiltCharges} tilts ({mergedAway} merges) — "
                + "the pile was slid, not re-dealt, so no orphan could ever find a partner");
        }

        [UnityTest]
        public IEnumerator TheAllowanceRunsOutAndIsNeverRefilled()
        {
            yield return LoadMain();
            var boot = Boot();
            boot.DebugDrop(0f, 1);
            yield return new WaitForSeconds(1.6f);

            int charges = boot.ZoneA.TiltCharges;
            var seen = new List<int>();
            GameEvents.TiltUsed += e => seen.Add(e.Remaining);

            // One more press than there are charges. The cooldown is what makes the wait necessary —
            // without it a fumbled double-tap would spend two.
            for (int i = 0; i < charges + 1; i++)
            {
                boot.ZoneA.Tilt();
                yield return new WaitForSeconds(boot.feel.tiltCooldownMs / 1000f + 0.35f);
            }

            Assert.AreEqual(charges, seen.Count, "the tilt spent more (or fewer) charges than the run was granted");
            Assert.AreEqual(0, boot.ZoneA.TiltsLeft);
            Assert.IsFalse(boot.ZoneA.CanTilt, "a spent allowance must stay spent");
            CollectionAssert.AreEqual(Enumerable.Range(0, charges).Reverse().ToArray(), seen,
                "each tilt should report one fewer remaining");
        }

        [UnityTest]
        public IEnumerator ADoubleTapCannotSpendTwoCharges()
        {
            yield return LoadMain();
            var boot = Boot();
            boot.DebugDrop(0f, 1);
            yield return new WaitForSeconds(1.6f);

            int before = boot.ZoneA.TiltsLeft;
            boot.ZoneA.Tilt();
            boot.ZoneA.Tilt();
            yield return null;

            Assert.AreEqual(before - 1, boot.ZoneA.TiltsLeft, "the cooldown did not swallow the second press");
        }

        [UnityTest]
        public IEnumerator AnEmptyBoardCannotBeShaken()
        {
            yield return LoadMain();
            var boot = Boot();
            Assume.That(boot.Board.BallCount, Is.EqualTo(0));

            Assert.IsFalse(boot.ZoneA.CanTilt, "there is nothing to shake");
            Assert.IsFalse(boot.ZoneA.Tilt(), "a refused tilt must not report success");
            Assert.AreEqual(boot.ZoneA.TiltCharges, boot.ZoneA.TiltsLeft, "a refused tilt must not cost a charge");
        }

        /// <summary>
        /// Phase B pans the camera away from Zone A entirely: a shake the player cannot see is a
        /// charge spent for nothing. Same reasoning as <c>ZoneCSystem.IsArmed</c>'s phase lock.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBoardCannotBeShakenFromPhaseB()
        {
            yield return LoadMain();
            var boot = Boot();
            boot.DebugDrop(0f, 1);
            yield return new WaitForSeconds(1.6f);
            Assume.That(boot.ZoneA.CanTilt, Is.True);

            GameEvents.RaisePhaseChanged(GamePhase.B);
            yield return null;

            Assert.IsFalse(boot.ZoneA.CanTilt);
            Assert.IsFalse(boot.ZoneA.Tilt());
            Assert.AreEqual(boot.ZoneA.TiltCharges, boot.ZoneA.TiltsLeft);
        }

        [UnityTest]
        public IEnumerator AModalFreezesTheTiltToo()
        {
            yield return LoadMain();
            var boot = Boot();
            boot.DebugDrop(0f, 1);
            yield return new WaitForSeconds(1.6f);
            Assume.That(boot.ZoneA.CanTilt, Is.True);

            GameEvents.RaiseModalOpen(true);
            yield return null;
            Assert.IsFalse(boot.ZoneA.CanTilt, "the quit confirm must freeze the tilt like everything else");

            GameEvents.RaiseModalOpen(false);
            yield return null;
            Assert.IsTrue(boot.ZoneA.CanTilt);
        }

        /// <summary>
        /// The gamble, asserted as a HAZARD rather than a death: overflow needs a full second of REST
        /// above the line, which is timing-dependent, but the loft itself is not. A tilt that could
        /// never put a ball into the danger band would be a free button.
        /// </summary>
        [UnityTest]
        public IEnumerator ATiltCanLoftABallIntoTheDangerBand()
        {
            yield return LoadMain();
            var boot = Boot();
            for (int i = 0; i < 6; i++)
            {
                boot.DebugDrop(-1.5f + i * 0.6f, 1);
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(2.0f);

            double highestBefore = HighestBall(boot);
            boot.ZoneA.Tilt();

            double highestDuring = highestBefore;
            float until = Time.time + 1.2f;
            while (Time.time < until)
            {
                highestDuring = System.Math.Min(highestDuring, HighestBall(boot));
                yield return null;
            }

            Assert.Less(highestDuring, highestBefore - 1.0,
                $"the tilt barely moved the stack ({highestBefore:0.#} → {highestDuring:0.#} px from the top); with no real loft it is not a gamble");
        }

        /// <summary>Distance from the band top to the topmost ball, in design px — smaller is closer to death.</summary>
        static double HighestBall(GameBootstrap boot)
        {
            double best = double.MaxValue;
            foreach (var ball in boot.Board.Balls)
                best = System.Math.Min(best, boot.Geometry.HeightFromTopPx(ball.Position.y));
            return best == double.MaxValue ? 0 : best;
        }
    }
}
