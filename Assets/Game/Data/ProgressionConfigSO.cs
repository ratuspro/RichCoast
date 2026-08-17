using System;
using System.Collections.Generic;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Data
{
    /// <summary>
    /// Designer-editable progression stage table, seeded from <see cref="DefaultProgression"/>.
    /// Replaces the original <c>progression.json</c>.
    ///
    /// The old JSON also carried a per-stage <c>bufferBalls</c> array; it is deliberately not
    /// ported, because the progression redesign moved the ball supply to the oscillating
    /// <see cref="Progression.BufferForLevel"/> curve and the JSON field had become dead data.
    /// </summary>
    [CreateAssetMenu(menuName = "Rich Coast/Progression Config", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfigSO : ScriptableObject
    {
        [Serializable]
        public struct Stage
        {
            [Tooltip("First internal level this stage applies to. Window shift-ups must land on multiples of 20.")]
            public int fromLevel;

            [Tooltip("Inclusive tier range balls are drawn from.")]
            public int minTier;
            public int maxTier;

            [Tooltip("Score-bar target at this anchor; levels in between interpolate geometrically.")]
            public double scoreBarTarget;

            [Tooltip("Milestone squeeze: <1 grows the arena less than the balls (harder), >1 is a breather.")]
            public float tightness;

            [Tooltip("Environment palette this milestone swaps to. Blank inherits the previous one.")]
            public string palette;
        }

        [SerializeField] private List<Stage> stages = CreateDefaultStages();

        private Progression _cached;

        /// <summary>Plain-object view for Core, built once from the authored stages.</summary>
        public Progression ToProgression()
        {
            if (_cached != null) return _cached;

            var converted = new List<ProgressionStage>(stages.Count);
            foreach (var s in stages)
            {
                converted.Add(new ProgressionStage(
                    s.fromLevel,
                    new TierWindow(s.minTier, s.maxTier),
                    s.scoreBarTarget,
                    s.tightness <= 0f ? 1f : s.tightness,
                    s.palette));
            }
            return _cached = new Progression(converted);
        }

        private void OnValidate() => _cached = null;

        private void Reset() => stages = CreateDefaultStages();

        private static List<Stage> CreateDefaultStages()
        {
            var authored = DefaultProgression.CreateStages();
            var list = new List<Stage>(authored.Length);
            foreach (var s in authored)
            {
                list.Add(new Stage
                {
                    fromLevel = s.FromLevel,
                    minTier = s.Window.Min,
                    maxTier = s.Window.Max,
                    scoreBarTarget = s.ScoreBarTarget,
                    tightness = s.Tightness,
                    palette = s.Palette,
                });
            }
            return list;
        }
    }
}
