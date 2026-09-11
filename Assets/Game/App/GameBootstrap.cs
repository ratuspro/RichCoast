using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using RichCoast.Game.Dev;
using RichCoast.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace RichCoast.App
{
    /// <summary>
    /// The composition root: the ONLY MonoBehaviour the scene needs. Builds the camera rig, the Zone A
    /// tray, the ball factory/board/aim/death line, the juice + audio, the M1 Zone B stand-in, and
    /// the uGUI shell — all from three ScriptableObjects — then ticks the plain-C# systems.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public TierLadderSO tierLadder;
        public ProgressionSO progression;
        public GameFeelSO feel;

        public Board Board { get; private set; }
        public ZoneASystem ZoneA { get; private set; }
        public BoardGeometry Geometry { get; private set; }
        public Camera Cam { get; private set; }
        public HudView Hud { get; private set; }

        ZoneBStub zoneBStub;
        AimController aim;
        Canvas overlayCanvas;
        bool restarting;

        void Awake()
        {
            GameEvents.Reset();
            PrimeTweenConfig.warnZeroDuration = false;
            PrimeTweenConfig.warnEndValueEqualsCurrent = false;
            Application.targetFrameRate = 60;

            var ladder = tierLadder.ToLadder();
            var curve = progression.ToCurve();

            Geometry = new BoardGeometry();
            Cam = Camera.main != null ? Camera.main : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" }.GetComponent<Camera>();
            var rig = Cam.gameObject.GetComponent<CameraRig>() ?? Cam.gameObject.AddComponent<CameraRig>();
            rig.Init(Cam, Geometry);

            var world = new GameObject("ZoneA").transform;
            new ArenaBuilder(world, Geometry, feel).Build();
            Physics2D.IgnoreLayerCollision(BallFactory.BallLayer, ArenaBuilder.WallLayer, false);

            var factory = new BallFactory(world, ladder, feel);
            Board = new Board(factory, Geometry, feel);
            var deathLine = new DeathLineView(world, Geometry);
            var queue = new BallQueue();
            aim = new AimController(world, Cam, Geometry, factory, feel, queue, () => Time.unscaledTime * 1000.0);
            var mergeFx = new MergeFx(world, feel, Geometry);
            Sfx.Create(feel);

            ZoneA = new ZoneASystem(Board, aim, deathLine, queue, curve, feel, mergeFx, factory);
            zoneBStub = new ZoneBStub(Board, Geometry, Cam);

            BuildUi();
            aim.QueueChanged += () => Hud.SetNextTier(aim.Queue.NextTier);
            Hud.SetNextTier(aim.Queue.NextTier);
            GameEvents.GameOver += finalScore => GameOverView.Show(overlayCanvas, finalScore, Restart);

            ZoneA.Start();
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
            zoneBStub.Tick(deltaMs);
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
                Sfx.Instance?.ToggleMute();
        }

        void FixedUpdate() => Board.FixedTick();

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
