using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using RichCoast.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace RichCoast.App
{
    /// <summary>
    /// The composition root: the ONLY MonoBehaviour the scene needs. Builds the camera rig, the Zone A
    /// tray, the ball factory/board/aim/death line, the juice + audio, the Zone C trap-door, the Zone
    /// B split arena, the phase + theme directors and the uGUI shell — all from three
    /// ScriptableObjects — then ticks the plain-C# systems. Zones never see each other here; they
    /// share only <see cref="GameEvents"/>.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public TierLadderSO tierLadder;
        public ProgressionSO progression;
        public GameFeelSO feel;

        public Board Board { get; private set; }
        public ZoneASystem ZoneA { get; private set; }
        public ZoneBSystem ZoneB { get; private set; }
        public ZoneCSystem ZoneC { get; private set; }
        public PhaseDirector Phases { get; private set; }
        public ThemeDirector Themes { get; private set; }
        public BoardGeometry Geometry { get; private set; }
        public Camera Cam { get; private set; }
        public HudView Hud { get; private set; }
        /// <summary>The milestone arena-growth factor in force (balls are 1/this of their ladder size).</summary>
        public float ArenaScale => factory.ArenaScale;

        AimController aim;
        BallFactory factory;
        Canvas overlayCanvas;
        bool restarting;

        void Awake()
        {
            GameEvents.Reset();
            // A fresh scene owns fresh tweens: a cross-fade or zoom from a previous load (plain C# tween
            // targets outlive scene unloads) must never keep writing into this run's Theme.
            Tween.StopAll();
            PrimeTweenConfig.warnZeroDuration = false;
            PrimeTweenConfig.warnEndValueEqualsCurrent = false;
            Application.targetFrameRate = 60;
            // Every run boots in the workshop look, before anything bakes a colour.
            Theme.Apply(Palettes.Workshop);
            GameEvents.ThemeChanged += Themed.RestyleAll;

            var ladder = tierLadder.ToLadder();
            var curve = progression.ToCurve();

            Geometry = new BoardGeometry();
            Cam = Camera.main != null ? Camera.main : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" }.GetComponent<Camera>();
            var rig = Cam.gameObject.GetComponent<CameraRig>() ?? Cam.gameObject.AddComponent<CameraRig>();
            rig.Init(Cam, Geometry);
            ConfigurePhysicsLayers();

            var world = new GameObject("ZoneA").transform;
            var arena = new ArenaBuilder(world, Geometry, feel);
            arena.Build();

            factory = new BallFactory(world, ladder, feel);
            Board = new Board(factory, Geometry, feel);
            var deathLine = new DeathLineView(world, Geometry);
            var queue = new BallQueue();
            aim = new AimController(world, Cam, Geometry, factory, feel, queue, () => Time.unscaledTime * 1000.0);
            var mergeFx = new MergeFx(world, feel, Geometry);
            var highlight = new DropHighlight(world);
            var growth = new ArenaGrowth(factory, Board, feel);
            Sfx.Create(feel);

            ZoneA = new ZoneASystem(Board, aim, deathLine, queue, curve, ladder, feel, mergeFx, factory, highlight, growth, Geometry, world);
            // One of the two layouts per run — every restart reloads the scene, so this re-rolls.
            ZoneB = new ZoneBSystem(transform, Geometry, feel, Cam, ZoneBLayouts.Pick(Random.value), curve.ScoreBarTargetForLevel(1));
            ZoneC = new ZoneCSystem(transform, Board, Geometry, feel);
            Phases = new PhaseDirector(rig, feel);
            Themes = new ThemeDirector(curve, feel);

            BuildUi();
            aim.QueueChanged += () => Hud.SetNextTier(aim.Queue.NextTier);
            Hud.SetNextTier(aim.Queue.NextTier);
            GameEvents.GameOver += finalScore => GameOverView.Show(overlayCanvas, finalScore, Restart);

            // Announce initial state LAST, after every system has subscribed.
            ZoneA.Start();
            Phases.Start();
        }

        /// <summary>
        /// Physics layers are physics only, never render routing: Zone A balls (8) never touch Zone B's
        /// balls or gates; fresh split copies (11) ignore gates for their grace window. Walls (9) are
        /// shared — both zones' balls collide with them.
        /// </summary>
        static void ConfigurePhysicsLayers()
        {
            Physics2D.IgnoreLayerCollision(BallFactory.BallLayer, ArenaBuilder.WallLayer, false);
            Physics2D.IgnoreLayerCollision(BallFactory.BallLayer, ZoneBSystem.BallLayer, true);
            Physics2D.IgnoreLayerCollision(BallFactory.BallLayer, ZoneBSystem.GraceLayer, true);
            Physics2D.IgnoreLayerCollision(BallFactory.BallLayer, ZoneBSystem.GateLayer, true);
            Physics2D.IgnoreLayerCollision(ZoneBSystem.GraceLayer, ZoneBSystem.GateLayer, true);
            Physics2D.IgnoreLayerCollision(ZoneBSystem.BallLayer, ZoneBSystem.GateLayer, false);
            Physics2D.IgnoreLayerCollision(ZoneBSystem.BallLayer, ZoneBSystem.GraceLayer, false);
        }

        void BuildUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>();
            }
            var hudCanvas = UiKit.Canvas("HUD", Cam, 10);
            overlayCanvas = UiKit.Canvas("Overlay", Cam, 20);
            Hud = HudView.Build(hudCanvas, overlayCanvas, feel);
        }

        void Update()
        {
            float deltaMs = Time.deltaTime * 1000f;
            aim.Tick();
            ZoneA.Tick(deltaMs);
            ZoneC.Tick(deltaMs);
            ZoneB.Tick(deltaMs);
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
                Sfx.Instance?.ToggleMute();
        }

        void FixedUpdate()
        {
            Board.FixedTick();
            ZoneB.FixedTick();
        }

        public void Restart()
        {
            if (restarting) return;
            restarting = true;
            Tween.StopAll();
            GameEvents.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Test/debug: drop a tier at a world x through the real system (spends the buffer).</summary>
        public void DebugDrop(float x, int tier) => ZoneA.DebugDrop(x, tier);
    }
}
