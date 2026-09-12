using System.Collections.Generic;
using PrimeTween;
using RichCoast.Core;
using TMPro;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone B — the ball-split multiplier arena (port of <c>ZoneBSystem</c> + its gate / collector /
    /// wall sub-systems). Builds one of the two authored shelf-cascade layouts in world space under
    /// Zone C, spawns a small pooled ball per <c>BallDropped</c>, splits balls that touch gates into
    /// fanned copies (with a short gate-grace so copies don't re-trigger), drains balls that reach a
    /// collector into the running total + the score bar, and reports its flight state
    /// (<c>ZoneBBusy</c>/<c>ZoneBEmpty</c>). Owns scoring: every level crossing is a live
    /// <c>ScoreBarFilled</c>; once the arena drains with a level owed, it waits for the bar's wraps and
    /// a settle beat, banks the round's haul (<c>ScoreHarvested</c>) and fires <c>ScoreBarCashedIn</c>.
    /// Talks to the rest of the game ONLY through <see cref="GameEvents"/>.
    /// </summary>
    public sealed class ZoneBSystem
    {
        public const int BallLayer = 10;
        /// <summary>Fresh split copies live here for <c>splitGraceMs</c>: it never collides with <see cref="GateLayer"/>.</summary>
        public const int GraceLayer = 11;
        public const int GateLayer = 12;

        const int MaxLevelsPerCashIn = 10;
        const float HaulMaxScale = 1.9f, HaulGrowth = 0.13f, HaulPopS = 0.18f;
        const float ContainmentPx = 40f;

        readonly Transform root;
        readonly BoardGeometry geometry;
        readonly GameFeelSO feel;
        readonly Camera camera;
        readonly ZoneBLayout layout;
        readonly ScoreBar scoreBar;

        readonly HashSet<ZoneBBall> balls = new HashSet<ZoneBBall>();
        readonly Stack<ZoneBBall> pool = new Stack<ZoneBBall>();
        readonly List<(ZoneBBall ball, int multiplier)> pendingSplits = new List<(ZoneBBall, int)>();
        readonly List<(ZoneBBall ball, int multiplier)> pendingDrains = new List<(ZoneBBall, int)>();
        readonly List<(ZoneBGate gate, GateDef def, Rigidbody2D body)> movingGates = new List<(ZoneBGate, GateDef, Rigidbody2D)>();
        readonly Dictionary<int, PhysicsMaterial2D> ballMaterials = new Dictionary<int, PhysicsMaterial2D>();
        PhysicsMaterial2D wallMaterial, gateMaterial;
        float gateElapsedMs;

        int inFlight;
        double total;
        bool pendingCashIn;
        int pendingWraps;
        int cycleLevels;
        bool resolveArmed;
        float resolveDwellMs;
        double roundScore;
        float displayFraction;

        // Score bar + haul label visuals.
        SpriteRenderer barGroove, barFill;
        TextMeshPro barLabel, haulLabel;
        float barFillLeft, barFillWidth, barMidY, barFillHeight;
        Tween haulPop;

        public int InFlight => inFlight;
        public double Total => total;
        public int BallCount => balls.Count;
        public string LayoutName => layout.Name;

        public ZoneBSystem(Transform parent, BoardGeometry geometry, GameFeelSO feel, Camera camera, ZoneBLayout layout, double initialTarget)
        {
            this.geometry = geometry;
            this.feel = feel;
            this.camera = camera;
            this.layout = layout;
            scoreBar = new ScoreBar(initialTarget);
            root = new GameObject("ZoneB").transform;
            root.SetParent(parent, false);

            wallMaterial = new PhysicsMaterial2D("zoneB-wall") { friction = 0.1f, bounciness = 0.3f };
            gateMaterial = new PhysicsMaterial2D("zoneB-gate") { friction = 0f, bounciness = feel.gateBounce };

            BuildBackdrop();
            BuildContainment();
            foreach (var wall in layout.Walls) BuildWall(wall);
            foreach (var gate in layout.Gates) BuildGate(gate);
            foreach (var collector in layout.Collectors) BuildCollector(collector);
            BuildScoreBar();
            EmitScoreBar();

            GameEvents.ProgressionChanged += e =>
            {
                scoreBar.SetTarget(e.ScoreBarTarget);
                EmitScoreBar();
            };
            GameEvents.BallDropped += e =>
            {
                Spawn(new Vector2(geometry.DesignXToWorld(e.X), geometry.ZoneBTopY - geometry.ZoneBBallRadius), e.Ball.Tier, e.Ball.Value, Vector2.zero, false);
                OnBallSpawned();
            };
        }

        // --- Per-frame ---------------------------------------------------------------------------

        public void Tick(float deltaMs)
        {
            ResolveSplits();
            ResolveDrains();
            AdvanceTimers(deltaMs);
            AnimateBar(deltaMs);
            UpdateResolve(deltaMs);
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

        public void ReportSplit(ZoneBBall ball, int multiplier)
        {
            if (!balls.Contains(ball) || ball.Claimed || ball.GraceMs > 0f) return;
            ball.Claimed = true;
            pendingSplits.Add((ball, multiplier));
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
            var batch = new List<(ZoneBBall ball, int multiplier)>(pendingSplits);
            pendingSplits.Clear();
            foreach (var (ball, multiplier) in batch)
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
                if (multiplier > 1) Sfx.Instance?.Multiply(multiplier);

                float offset = geometry.ZoneBBallRadius * 1.8f;
                for (int i = 0; i < multiplier; i++)
                {
                    float fan = (float)DoorMath.SplitFanAngle(i, multiplier, feel.splitSpread);
                    // Fan opens DOWNWARD (y-up world): straight down at fan 0, sideways at the edges.
                    var dir = new Vector2(Mathf.Sin(fan), -Mathf.Cos(fan));
                    Spawn(at + dir * offset, tier, value, dir * feel.splitKick, true);
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
                GameEvents.RaiseZoneBEmpty();
            }
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

        /// <summary>Once drained with a cash-in owed: let the bar finish wrapping, dwell, then release Zone B and fire the pan-up trigger.</summary>
        void UpdateResolve(float deltaMs)
        {
            if (!resolveArmed) return;
            if (pendingWraps > 0) return;
            resolveDwellMs -= deltaMs;
            if (resolveDwellMs > 0f) return;
            resolveArmed = false;
            pendingCashIn = false;
            cycleLevels = 0;
            HarvestRound();
            GameEvents.RaiseZoneBEmpty();
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
            WorldArt.Rect(root, "Paper", x0, x1, geometry.ZoneBBottomY - 2f, geometry.ZoneBTopY, Theme.Paper, -20);
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
            var go = new GameObject("Wall") { layer = ArenaBuilder.WallLayer };
            go.transform.SetParent(root, false);
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
                WorldArt.Quad(root, "RampFill", new[] { a, b, new Vector2(b.x, bottom), new Vector2(a.x, bottom) }, Theme.Pine, 2);
            }
            WorldArt.Rail(root, a, b, thickness + BoardGeometry.Units(2), Theme.PineShadow, 3);
            WorldArt.Rail(root, a, b, thickness, Theme.Pine, 4);
        }

        /// <summary>A painted wooden sign — green for high multipliers, brass for low — with a stencilled "X N" and a wood-shadow edge. Moving kinds ride a kinematic body.</summary>
        void BuildGate(GateDef def)
        {
            var pose = def.PoseAt(0);
            float length = BoardGeometry.Units(def.Length);
            float thickness = BoardGeometry.Units(ZoneBLayouts.GateThickness);
            var go = new GameObject($"Gate x{def.Multiplier}") { layer = GateLayer };
            go.transform.SetParent(root, false);
            go.transform.position = geometry.ZoneBToWorld(pose.x, pose.y);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -(float)pose.angle * Mathf.Rad2Deg);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = def.Kind == GateKind.Static ? RigidbodyType2D.Static : RigidbodyType2D.Kinematic;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(length, thickness);
            box.sharedMaterial = gateMaterial;
            var gate = go.AddComponent<ZoneBGate>();
            gate.Multiplier = def.Multiplier;
            if (def.Kind != GateKind.Static) movingGates.Add((gate, def, body));

            var paint = def.Multiplier >= 4 ? Theme.GatePaint : Theme.Brass;
            float edge = BoardGeometry.Units(2);
            WorldArt.Rect(go.transform, "Edge", Vector2.zero, new Vector2(length + 2f * edge, thickness + 2f * edge), Theme.PineShadow, 4);
            WorldArt.Rect(go.transform, "Paint", Vector2.zero, new Vector2(length, thickness), paint, 5);
            WorldArt.Text(go.transform, "Label", $"X{def.Multiplier}", 15f, Theme.Ink, 6);
        }

        /// <summary>An invisible sensor (the funnel ramps already read as the mouth); a scored collector labels its multiplier.</summary>
        void BuildCollector(CollectorDef def)
        {
            var centre = geometry.ZoneBToWorld(def.X + def.Width / 2, def.Y + def.Height / 2);
            var go = new GameObject("Collector") { layer = ArenaBuilder.WallLayer };
            go.transform.SetParent(root, false);
            go.transform.position = centre;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(BoardGeometry.Units(def.Width), BoardGeometry.Units(def.Height));
            box.isTrigger = true;
            var collector = go.AddComponent<ZoneBCollector>();
            collector.ScoreMultiplier = def.ScoreMultiplier;
            if (def.ScoreMultiplier != 1) WorldArt.Text(go.transform, "Label", $"x{def.ScoreMultiplier}", 11f, Theme.Ink, 5, FontStyles.Normal);
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

            WorldArt.Rect(root, "BarOutline", x0, x1, bottom, barTop, Theme.Ink, 10);
            barGroove = WorldArt.Rect(root, "BarGroove", x0 + stroke, x1 - stroke, bottom + stroke, barTop - stroke, Theme.Groove, 11);
            barFill = WorldArt.Rect(root, "BarFill", new Vector2(barFillLeft, barMidY), new Vector2(0f, barFillHeight), Theme.Brass, 12);
            barLabel = WorldArt.Text(root, "BarLabel", "", 11f, Theme.Ink, 13);
            barLabel.transform.localPosition = new Vector3(0f, barMidY, 0f);

            // Hovering haul label — centred just above the bar, hidden until the round earns its first points.
            haulLabel = WorldArt.Text(root, "Haul", "", 18f, Theme.BrassBright, 40);
            haulLabel.transform.localPosition = new Vector3(0f, barTop + BoardGeometry.Units(16f), 0f);
            haulLabel.outlineWidth = 0.25f;
            haulLabel.outlineColor = Theme.Ink;
            haulLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Drive the shown fill every frame. With owed wraps, sweep the bar up to full at a steady pace,
        /// then celebrate + snap it to empty and clear one wrap — the visible fill → empty → fill roll,
        /// live, as balls keep draining. With none owed, ease toward the current level's fill.
        /// </summary>
        void AnimateBar(float deltaMs)
        {
            if (pendingWraps > 0)
            {
                displayFraction += deltaMs / feel.wrapFillMs;
                if (displayFraction >= 1f)
                {
                    CelebrateFull();
                    displayFraction = 0f;
                    pendingWraps -= 1;
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

            // Vertical throb of the groove (centred on its own midline, so it puffs in place).
            var grooveScale = barGroove.transform.localScale;
            Tween.Scale(barGroove.transform, new Vector3(grooveScale.x, grooveScale.y * 1.6f, 1f), 0.13f, Ease.InOutSine, cycles: 2, CycleMode.Yoyo);
            Tween.Scale(barLabel.transform, 1.3f, 0.13f, Ease.InOutSine, cycles: 2, CycleMode.Yoyo);

            // Brass sparkle rising off the full bar.
            float x0 = geometry.ZoneBMinX, x1 = geometry.ZoneBMaxX;
            for (int i = 0; i < 16; i++)
            {
                float px = Random.Range(x0 + 0.15f, x1 - 0.15f);
                float size = BoardGeometry.Units(Random.Range(4f, 8f));
                var mote = new GameObject("Sparkle");
                mote.transform.SetParent(root, false);
                mote.transform.localPosition = new Vector3(px, barMidY, 0f);
                mote.transform.localScale = new Vector3(size, size, 1f);
                var sr = mote.AddComponent<SpriteRenderer>();
                sr.sprite = BallArt.SoftDot;
                sr.color = Theme.BrassBright;
                sr.sortingOrder = 13;
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
