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
    /// <summary>What the NEXT scene load should do once it boots. See <see cref="GameBootstrap.PendingIntent"/>.</summary>
    public enum AppIntent { Title, NewRun }

    /// <summary>
    /// The composition root: the ONLY MonoBehaviour the scene needs. Builds the camera rig, the audio
    /// singleton and the uGUI shell, loads the save, and then runs one of three app states — the title
    /// screen, a live <see cref="GameSession"/>, or the game-over overlay. The run itself lives in
    /// <see cref="GameSession"/>; this class owns composition, app state and persistence.
    /// <para>Zones never see each other here; they share only <see cref="GameEvents"/>.</para>
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public enum AppState { Title, Run, GameOver }

        public TierLadderSO tierLadder;
        public ProgressionSO progression;
        public GameFeelSO feel;
        public ZoneBArenaSO zoneBArena;

        /// <summary>
        /// RESTART reloads the scene — the honest reset for pools, tweens and theme — and would
        /// otherwise land on the title, so it sets this first. Statics survive scene loads, the same
        /// mechanism <see cref="GameEvents"/> already relies on. PlayMode tests set it too.
        /// </summary>
        public static AppIntent PendingIntent = AppIntent.Title;

        public AppState State { get; private set; } = AppState.Title;
        public GameSession Session { get; private set; }
        public SaveData Save { get; private set; }

        public BoardGeometry Geometry { get; private set; }
        public Camera Cam { get; private set; }
        public HudView Hud { get; private set; }

        // Shims so existing callers and the PlayMode suite keep reaching the live run directly.
        public Board Board => Session?.Board;
        public ZoneASystem ZoneA => Session?.ZoneA;
        public ZoneBSystem ZoneB => Session?.ZoneB;
        public ZoneCSystem ZoneC => Session?.ZoneC;
        public PhaseDirector Phases => Session?.Phases;
        public ThemeDirector Themes => Session?.Themes;
        /// <summary>The milestone arena-growth factor in force (balls are 1/this of their ladder size).</summary>
        public float ArenaScale => Session?.ArenaScale ?? 1f;

        ProgressionCurve curve;
        CameraRig rig;
        Canvas overlayCanvas;
        GameObject titleUi;
        GameObject quitDialog;
        bool restarting;
        bool quitConfirmed;

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

            curve = progression.ToCurve();
            Save = SaveStore.Load(curve);

            Geometry = new BoardGeometry();
            Cam = Camera.main != null ? Camera.main : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" }.GetComponent<Camera>();
            rig = Cam.gameObject.GetComponent<CameraRig>() ?? Cam.gameObject.AddComponent<CameraRig>();
            rig.Init(Cam, Geometry);
            ConfigurePhysicsLayers();
            // The audio singleton outlives sessions, so settings apply once at boot, not per run.
            Sfx.Create(feel);
            ApplySettings();

            BuildUi();

            var intent = PendingIntent;
            PendingIntent = AppIntent.Title;
            if (intent == AppIntent.NewRun) StartRun(null);
            else ShowTitle();
        }

        void OnEnable() => Application.wantsToQuit += WantsToQuit;
        void OnDisable() => Application.wantsToQuit -= WantsToQuit;

        void ShowTitle()
        {
            State = AppState.Title;
            if (Hud != null) Hud.gameObject.SetActive(false);
            titleUi = TitleView.Show(overlayCanvas, Save,
                run => StartRun(run),
                on => { Save.settings.soundOn = on; ApplySettings(); SaveStore.Save(Save); },
                on => { Save.settings.hapticsOn = on; ApplySettings(); SaveStore.Save(Save); });
        }

        /// <summary>Begin a run — fresh when <paramref name="restore"/> is null, otherwise resumed.</summary>
        public void StartRun(RunSnapshot restore)
        {
            if (titleUi != null) { Destroy(titleUi); titleUi = null; }
            State = AppState.Run;
            if (Hud != null) Hud.gameObject.SetActive(true);

            Session = new GameSession(transform, Cam, rig, Geometry, tierLadder.ToLadder(), curve, feel,
                zoneBArena != null ? zoneBArena.ToParams() : new ZoneBGenParams(),
                DoorColumns(), Random.Range(int.MinValue, int.MaxValue), restore);
            Session.CheckpointReady += Checkpoint;
            Session.Aim.QueueChanged += () => Hud.SetNextTier(Session.Queue.NextTier);
            GameEvents.GameOver += OnGameOver;

            Session.Begin(restore);
            Hud.SetNextTier(Session.Queue.NextTier);
        }

        /// <summary>
        /// The run settled. Written straight through rather than held for the pause callback: the
        /// payload is a few hundred bytes and turns are seconds apart, so paying for the write here
        /// removes all dependence on the OS delivering a pause before it evicts us.
        /// </summary>
        void Checkpoint()
        {
            Save.SetRun(Session.Capture());
            SaveStore.Save(Save);
        }

        void OnGameOver(double finalScore)
        {
            State = AppState.GameOver;
            bool newBest = Save.records.Merge(finalScore, Session != null ? Session.ZoneA.Level : 1);
            Save.ClearRun();
            SaveStore.Save(Save);
            GameOverView.Show(overlayCanvas, finalScore, Save.records, newBest, Restart, ToTitle);
        }

        /// <summary>The one path to muted: the editor's M shortcut and the title toggle share it.</summary>
        public void ToggleSound()
        {
            Save.settings.soundOn = !Save.settings.soundOn;
            ApplySettings();
            SaveStore.Save(Save);
        }

        public void ApplySettings()
        {
            Sfx.Instance?.SetMuted(!Save.settings.soundOn);
            Haptics.Enabled = Save.settings.hapticsOn;
        }

        /// <summary>
        /// Android's Back finishes the activity with no confirmation of its own, so veto the quit and
        /// ask. Built on wantsToQuit rather than the Escape key because this sits downstream of however
        /// the platform delivers Back — it works whether or not the Input System surfaces it.
        /// </summary>
        bool WantsToQuit()
        {
            if (quitConfirmed) return true;
            Flush();
            if (quitDialog != null) return false; // already asking
            quitDialog = ConfirmView.Show(overlayCanvas, "QUIT RICHCOAST?", "QUIT",
                () => { quitConfirmed = true; Flush(); Application.Quit(); },
                () => { quitDialog = null; });
            return false;
        }

        void OnApplicationPause(bool paused) { if (paused) Flush(); }
        void OnApplicationQuit() => Flush();

        /// <summary>
        /// Belt-and-braces only: the run's lifeline is the per-checkpoint write. This exists for
        /// settings changes, and for the case where the last checkpoint is already current.
        /// </summary>
        void Flush()
        {
            if (Save == null) return;
            if (State == AppState.Run && Session != null && Session.ZoneA.IsQuiescent)
                Save.SetRun(Session.Capture());
            SaveStore.Save(Save);
        }

        /// <summary>
        /// The columns the trap-door can drop a ball down — its nine sweep positions. Zone B centres the
        /// golden mouth on one of them, so hitting the mouth is a timing skill rather than luck.
        /// </summary>
        static double[] DoorColumns()
        {
            var xs = new double[DesignSpace.SweepPositions];
            for (int i = 0; i < xs.Length; i++)
                xs[i] = DoorMath.SweepPositionX(i, DesignSpace.SweepPositions, DesignSpace.SweepMargin, DesignSpace.Width - DesignSpace.SweepMargin);
            return xs;
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
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
                ToggleSound();
            if (State != AppState.Run || Session == null) return;
            Session.Tick(Time.deltaTime * 1000f);
        }

        void FixedUpdate()
        {
            if (State != AppState.Run || Session == null) return;
            Session.FixedTick();
        }

        public void Restart()
        {
            if (restarting) return;
            restarting = true;
            PendingIntent = AppIntent.NewRun;
            Reload();
        }

        public void ToTitle()
        {
            if (restarting) return;
            restarting = true;
            PendingIntent = AppIntent.Title;
            Reload();
        }

        void Reload()
        {
            Tween.StopAll();
            GameEvents.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Test/debug: drop a tier at a world x through the real system (spends the buffer).</summary>
        public void DebugDrop(float x, int tier) => Session?.DebugDrop(x, tier);
    }
}
