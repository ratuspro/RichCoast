using GameAnalyticsSDK;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Gameplay
{
    /// <summary>
    /// The single place the game talks to GameAnalytics. Everything else raises ordinary game
    /// events on the bus and this service decides what, if anything, is worth reporting — so no
    /// gameplay code carries an SDK dependency and analytics can be swapped or stubbed wholesale.
    ///
    /// Replaces the scratch <c>Assets/Inits.cs</c> from the empty Unity project.
    /// </summary>
    public sealed class AnalyticsService : IGameSystem
    {
        private readonly EventBus _bus;
        private readonly bool _enabled;
        private int _level = 1;

        public AnalyticsService(EventBus bus, bool enabled = true)
        {
            _bus = bus;
            _enabled = enabled;
        }

        public void Create()
        {
            if (!_enabled) return;

            GameAnalytics.Initialize();

            _bus.Subscribe<ProgressionChanged>(OnProgressionChanged);
            _bus.Subscribe<GameOver>(OnGameOver);

            // A run starts the moment the game boots — there is no menu yet.
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, RunName);
        }

        public void Tick(float deltaMs) { }

        public void Dispose()
        {
            if (!_enabled) return;
            _bus.Unsubscribe<ProgressionChanged>(OnProgressionChanged);
            _bus.Unsubscribe<GameOver>(OnGameOver);
        }

        /// <summary>
        /// The run is one endless session, so it is reported as a single progression "world" with
        /// the reached level and score as its result — not one event per level, which would be
        /// thousands of events for no extra insight.
        /// </summary>
        private const string RunName = "Run";

        private void OnProgressionChanged(ProgressionChanged e) => _level = e.Level;

        private void OnGameOver(GameOver e)
        {
            GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, RunName, Mathf.RoundToInt((float)e.FinalScore));
            GameAnalytics.NewDesignEvent("run:levelReached", _level);
        }
    }
}
