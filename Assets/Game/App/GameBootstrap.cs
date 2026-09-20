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
        /// <summary>The measurement seam. A pure GameEvents subscriber — nothing in a zone knows it exists.</summary>
        public AnalyticsService Analytics { get; private set; }
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
        AnalyticsOverlay analyticsOverlay;
        bool restarting;
        bool quitConfirmed;
        bool overlayGesture;

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

            // Built from the SAVED consent, so the backend sink does not exist at all unless the
            // player has already said yes — a consent granted this session takes effect next launch.
            Analytics = new AnalyticsService(Save.settings.Consent);

            BuildUi();

            var intent = PendingIntent;
            PendingIntent = AppIntent.Title;
            if (intent == AppIntent.NewRun) StartRun(null);
            else if (Save.settings.Consent == ConsentState.Unasked) ShowConsentThenTitle();
            else ShowTitle();
        }

        void OnEnable() => Application.wantsToQuit += WantsToQuit;
        void OnDisable() => Application.wantsToQuit -= WantsToQuit;

        /// <summary>
        /// First launch: the consent question comes BEFORE the title, because it is the one thing that
        /// must be answered before any run can start. Answering it drops straight through to the title.
        /// </summary>
        void ShowConsentThenTitle()
        {
            ConsentView.Show(overlayCanvas, ConsentState.Unasked, SetConsent, ShowTitle);
        }

        /// <summary>Persist a consent answer, and honour a revocation immediately by wiping the local tail.</summary>
        void SetConsent(ConsentState state)
        {
            bool revoked = Save.settings.Consent == ConsentState.Granted && state != ConsentState.Granted;
            Save.settings.Consent = state;
            SaveStore.Save(Save);
            if (revoked) Analytics?.OnConsentRevoked();
        }

        void ShowTitle()
        {
            State = AppState.Title;
            if (Hud != null) Hud.gameObject.SetActive(false);
            titleUi = TitleView.Show(overlayCanvas, Save,
                run => StartRun(run),
                on => { Save.settings.soundOn = on; ApplySettings(); SaveStore.Save(Save); },
                on => { Save.settings.hapticsOn = on; ApplySettings(); SaveStore.Save(Save); },
                () => ConsentView.Show(overlayCanvas, Save.settings.Consent, SetConsent));
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
            Analytics?.StartRun(restore != null, Session.ZoneA.Level);
            Hud.SetNextTier(Session.Queue.NextTier);
            // The HUD outlives a run and ZoneASystem does not, so the tilt button is re-pointed here
            // rather than held across the reload.
            Hud.BindTilt(() => Session != null && Session.ZoneA.CanTilt,
                         () => Session?.ZoneA.Tilt(),
                         Session.ZoneA.TiltCharges, Session.ZoneA.TiltsLeft);
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

        void OnGameOver(GameOverEvent e)
        {
            double finalScore = e.FinalScore;
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
        /// Raise the quit confirmation (idempotent while it is already up). Verified on a Pixel 7 /
        /// Android 16: Unity 6's activity CONSUMES the Back press without attempting to quit, so
        /// <see cref="Application.wantsToQuit"/> never fires on its own and Back would otherwise be
        /// completely inert. The Escape key is therefore the primary trigger — the manifest ships
        /// <c>enableOnBackInvokedCallback=false</c>, so Back takes the legacy path and arrives here.
        /// </summary>
        public void RequestQuit()
        {
            if (quitDialog != null) return;
            Flush();
            quitDialog = ConfirmView.Show(overlayCanvas, "QUIT RICHCOAST?", "QUIT",
                () => { quitConfirmed = true; Flush(); Application.Quit(); },
                () => { quitDialog = null; });
        }

        /// <summary>
        /// The secondary net: anything that DOES reach Unity's quit path (a platform where the
        /// activity really does finish, or our own Application.Quit) is vetoed until confirmed.
        /// </summary>
        bool WantsToQuit()
        {
            if (quitConfirmed) return true;
            RequestQuit();
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
            Analytics?.Flush();
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
            // Its own canvas above everything: a debug view that a dialog could cover is useless.
            analyticsOverlay = AnalyticsOverlay.Build(UiKit.Canvas("Debug", Cam, 30), Analytics);
        }

        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.mKey.wasPressedThisFrame) ToggleSound();
                if (keyboard.aKey.wasPressedThisFrame) analyticsOverlay?.Toggle();
                // Editor convenience only; on device the button is the input.
                if (keyboard.tKey.wasPressedThisFrame && State == AppState.Run) Session?.ZoneA.Tilt();
                // Android's Back arrives as Escape. See RequestQuit: Unity consumes Back itself, so
                // without this the button does nothing at all on device.
                if (keyboard.escapeKey.wasPressedThisFrame) RequestQuit();
            }
            if (analyticsOverlay != null && UnityEngine.InputSystem.Touchscreen.current != null)
            {
                int down = 0;
                foreach (var t in UnityEngine.InputSystem.Touchscreen.current.touches)
                    if (t.press.isPressed) down++;
                if (down >= 4 && !overlayGesture) analyticsOverlay.Toggle();
                overlayGesture = down >= 4;
            }
            if (State != AppState.Run || Session == null) return;
            // Unscaled: time spent frozen under a dialog is still time the player spent on this run.
            Analytics.ArenaScale = Session.ArenaScale;
            Analytics.Tick(Time.unscaledDeltaTime * 1000f);
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
            // Left, not lost: emitting a run_end here would put a third, invisible outcome into the
            // cause funnel and make "where do runs end" unanswerable all over again.
            if (State == AppState.Run) Analytics?.AbandonRun();
            PendingIntent = AppIntent.Title;
            Reload();
        }

        void Reload()
        {
            Analytics?.Dispose();
            Analytics = null;
            Tween.StopAll();
            GameEvents.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Test/debug: drop a tier at a world x through the real system (spends the buffer).</summary>
        public void DebugDrop(float x, int tier) => Session?.DebugDrop(x, tier);
    }
}
