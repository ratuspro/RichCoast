using System.Collections.Generic;
using RichCoast.Core;
using RichCoast.Data;
using RichCoast.Gameplay.Stubs;
using RichCoast.Gameplay.ZoneA;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay
{
    /// <summary>
    /// Which zones run for real and which are stubbed. The Unity replacement for the original
    /// build's <c>?zone=</c> URL flag: it lets one zone be built and played long before the others
    /// exist, and lets a bug be isolated to one side of the seam.
    /// </summary>
    public enum ZoneMode
    {
        /// <summary>Everything real.</summary>
        Full,

        /// <summary>Real Zone A and C, stubbed Zone B (fake score, fake busy/empty).</summary>
        ZoneAC,

        /// <summary>Real Zone B driven by a debug harness that fires drops at it.</summary>
        ZoneB,
    }

    /// <summary>
    /// The single scene bootstrap: builds the shared context, constructs the systems in order,
    /// wires them to the bus and ticks them. It deliberately stays thin — systems never reference
    /// it, so adding a zone means adding one line here and nothing else.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        [Header("Tuning data")]
        [SerializeField] private ProgressionConfigSO progressionConfig;
        [SerializeField] private BallTierTableSO tierTable;

        [Header("Run mode")]
        [Tooltip("Which zones run for real. Stubs stand in for the rest, so a slice stays playable.")]
        [SerializeField] private ZoneMode zoneMode = ZoneMode.Full;

        [Tooltip("Disable to keep analytics quiet during development.")]
        [SerializeField] private bool analyticsEnabled = true;

        [Header("Performance")]
        [Tooltip("Frame rate target. 60 on the ~2019 budget Android the game is budgeted against.")]
        [SerializeField] private int targetFrameRate = 60;

        private readonly List<IGameSystem> _systems = new List<IGameSystem>();

        public GameContext Context { get; private set; }
        public ZoneMode Mode => zoneMode;

        /// <summary>The live Zone A system, for the debug harness and PlayMode tests. Null if unbuilt.</summary>
        public ZoneASystem ZoneA { get; private set; }

        private void Awake()
        {
            ApplyRuntimePerformanceSettings();

            var bus = new EventBus();
            Context = new GameContext(
                bus,
                progressionConfig != null ? progressionConfig.ToProgression() : DefaultProgression.Create(),
                tierTable != null ? tierTable.ToTable() : DefaultTierLadder.CreateTable(),
                Layout.CurrentScreenHeight());

            BuildSystems();
        }

        private void Start()
        {
            for (var i = 0; i < _systems.Count; i++) _systems[i].Create();
        }

        private void Update()
        {
            // Systems are ported from a build whose tuning is authored in milliseconds; feeding
            // them ms keeps every ported constant meaningful without a unit conversion per call.
            var deltaMs = Time.deltaTime * 1000f;
            for (var i = 0; i < _systems.Count; i++) _systems[i].Tick(deltaMs);
        }

        private void OnDestroy()
        {
            for (var i = _systems.Count - 1; i >= 0; i--) _systems[i].Dispose();
            _systems.Clear();
            Context?.Bus.Clear();
        }

        private void BuildSystems()
        {
            _systems.Add(new AnalyticsService(Context.Bus, analyticsEnabled));

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[RichCoast] No main camera in the scene — rebuild it with Rich Coast/Rebuild Game Scene.");
                return;
            }

            _systems.Add(new PhaseDirector(Context.Bus, camera, Context.PanDistance));

            // Zone A is real; Zone B and C stand in until they are migrated. Only the bus couples
            // them, so swapping a stub for the real system changes nothing here but the line.
            var hud = HudView.Create(transform);
            var zoneARoot = new GameObject("Zone A").transform;
            zoneARoot.SetParent(transform, worldPositionStays: false);

            var zoneA = new ZoneASystem(Context, camera, zoneARoot, hud);
            ZoneA = zoneA;
            _systems.Add(zoneA);
            _systems.Add(new StubZoneC(Context.Bus, zoneA));
            _systems.Add(new StubZoneB(Context.Bus));
        }

        /// <summary>
        /// Settings that must hold on device regardless of which quality level ships. The rest of
        /// the low-end budget (renderer features, stripping, graphics APIs) lives in the project
        /// settings applied by the editor configurator.
        /// </summary>
        private void ApplyRuntimePerformanceSettings()
        {
            // vSync would override the frame-rate target on some Android drivers.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;

            // A fixed 1/60 physics step keeps the ported Zone A feel identical everywhere, and the
            // max-delta cap stops a 30fps device from spiralling into catch-up steps it cannot
            // afford — physics simply runs slightly slow instead of dropping frames further.
            Time.fixedDeltaTime = 1f / 60f;
            Time.maximumDeltaTime = 1f / 15f;

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
