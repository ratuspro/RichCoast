using System.Collections.Generic;
using PrimeTween;
using RichCoast.Core;
using TMPro;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone B — the ball-split multiplier arena. Lays a freshly GENERATED shelf cascade
    /// (<see cref="ZoneBGenerator"/>) in world space under Zone C and RESHUFFLES it every time the
    /// arena drains empty, so no two drops play the same board. Spawns a small pooled ball per
    /// <c>BallDropped</c>, splits balls that touch gates into fanned copies (with a short gate-grace so
    /// copies don't re-trigger), drains balls that reach a collector into the running total + the score
    /// bar, and reports its flight state (<c>ZoneBBusy</c>/<c>ZoneBEmpty</c>). One gate per arena is
    /// GILDED — the payoff of the golden chute — and pays ×6–×8 with a fanfare and a
    /// <c>GoldenGateHit</c>. Owns scoring: every level crossing is a live <c>ScoreBarFilled</c>; once
    /// the arena drains with a level owed, it waits for the bar's wraps and a settle beat, banks the
    /// round's haul (<c>ScoreHarvested</c>) and fires <c>ScoreBarCashedIn</c>. Talks to the rest of the
    /// game ONLY through <see cref="GameEvents"/>.
    /// </summary>
    public sealed class ZoneBSystem
    {
        public const int BallLayer = 10;
        /// <summary>Fresh split copies live here for <c>splitGraceMs</c>: it never collides with <see cref="GateLayer"/>.</summary>
        public const int GraceLayer = 11;
        public const int GateLayer = 12;

        const int MaxLevelsPerCashIn = 10;
        /// <summary>
        /// A reshuffle waits this long after the last arrival. Zone A's milestone drain raises several
        /// <c>BallDropped</c> in one tween batch, and an early ball can drain before the last one lands
        /// — rebuilding in that gap would yank the arena out from under the rest of the batch.
        /// </summary>
        const float RebuildGuardMs = 250f;
        const float MouthGlowPulseMs = 1400f;
        const float MouthGlowAlphaMin = 0.10f, MouthGlowAlphaMax = 0.30f;
        const float HaulMaxScale = 1.9f, HaulGrowth = 0.13f, HaulPopS = 0.18f;
        const float ContainmentPx = 40f;

        readonly Transform root;
        readonly BoardGeometry geometry;
        readonly GameFeelSO feel;
        readonly Camera camera;
        readonly ZoneBGenParams genParams;
        readonly IReadOnlyList<double> entryXs;
        readonly System.Random rebuildRng;
        readonly ScoreBar scoreBar;

        /// <summary>The playfield in play — walls, gates, collectors. Replaced wholesale on a reshuffle.</summary>
        Transform arena;
        ZoneBLayout layout;
        SpriteRenderer mouthGlow;
        float mouthGlowMs;

        readonly HashSet<ZoneBBall> balls = new HashSet<ZoneBBall>();
        readonly Stack<ZoneBBall> pool = new Stack<ZoneBBall>();
        readonly List<(ZoneBBall ball, int multiplier, bool golden)> pendingSplits = new List<(ZoneBBall, int, bool)>();
        readonly List<(ZoneBBall ball, int multiplier)> pendingDrains = new List<(ZoneBBall, int)>();
        readonly List<(ZoneBGate gate, GateDef def, Rigidbody2D body)> movingGates = new List<(ZoneBGate, GateDef, Rigidbody2D)>();
        readonly Dictionary<int, PhysicsMaterial2D> ballMaterials = new Dictionary<int, PhysicsMaterial2D>();
        PhysicsMaterial2D wallMaterial, gateMaterial;
        float gateElapsedMs;

        int inFlight;
        /// <summary>Since the last <c>BallDropped</c> arrived — gates the reshuffle (see <see cref="RebuildGuardMs"/>).</summary>
        float sinceLastDropMs = float.MaxValue;
        double total;
        bool pendingCashIn;
        int pendingWraps;
        int cycleLevels;
        bool resolveArmed;
        float resolveDwellMs;
        double roundScore;
        float displayFraction;
        /// <summary>
        /// What the shown fill is doing when no wrap is owed: easing to the live fill, holding full (the
        /// round's final wrap, through the settle dwell), or draining back down to empty.
        /// </summary>
        enum BarMode { Live, Hold, Drain }
        BarMode barMode = BarMode.Live;
        float barHoldMs, barDrainMs, barDrainTotalMs;

        // Score bar + haul label visuals.
        SpriteRenderer barGroove, barFill;
        TextMeshPro barLabel, haulLabel;
        float barFillLeft, barFillWidth, barMidY, barFillHeight;
        Vector3 grooveBaseScale;
        Tween haulPop, grooveThrob, labelThrob;

        public int InFlight => inFlight;
        public double Total => total;
        public int BallCount => balls.Count;
        public string LayoutName => layout.Name;
        /// <summary>Seed of the arena in play. Changes on every reshuffle.</summary>
        public int Seed => layout.Seed;
        public ZoneBLayout Layout => layout;
        /// <summary>The design-space column a drop must hit to take the golden path.</summary>
        public double GoldenMouthX => layout.Golden.MouthX;
        public int GoldenMultiplier => layout.Golden.Multiplier;

        /// <summary>
        /// <paramref name="entryXs"/> are the columns a ball can be dropped down — the trap-door's
        /// sweep positions. The generator centres the golden mouth on one of them so a well-timed tap
        /// is rewarded; pass null and the mouth lands anywhere, which is what a continuous aim wants.
        /// </summary>
        public ZoneBSystem(Transform parent, BoardGeometry geometry, GameFeelSO feel, Camera camera,
            ZoneBGenParams genParams, IReadOnlyList<double> entryXs, int seed, double initialTarget)
        {
            this.geometry = geometry;
            this.feel = feel;
            this.camera = camera;
            this.genParams = genParams ?? new ZoneBGenParams();
            this.entryXs = entryXs;
            rebuildRng = new System.Random(seed);
            scoreBar = new ScoreBar(initialTarget);
            root = new GameObject("ZoneB").transform;
            root.SetParent(parent, false);

            wallMaterial = new PhysicsMaterial2D("zoneB-wall") { friction = 0.1f, bounciness = 0.3f };
            gateMaterial = new PhysicsMaterial2D("zoneB-gate") { friction = 0f, bounciness = feel.gateBounce };

            // The backdrop, the containment box and the score bar outlive every reshuffle; only the
            // playfield under `arena` is torn down and rebuilt.
            BuildBackdrop();
            BuildContainment();
            BuildScoreBar();
            EmitScoreBar();
            BuildArena(ZoneBGenerator.Generate(rebuildRng.Next(), this.genParams, entryXs), popIn: false);

            GameEvents.ProgressionChanged += e =>
            {
                scoreBar.SetTarget(e.ScoreBarTarget);
                EmitScoreBar();
            };
            GameEvents.BallDropped += e =>
            {
                sinceLastDropMs = 0f;
                Spawn(new Vector2(geometry.DesignXToWorld(e.X), geometry.ZoneBTopY - geometry.ZoneBBallRadius), e.Ball.Tier, e.Ball.Value, Vector2.zero, false);
                OnBallSpawned();
            };
        }

        // --- Arena lifecycle ----------------------------------------------------------------------

        /// <summary>
        /// Lay a fresh playfield. Safe only with nothing in flight: the old colliders are destroyed at
        /// the end of the frame, and the earliest a ball can exist afterwards is a door tap plus the
        /// suck and pop (≥ 260 ms), so nothing ever touches a ghost.
        /// </summary>
        public void Rebuild(int seed) => BuildArena(ZoneBGenerator.Generate(seed, genParams, entryXs), popIn: true);

        /// <summary>Test/debug hook: lay one specific arena so a run is reproducible.</summary>
        public void DebugRebuild(int seed) => Rebuild(seed);

        /// <summary>
        /// Where every live ball is, in Zone B DESIGN space (x across, y down from the band top) — the
        /// same frame the generator authors in. A stuck ball is a generator bug, and this says which
        /// pocket to go look at.
        /// </summary>
        public string DescribeBalls()
        {
            if (balls.Count == 0) return "no balls";
            var sb = new System.Text.StringBuilder();
            foreach (var ball in balls)
            {
                var at = ball.Position;
                double x = geometry.WorldXToDesign(at.x);
                double y = DesignSpace.ToPixels(geometry.ZoneBTopY - at.y);
                sb.Append($"[t{ball.Tier} at ({x:0.#},{y:0.#}) speed {ball.Speed:0.00} slow {ball.SlowMs:0}ms] ");
            }
            return sb.ToString();
        }

        void BuildArena(ZoneBLayout next, bool popIn)
        {
            if (arena != null) Object.Destroy(arena.gameObject);
            movingGates.Clear();
            gateElapsedMs = 0f;
            mouthGlow = null;
            layout = next;

            arena = new GameObject("Arena").transform;
            arena.SetParent(root, false);
            foreach (var wall in layout.Walls) BuildWall(wall);
            foreach (var gate in layout.Gates) BuildGate(gate);
            foreach (var collector in layout.Collectors) BuildCollector(collector);
            if (layout.Golden != null) BuildMouthGlow(layout.Golden);
            if (popIn) PopIn();
        }

        /// <summary>
        /// Stagger the fresh playfield in so a reshuffle reads as a beat, not a jump cut. Each piece
        /// grows from its own centre, so the board looks like it assembles itself. The top of Zone B is
        /// on screen even during phase A, so this is always seen.
        /// </summary>
        void PopIn()
        {
            float durationS = feel.arenaPopInMs / 1000f;
            if (durationS <= 0f) return;
            int index = 0;
            foreach (Transform piece in arena)
            {
                var from = piece.localScale * 0.7f;
                var to = piece.localScale;
                piece.localScale = from;
                Tween.Scale(piece, to, durationS, Ease.OutBack, startDelay: index * 0.012f);
                index++;
            }
        }

        /// <summary>A slow brass breath under the golden mouth — enough to say "aim here" without shouting.</summary>
        void BuildMouthGlow(GoldenPath golden)
        {
            float d = BoardGeometry.Units((float)golden.MouthWidth * 2.6f);
            var go = new GameObject("GoldenMouth");
            go.transform.SetParent(arena, false);
            go.transform.localPosition = geometry.ZoneBToWorld(golden.MouthX, golden.MouthY);
            go.transform.localScale = new Vector3(d, d, 1f);
            mouthGlow = go.AddComponent<SpriteRenderer>();
            mouthGlow.sprite = BallArt.SoftDot;
            mouthGlow.sortingOrder = 1; // under the rails, so it reads as a glow behind the gateway
            Themed.Bind(mouthGlow, ThemeKey.BrassBright);
            mouthGlowMs = 0f;
        }

        // --- Per-frame ---------------------------------------------------------------------------

        public void Tick(float deltaMs)
        {
            ResolveSplits();
            ResolveDrains();
            AdvanceTimers(deltaMs);
            BreatheMouthGlow(deltaMs);
            AnimateBar(deltaMs);
            UpdateResolve(deltaMs);
        }

        /// <summary>Driven per frame rather than by a looping tween, so a reshuffle can never orphan one.</summary>
        void BreatheMouthGlow(float deltaMs)
        {
            if (mouthGlow == null) return;
            mouthGlowMs += deltaMs;
            float breathe = 0.5f + 0.5f * Mathf.Sin(mouthGlowMs * 2f * Mathf.PI / MouthGlowPulseMs);
            var c = mouthGlow.color;
            mouthGlow.color = new Color(c.r, c.g, c.b, Mathf.Lerp(MouthGlowAlphaMin, MouthGlowAlphaMax, breathe));
        }

        /// <summary>Per physics step: moving gates + the anti-tunnel speed cap.</summary>
        public void FixedTick()
        {
            gateElapsedMs += Time.fixedDeltaTime * 1000f;
            foreach (var (gate, def, body) in movingGates)
            {
                var pose = def.PoseAt(gateElapsedMs);
                body.MovePosition(geometry.ZoneBToWorld(pose.x, pose.y));
                body.MoveRotation(-(float)pose.angle * Mathf.Rad2Deg);
            }
            float cap = feel.zoneBMaxSpeed;
            float capSq = cap * cap;
            foreach (var ball in balls)
            {
                var v = ball.Body.linearVelocity;
                if (v.sqrMagnitude > capSq) ball.Body.linearVelocity = v.normalized * cap;
            }
        }

        // --- Contact reports (from ZoneBBall) ---------------------------------------------------

        public void ReportSplit(ZoneBBall ball, int multiplier, bool golden = false)
        {
            if (!balls.Contains(ball) || ball.Claimed || ball.GraceMs > 0f) return;
            ball.Claimed = true;
            pendingSplits.Add((ball, multiplier, golden));
        }

        public void ReportDrain(ZoneBBall ball, int scoreMultiplier)
        {
            if (!balls.Contains(ball) || ball.Claimed) return;
            ball.Claimed = true;
            pendingDrains.Add((ball, scoreMultiplier));
        }

        void ResolveSplits()
        {
            if (pendingSplits.Count == 0) return;
            var batch = new List<(ZoneBBall ball, int multiplier, bool golden)>(pendingSplits);
            pendingSplits.Clear();
            foreach (var (ball, multiplier, golden) in batch)
            {
                if (!balls.Contains(ball)) continue;
                // Safety cap: a runaway cascade stops multiplying — the ball just bounces on.
                if (balls.Count + multiplier - 1 > feel.maxBallsInFlight)
                {
                    ball.Claimed = false;
                    continue;
                }
                var at = ball.Position;
                int tier = ball.Tier;
                double value = ball.Value;
                Despawn(ball);
                inFlight += multiplier - 1;

                if (golden) CelebrateGolden(at, multiplier);
                else if (multiplier > 1) Sfx.Instance?.Multiply(multiplier);

                // The gilded gate throws two to four times as many copies as an ordinary one, so it fans
                // them wider and alternates two spawn radii — eight balls on a single arc would appear
                // interpenetrating and blow apart instead of bursting.
                float spread = golden ? feel.goldenSplitSpread : feel.splitSpread;
                float offset = geometry.ZoneBBallRadius * (golden ? feel.goldenSplitOffsetMult : 1.8f);
                float stagger = golden ? geometry.ZoneBBallRadius * 1.2f : 0f;
                float kick = feel.splitKick * (golden ? feel.goldenSplitKickMult : 1f);
                for (int i = 0; i < multiplier; i++)
                {
                    float fan = (float)DoorMath.SplitFanAngle(i, multiplier, spread);
                    // Fan opens DOWNWARD (y-up world): straight down at fan 0, sideways at the edges.
                    var dir = new Vector2(Mathf.Sin(fan), -Mathf.Cos(fan));
                    Spawn(at + dir * (offset + ((i & 1) == 1 ? stagger : 0f)), tier, value, dir * kick, true);
                }
            }
        }

        void ResolveDrains()
        {
            if (pendingDrains.Count == 0) return;
            var batch = new List<(ZoneBBall ball, int multiplier)>(pendingDrains);
            pendingDrains.Clear();
            foreach (var (ball, multiplier) in batch)
            {
                if (!balls.Contains(ball)) continue;
                double value = ball.Value;
                Despawn(ball);
                AddScore(value * multiplier);
                Sfx.Instance?.Collect(value);
                OnBallDrained();
            }
        }

        /// <summary>Gate-grace countdown (relayer when it expires) and the stuck watchdog.</summary>
        void AdvanceTimers(float deltaMs)
        {
            sinceLastDropMs = Mathf.Min(sinceLastDropMs + deltaMs, 1e9f);
            float restSpeed = feel.restSpeed;
            foreach (var ball in balls)
            {
                if (ball.GraceMs > 0f)
                {
                    ball.GraceMs -= deltaMs;
                    if (ball.GraceMs <= 0f) ball.gameObject.layer = BallLayer;
                }
                ball.SlowMs = ball.Speed < restSpeed ? ball.SlowMs + deltaMs : 0f;
                if (ball.SlowMs >= feel.stuckNudgeMs)
                {
                    ball.SlowMs = 0f;
                    ball.Body.WakeUp();
                    ball.Body.linearVelocity += new Vector2(Random.Range(-1f, 1f), 1.5f) * feel.splitKick * 0.5f;
                }
            }
        }

        // --- Balls -------------------------------------------------------------------------------

        void Spawn(Vector2 position, int tier, double value, Vector2 velocity, bool fromSplit)
        {
            ZoneBBall ball = pool.Count > 0 ? pool.Pop() : Build();
            var go = ball.gameObject;
            go.name = $"ZoneB Ball t{tier}";
            go.layer = fromSplit ? GraceLayer : BallLayer;
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(true);
            ball.Configure(this, tier, value, geometry.ZoneBBallRadius);
            ball.GraceMs = fromSplit ? feel.splitGraceMs : 0f;

            var body = ball.Body;
            body.gravityScale = feel.zoneBGravityScale;
            body.linearDamping = feel.zoneBLinearDamping;
            body.angularDamping = 0.5f;
            body.linearVelocity = velocity;
            body.angularVelocity = 0f;
            body.WakeUp();
            ball.Collider.sharedMaterial = MaterialForTier(tier);
            balls.Add(ball);
        }

        void Despawn(ZoneBBall ball)
        {
            balls.Remove(ball);
            ball.Body.linearVelocity = Vector2.zero;
            ball.gameObject.SetActive(false);
            pool.Push(ball);
        }

        ZoneBBall Build()
        {
            var go = new GameObject("ZoneB Ball");
            go.transform.SetParent(root, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.StartAwake;
            go.AddComponent<CircleCollider2D>();
            var face = new GameObject("Face");
            face.transform.SetParent(go.transform, false);
            var sr = face.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            var ball = go.AddComponent<ZoneBBall>();
            go.SetActive(false);
            return ball;
        }

        /// <summary>Material feel: the shared per-tier multipliers on Zone B's own constants; restitution capped so exotic tiers don't ping-pong the cascade.</summary>
        PhysicsMaterial2D MaterialForTier(int tier)
        {
            if (ballMaterials.TryGetValue(tier, out var m)) return m;
            var physics = Materials.ForTier(tier).Def.Physics;
            m = new PhysicsMaterial2D($"zoneB-ball-t{tier}")
            {
                bounciness = Mathf.Min(0.5f, feel.zoneBBounce * (float)physics.RestitutionMult),
                friction = feel.zoneBFriction * (float)physics.FrictionMult,
            };
            ballMaterials[tier] = m;
            return m;
        }

        // --- Contract plumbing -------------------------------------------------------------------

        void OnBallSpawned()
        {
            resolveArmed = false; // a fresh ball is in flight — not resolving a cash-in
            inFlight += 1;
            if (inFlight == 1) GameEvents.RaiseZoneBBusy();
        }

        void OnBallDrained()
        {
            if (inFlight == 0) return;
            inFlight -= 1;
            if (inFlight != 0) return;
            if (pendingCashIn)
            {
                // A level was crossed this cycle: DON'T report empty yet. Keeping Zone B "busy" leaves
                // the trap-door locked while UpdateResolve waits for the bar's wraps, dwells, then pans up.
                resolveArmed = true;
                resolveDwellMs = feel.settleDwellMs;
            }
            else
            {
                ReleaseArena();
            }
        }

        /// <summary>
        /// Hand Zone B back to the trap-door — laying a fresh playfield FIRST, so the door re-arms onto
        /// the arena the player is about to play and the new golden mouth is visible while they aim.
        /// </summary>
        void ReleaseArena()
        {
            if (inFlight == 0 && balls.Count == 0 && sinceLastDropMs >= RebuildGuardMs) Rebuild(rebuildRng.Next());
            GameEvents.RaiseZoneBEmpty();
        }

        /// <summary>
        /// Score a drained ball and wrap the bar LIVE through every target this crossed. Each crossing
        /// is a real level-up: <c>ScoreBarFilled</c> bumps Zone A's level and — synchronously, via its
        /// <c>ProgressionChanged</c> → SetTarget — raises our target, so the next crossing is measured
        /// against the new one. Every crossing also queues one display wrap.
        /// </summary>
        void AddScore(double points)
        {
            total += points;
            GameEvents.RaiseScoreChanged(total);

            roundScore += points;
            PumpHaulLabel();

            scoreBar.Add(points);
            while (scoreBar.CrossedTarget)
            {
                if (cycleLevels >= MaxLevelsPerCashIn)
                {
                    scoreBar.ForfeitOverflow();
                    break;
                }
                scoreBar.ConsumeLevel();
                cycleLevels += 1;
                GameEvents.RaiseScoreBarFilled();
                pendingWraps += 1;
                pendingCashIn = true;
            }
            EmitScoreBar();
        }

        /// <summary>
        /// Once drained with a cash-in owed: let the bar finish wrapping and draining, dwell (unless the
        /// final wrap's hold already was the dwell), then release Zone B and fire the pan-up trigger.
        /// </summary>
        void UpdateResolve(float deltaMs)
        {
            if (!resolveArmed) return;
            if (pendingWraps > 0 || barMode != BarMode.Live) return;
            resolveDwellMs -= deltaMs;
            if (resolveDwellMs > 0f) return;
            resolveArmed = false;
            pendingCashIn = false;
            cycleLevels = 0;
            HarvestRound();
            ReleaseArena();
            GameEvents.RaiseScoreBarCashedIn();
        }

        /// <summary>Bank this round's haul: fly its number up to the HUD from the haul label's on-screen spot, then reset.</summary>
        void HarvestRound()
        {
            if (roundScore > 0)
            {
                var screen = camera.WorldToScreenPoint(haulLabel.transform.position);
                // Clamp just inside the bottom edge for a cash-in that resolves in phase A (label off-screen below).
                GameEvents.RaiseScoreHarvested(new ScoreHarvestedEvent(roundScore, screen.x, Mathf.Max(screen.y, 5f)));
            }
            roundScore = 0;
            haulPop.Stop();
            haulLabel.gameObject.SetActive(false);
            haulLabel.transform.localScale = Vector3.one;
        }

        void PumpHaulLabel()
        {
            haulLabel.text = $"+{NumberFormat.Compact(roundScore)}";
            haulLabel.gameObject.SetActive(true);
            float baseScale = Mathf.Min(HaulMaxScale, 1f + Mathf.Log10((float)roundScore + 1f) * HaulGrowth);
            haulPop.Stop();
            haulLabel.transform.localScale = Vector3.one * (baseScale * 1.18f);
            haulPop = Tween.Scale(haulLabel.transform, baseScale, HaulPopS, Ease.OutBack);
        }

        void EmitScoreBar()
        {
            GameEvents.RaiseScoreBarChanged(scoreBar.Filled, scoreBar.Target);
            RenderBar();
        }

        // --- Arena build -------------------------------------------------------------------------

        void BuildBackdrop()
        {
            float x0 = geometry.ZoneBMinX - 0.5f, x1 = geometry.ZoneBMaxX + 0.5f;
            WorldArt.Rect(root, "Paper", x0, x1, geometry.ZoneBBottomY - 2f, geometry.ZoneBTopY, ThemeKey.Paper, -20);
        }

        /// <summary>Invisible left/right/bottom border so balls are always contained within the band.</summary>
        void BuildContainment()
        {
            float t = BoardGeometry.Units(ContainmentPx);
            float top = geometry.ZoneBTopY, bottom = geometry.ZoneBBottomY;
            float midY = (top + bottom) / 2f;
            float height = top - bottom + t;
            StaticBox("BoundLeft", new Vector2(geometry.ZoneBMinX - t / 2f, midY), new Vector2(t, height), 0f, wallMaterial, ArenaBuilder.WallLayer);
            StaticBox("BoundRight", new Vector2(geometry.ZoneBMaxX + t / 2f, midY), new Vector2(t, height), 0f, wallMaterial, ArenaBuilder.WallLayer);
            StaticBox("BoundBottom", new Vector2(0f, bottom - t / 2f), new Vector2(geometry.ZoneBMaxX - geometry.ZoneBMinX + 2f * t, t), 0f, wallMaterial, ArenaBuilder.WallLayer);
        }

        /// <summary>A pine guide rail: capsule collider (no flat top a ball could balance on) + light wood over a darker shadow edge.</summary>
        void BuildWall(WallDef wall)
        {
            var a = geometry.ZoneBToWorld(wall.X1, wall.Y1);
            var b = geometry.ZoneBToWorld(wall.X2, wall.Y2);
            float thickness = BoardGeometry.Units(wall.Thickness);
            var d = b - a;
            var go = new GameObject(wall.IsGolden ? "ChuteRail" : "Wall") { layer = ArenaBuilder.WallLayer };
            go.transform.SetParent(arena, false);
            go.transform.position = (a + b) / 2f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Horizontal;
            capsule.size = new Vector2(d.magnitude + thickness, thickness);
            capsule.sharedMaterial = wallMaterial;

            if (wall.FillBelow)
            {
                float bottom = geometry.ZoneBBottomY;
                WorldArt.Quad(arena, "RampFill", new[] { a, b, new Vector2(b.x, bottom), new Vector2(a.x, bottom) }, ThemeKey.Pine, 2);
            }
            // The golden chute is the same rail in a richer metal: brass over a darker brass edge.
            var edge = wall.IsGolden ? ThemeKey.Brass : ThemeKey.PineShadow;
            var face = wall.IsGolden ? ThemeKey.BrassBright : ThemeKey.Pine;
            WorldArt.Rail(arena, a, b, thickness + BoardGeometry.Units(2), edge, 3);
            WorldArt.Rail(arena, a, b, thickness, face, 4);
        }

        /// <summary>A painted wooden sign — green for high multipliers, brass for low — with a stencilled "X N" and a wood-shadow edge. Moving kinds ride a kinematic body.</summary>
        void BuildGate(GateDef def)
        {
            var pose = def.PoseAt(0);
            float length = BoardGeometry.Units(def.Length);
            float thickness = BoardGeometry.Units(ZoneBLayouts.GateThickness);
            var go = new GameObject($"{(def.IsGolden ? "GoldenGate" : "Gate")} x{def.Multiplier}") { layer = GateLayer };
            go.transform.SetParent(arena, false);
            go.transform.position = geometry.ZoneBToWorld(pose.x, pose.y);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -(float)pose.angle * Mathf.Rad2Deg);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = def.Kind == GateKind.Static ? RigidbodyType2D.Static : RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(length, thickness);
            box.sharedMaterial = gateMaterial;
            var gate = go.AddComponent<ZoneBGate>();
            gate.Multiplier = def.Multiplier;
            gate.IsGolden = def.IsGolden;
            if (def.Kind != GateKind.Static) movingGates.Add((gate, def, body));

            var paint = def.IsGolden ? ThemeKey.BrassBright : def.Multiplier >= 4 ? ThemeKey.GatePaint : ThemeKey.Brass;
            float edge = BoardGeometry.Units(2);
            WorldArt.Rect(go.transform, "Edge", Vector2.zero, new Vector2(length + 2f * edge, thickness + 2f * edge), ThemeKey.PineShadow, 4);
            WorldArt.Rect(go.transform, "Paint", Vector2.zero, new Vector2(length, thickness), paint, 5);
            WorldArt.Text(go.transform, "Label", $"X{def.Multiplier}", 15f, ThemeKey.Ink, 6);
        }

        /// <summary>An invisible sensor (the funnel ramps already read as the mouth); a scored collector labels its multiplier.</summary>
        void BuildCollector(CollectorDef def)
        {
            var centre = geometry.ZoneBToWorld(def.X + def.Width / 2, def.Y + def.Height / 2);
            var go = new GameObject("Collector") { layer = ArenaBuilder.WallLayer };
            go.transform.SetParent(arena, false);
            go.transform.position = centre;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(BoardGeometry.Units(def.Width), BoardGeometry.Units(def.Height));
            box.isTrigger = true;
            var collector = go.AddComponent<ZoneBCollector>();
            collector.ScoreMultiplier = def.ScoreMultiplier;
            if (def.ScoreMultiplier != 1) WorldArt.Text(go.transform, "Label", $"x{def.ScoreMultiplier}", 11f, ThemeKey.Ink, 5, FontStyles.Normal);
        }

        void StaticBox(string name, Vector2 centre, Vector2 size, float angleDeg, PhysicsMaterial2D material, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(root, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.sharedMaterial = material;
        }

        // --- Score bar visual --------------------------------------------------------------------

        void BuildScoreBar()
        {
            float barH = BoardGeometry.Units(ZoneBLayouts.BarHeight);
            float stroke = BoardGeometry.Units(2);
            float x0 = geometry.ZoneBMinX, x1 = geometry.ZoneBMaxX;
            float bottom = geometry.ZoneBBottomY;
            float barTop = bottom + barH;
            barMidY = bottom + barH / 2f;
            barFillLeft = x0 + stroke;
            barFillWidth = (x1 - x0) - 2f * stroke;
            barFillHeight = barH - 2f * stroke;

            WorldArt.Rect(root, "BarOutline", x0, x1, bottom, barTop, ThemeKey.Ink, 10);
            barGroove = WorldArt.Rect(root, "BarGroove", x0 + stroke, x1 - stroke, bottom + stroke, barTop - stroke, ThemeKey.Groove, 11);
            grooveBaseScale = barGroove.transform.localScale;
            barFill = WorldArt.Rect(root, "BarFill", new Vector2(barFillLeft, barMidY), new Vector2(0f, barFillHeight), ThemeKey.Brass, 12);
            barLabel = WorldArt.Text(root, "BarLabel", "", 11f, ThemeKey.Ink, 13);
            barLabel.transform.localPosition = new Vector3(0f, barMidY, 0f);

            // Hovering haul label — centred just above the bar, hidden until the round earns its first points.
            haulLabel = WorldArt.Text(root, "Haul", "", 18f, ThemeKey.BrassBright, 40);
            haulLabel.transform.localPosition = new Vector3(0f, barTop + BoardGeometry.Units(16f), 0f);
            haulLabel.outlineWidth = 0.25f;
            haulLabel.outlineColor = Theme.Ink;
            haulLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Drive the shown fill every frame. With owed wraps, sweep the bar up to full at a steady pace
        /// and celebrate; a roll-through snaps to empty between wraps so the whole roll stays fast. The
        /// LAST wrap never snaps: mid-round it drains back down over <c>wrapDrainMs</c>, and when it is
        /// the round's final wrap (arena drained, nothing more owed — the payout) it holds full through
        /// the settle dwell and then drains out over <c>barDrainMs</c>, the cash-in firing only once the
        /// bar is empty. With nothing owed and nothing draining, ease toward the current level's fill.
        /// </summary>
        void AnimateBar(float deltaMs)
        {
            if (pendingWraps > 0)
            {
                displayFraction += deltaMs / feel.wrapFillMs;
                if (displayFraction >= 1f)
                {
                    CelebrateFull();
                    pendingWraps -= 1;
                    if (pendingWraps > 0)
                    {
                        displayFraction = 0f; // more owed: snap and sweep again (the roll)
                    }
                    else
                    {
                        displayFraction = 1f;
                        bool finalWrap = resolveArmed;
                        barMode = finalWrap ? BarMode.Hold : BarMode.Drain;
                        barHoldMs = feel.settleDwellMs;
                        barDrainTotalMs = barDrainMs = finalWrap ? feel.barDrainMs : feel.wrapDrainMs;
                        if (finalWrap) resolveDwellMs = 0f; // the hold IS the dwell
                    }
                }
            }
            else if (barMode == BarMode.Hold)
            {
                displayFraction = 1f;
                barHoldMs -= deltaMs;
                if (barHoldMs <= 0f) barMode = BarMode.Drain;
            }
            else if (barMode == BarMode.Drain)
            {
                barDrainMs -= deltaMs;
                // Ease-in: the fill pours out slowly at first, then rushes to empty.
                float u = barDrainTotalMs > 0f ? 1f - Mathf.Clamp01(barDrainMs / barDrainTotalMs) : 1f;
                displayFraction = 1f - u * u;
                if (barDrainMs <= 0f)
                {
                    displayFraction = 0f;
                    barMode = BarMode.Live;
                }
            }
            else
            {
                float target = Mathf.Min(1f, (float)(scoreBar.Filled / scoreBar.Target));
                if (target < displayFraction - 1e-3f)
                {
                    displayFraction = target; // snap down (defensive; live fill only rises)
                }
                else
                {
                    float k = 1f - Mathf.Pow(1f - feel.barFillLerp, deltaMs / 16.67f);
                    displayFraction += (target - displayFraction) * k;
                    if (target - displayFraction < 0.005f) displayFraction = target;
                }
            }
            RenderBar();
        }

        void RenderBar()
        {
            if (barFill == null) return;
            float w = barFillWidth * displayFraction;
            barFill.transform.localScale = new Vector3(w, barFillHeight, 1f);
            barFill.transform.localPosition = new Vector3(barFillLeft + w / 2f, barMidY, 0f);
            double target = scoreBar.Target;
            barLabel.text = $"{NumberFormat.Compact(System.Math.Round(displayFraction * target))} / {NumberFormat.Compact(target)}";
        }

        /// <summary>A full bar throbs and throws off a rising brass sparkle — the "you filled it" beat.</summary>
        void CelebrateFull()
        {
            Sfx.Instance?.Goal();
            Haptics.Pulse(feel.heavyHapticMs, feel.heavyHapticAmp);

            // Vertical throb of the groove (centred on its own midline, so it puffs in place). Wraps
            // can arrive faster than a throb lasts, so restart from the BASE scale every time — a
            // throb that read the current (mid-throb) scale would compound 1.6× per wrap.
            grooveThrob.Stop();
            barGroove.transform.localScale = grooveBaseScale;
            grooveThrob = Tween.Scale(barGroove.transform, new Vector3(grooveBaseScale.x, grooveBaseScale.y * 1.6f, 1f), 0.13f, Ease.InOutSine, cycles: 2, CycleMode.Yoyo);
            labelThrob.Stop();
            barLabel.transform.localScale = Vector3.one;
            labelThrob = Tween.Scale(barLabel.transform, 1.3f, 0.13f, Ease.InOutSine, cycles: 2, CycleMode.Yoyo);

            // Brass sparkle rising off the full bar, spread along its whole length.
            float mid = (geometry.ZoneBMinX + geometry.ZoneBMaxX) / 2f;
            Sparkle(new Vector2(mid, barMidY), 16, (geometry.ZoneBMaxX - geometry.ZoneBMinX) / 2f - 0.15f, 13);
        }

        /// <summary>
        /// The rarest beat in the game: a ball threaded the golden mouth and struck the gilded gate.
        /// Fanfare, a heavy pulse, a gilded burst — and the event, for anyone else who wants to react.
        /// </summary>
        void CelebrateGolden(Vector2 at, int multiplier)
        {
            Sfx.Instance?.Golden();
            Haptics.Pulse(feel.heavyHapticMs, feel.heavyHapticAmp);
            Sparkle(root.InverseTransformPoint(at), 28, geometry.ZoneBBallRadius * 5f, 30);
            GameEvents.RaiseGoldenGateHit(multiplier);
        }

        /// <summary>
        /// A puff of brass motes rising and fading out. Shared by the full score bar (spread along its
        /// length) and the gilded gate (spread around the hit). Motes hang off <c>root</c>, not the
        /// arena, so a reshuffle mid-fade never destroys one in flight.
        /// </summary>
        void Sparkle(Vector2 centre, int count, float spreadX, int sortingOrder)
        {
            for (int i = 0; i < count; i++)
            {
                float size = BoardGeometry.Units(Random.Range(4f, 8f));
                var mote = new GameObject("Sparkle");
                mote.transform.SetParent(root, false);
                mote.transform.localPosition = new Vector3(centre.x + Random.Range(-spreadX, spreadX), centre.y, 0f);
                mote.transform.localScale = new Vector3(size, size, 1f);
                var sr = mote.AddComponent<SpriteRenderer>();
                sr.sprite = BallArt.SoftDot;
                sr.color = Theme.BrassBright;
                sr.sortingOrder = sortingOrder;
                float rise = BoardGeometry.Units(Random.Range(26f, 60f));
                float drift = BoardGeometry.Units(Random.Range(-14f, 14f));
                var from = mote.transform.localPosition;
                Tween.Custom(sr, 0f, 1f, Random.Range(0.42f, 0.72f), (m, t) =>
                {
                    m.transform.localPosition = from + new Vector3(drift * t, rise * t, 0f);
                    m.transform.localScale = Vector3.one * size * (1f - t);
                    m.color = new Color(m.color.r, m.color.g, m.color.b, 1f - t);
                }, Ease.OutQuad).OnComplete(sr, m => Object.Destroy(m.gameObject), warnIfTargetDestroyed: false);
            }
        }
    }
}
