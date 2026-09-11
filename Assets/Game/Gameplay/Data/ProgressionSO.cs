using System;
using System.Linq;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Inspector port of <c>progression.json</c>: the authored anchors of the progression curve.
    /// <see cref="ToCurve"/> builds the engine-free <see cref="ProgressionCurve"/> the systems read.
    /// </summary>
    [CreateAssetMenu(menuName = "RichCoast/Progression", fileName = "Progression")]
    public sealed class ProgressionSO : ScriptableObject
    {
        [Serializable]
        public sealed class Stage
        {
            public int fromLevel = 1;
            public int windowMin = 1;
            public int windowMax = 4;
            public double scoreBarTarget = 20;
            [Tooltip("Optional curated first hand (tiers) seeded on this stage's own level. Empty = random.")]
            public int[] bufferBalls = Array.Empty<int>();
            [Tooltip("Milestone difficulty knob: multiplies the neutral arena growth. 0 = default (1). <1 tighter, >1 roomier.")]
            public double tightness = 0;
            [Tooltip("Environment palette this milestone swaps to (author-then-hold). Empty = keep.")]
            public string palette = "";
        }

        public Stage[] stages = DefaultStages();

        public ProgressionCurve ToCurve() => new ProgressionCurve(stages.Select(s => new ProgressionStage
        {
            FromLevel = s.fromLevel,
            WindowMin = s.windowMin,
            WindowMax = s.windowMax,
            ScoreBarTarget = s.scoreBarTarget,
            BufferBalls = s.bufferBalls != null && s.bufferBalls.Length > 0 ? s.bufferBalls : null,
            Tightness = s.tightness > 0 ? s.tightness : (double?)null,
            Palette = string.IsNullOrEmpty(s.palette) ? null : s.palette,
        }));

        /// <summary>The shipped curve, mirrored from <see cref="ProgressionCurve.Default"/>.</summary>
        public static Stage[] DefaultStages() => ProgressionCurve.Default.Stages.Select(s => new Stage
        {
            fromLevel = s.FromLevel,
            windowMin = s.WindowMin,
            windowMax = s.WindowMax,
            scoreBarTarget = s.ScoreBarTarget,
            bufferBalls = s.BufferBalls ?? Array.Empty<int>(),
            tightness = s.Tightness ?? 0,
            palette = s.Palette ?? "",
        }).ToArray();
    }
}
