using System;
using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// ONE live run — the zones, the directors and the board. <c>GameBootstrap</c> keeps composition,
    /// app state and persistence. Splitting these is what lets a title screen exist at all: the
    /// bootstrap can be alive with no session behind it.
    /// <para>Also owns the save CHECKPOINT. Quiescence going false→true is edge-triggered here, once
    /// per turn, and announced through <see cref="CheckpointReady"/> — the only moment a snapshot is
    /// meaningful, since every other state carries tween-bound data that cannot be serialised.</para>
    /// <para>A session is created once per scene load and never torn down: RESTART and MENU both
    /// reload the scene, which is the honest reset for pooled balls, tweens and the theme.</para>
    /// </summary>
    public sealed class GameSession
    {
        public Board Board { get; }
        public ZoneASystem ZoneA { get; }
        public ZoneBSystem ZoneB { get; }
        public ZoneCSystem ZoneC { get; }
        public PhaseDirector Phases { get; }
        public ThemeDirector Themes { get; }
        public AimController Aim => aim;
        public BallQueue Queue => queue;
        public float ArenaScale => factory.ArenaScale;

        /// <summary>The run reached rest — the caller snapshots and persists.</summary>
        public event Action CheckpointReady;

        readonly AimController aim;
        readonly BallFactory factory;
        readonly BallQueue queue;
        readonly BoardGeometry geometry;
        /// <summary>Starts true so the initial settled board is not mistaken for a fresh edge.</summary>
        bool wasQuiescent = true;

        public GameSession(Transform owner, Camera cam, CameraRig rig, BoardGeometry geometry,
            TierLadder ladder, ProgressionCurve curve, GameFeelSO feel, ZoneBGenParams genParams,
            IReadOnlyList<double> doorColumns, int zoneBSeed, RunSnapshot restore)
        {
            this.geometry = geometry;

            var world = new GameObject("ZoneA").transform;
            var arena = new ArenaBuilder(world, geometry, feel);
            arena.Build();

            factory = new BallFactory(world, ladder, feel);
            Board = new Board(factory, geometry, feel);
            var deathLine = new DeathLineView(world, geometry);
            queue = new BallQueue();
            aim = new AimController(world, cam, geometry, factory, feel, queue, () => Time.unscaledTime * 1000.0);
            var mergeFx = new MergeFx(world, feel, geometry);
            var highlight = new DropHighlight(world);
            var growth = new ArenaGrowth(factory, Board, feel);

            // A restored run resumes mid-progression, so Zone B's opening bar target is that level's,
            // not level 1's.
            int startLevel = restore != null ? restore.level : 1;
            ZoneA = new ZoneASystem(Board, aim, deathLine, queue, curve, ladder, feel, mergeFx, factory, highlight, growth, geometry, world);
            ZoneB = new ZoneBSystem(owner, geometry, feel, cam, genParams, doorColumns, zoneBSeed, curve.ScoreBarTargetForLevel(startLevel));
            ZoneC = new ZoneCSystem(owner, Board, geometry, feel);
            Phases = new PhaseDirector(rig, feel);
            Themes = new ThemeDirector(curve, feel);

            // The camera answers a tilt, but Zone A must not know a camera exists — so the shake hangs
            // off the seam here, next to the other composition wiring.
            GameEvents.TiltUsed += _ => rig.Shake(feel.tiltCameraShakePx, feel.tiltMs);
        }

        /// <summary>
        /// Seed a restored run, then announce. THE ORDER IS LOAD-BEARING and both mistakes are silent —
        /// see the two comments below.
        /// </summary>
        public void Begin(RunSnapshot restore)
        {
            if (restore != null)
            {
                // BEFORE the balls: RadiusForTier divides by ArenaScale, so restoring balls first
                // spawns every one of them at the wrong size.
                factory.SetArenaScale(restore.arenaScale);
                ZoneA.Restore(restore.level, restore.ballBuffer, restore.score);
                queue.Seed(new[] { restore.currentTier, restore.nextTier });
                foreach (var b in restore.board)
                    Board.Restore(geometry.DesignXToWorld(b.x),
                                  geometry.CeilingY - BoardGeometry.Units(b.yFromTop),
                                  b.tier);
                // Zone B owns the lifetime total and re-announces it, which is what re-syncs Zone A's
                // mirror — so this must come after ZoneA.Restore, not before.
                ZoneB.Restore(restore.score, restore.barFilled, restore.barTarget,
                              restore.zbStructureSeed, restore.zbDressingSeed);
                ZoneA.RestoreTilts(restore.tiltsLeft);
            }

            ZoneA.Start();
            // AFTER ZoneA.Start(): that is what raises ProgressionChanged, which sets the director's
            // target. Snapping before it would simply be overwritten.
            if (restore != null) Themes.SnapTo(restore.level);
            Phases.Start();
            wasQuiescent = true;
        }

        public void Tick(float deltaMs)
        {
            aim.Tick();
            ZoneA.Tick(deltaMs);
            ZoneC.Tick(deltaMs);
            ZoneB.Tick(deltaMs);
            TrackCheckpoint();
        }

        public void FixedTick()
        {
            Board.FixedTick();
            ZoneB.FixedTick();
        }

        /// <summary>Edge-triggered: fire once when the run SETTLES, not every frame it stays settled.</summary>
        void TrackCheckpoint()
        {
            bool now = ZoneA.IsQuiescent;
            if (now && !wasQuiescent) CheckpointReady?.Invoke();
            wasQuiescent = now;
        }

        /// <summary>
        /// The durable half of the run, in design px — exactly what <see cref="Begin"/> needs and
        /// nothing more. No velocities, no phase.
        /// </summary>
        public RunSnapshot Capture()
        {
            var snap = new RunSnapshot
            {
                level = ZoneA.Level,
                // Zone B is the authority on the lifetime total; ZoneA.Score is only its mirror.
                score = ZoneB.Total,
                ballBuffer = ZoneA.BallBuffer,
                arenaScale = factory.ArenaScale,
                currentTier = queue.CurrentTier,
                nextTier = queue.NextTier,
                barFilled = ZoneB.BarFilled,
                barTarget = ZoneB.BarTarget,
                zbStructureSeed = ZoneB.StructureSeed,
                zbDressingSeed = ZoneB.DressingSeed,
                tiltsLeft = ZoneA.TiltsLeft,
            };
            foreach (var ball in Board.Balls)
                snap.board.Add(new BallSpawn
                {
                    tier = ball.Tier,
                    x = geometry.WorldXToDesign(ball.Position.x),
                    yFromTop = geometry.HeightFromTopPx(ball.Position.y),
                });
            return snap;
        }

        /// <summary>Test/debug: drop a tier at a world x through the real system (spends the buffer).</summary>
        public void DebugDrop(float x, int tier) => ZoneA.DebugDrop(x, tier);
    }
}
