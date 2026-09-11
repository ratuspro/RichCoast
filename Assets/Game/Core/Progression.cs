using System;
using System.Collections.Generic;
using System.Linq;

namespace RichCoast.Core
{
    /// <summary>One authored anchor of the progression curve (port of a <c>progression.json</c> stage).</summary>
    public sealed class ProgressionStage
    {
        public int FromLevel;
        public int WindowMin;
        public int WindowMax;
        /// <summary>Score-bar target at this anchor; the curve interpolates geometrically between anchors.</summary>
        public double ScoreBarTarget;
        /// <summary>Optional curated first hand (tiers) seeded into the queue on this stage's own level.</summary>
        public int[] BufferBalls;
        /// <summary>Milestone difficulty knob (null = 1): multiplies the neutral arena growth. &lt;1 tighter, &gt;1 roomier.</summary>
        public double? Tightness;
        /// <summary>Environment palette the milestone swaps to (author-then-hold). Null = keep.</summary>
        public string Palette;

        public (int min, int max) Window => (WindowMin, WindowMax);
    }

    /// <summary>
    /// The progression curve — a port of the Phaser <c>Progression.ts</c>, parameterised by its
    /// authored stages so the Game layer's <c>ProgressionSO</c> can hand in inspector-tuned data
    /// while <see cref="Default"/> mirrors the shipped <c>progression.json</c> exactly.
    ///
    /// Stages are ANCHORS, not plateaus: between anchors the score-bar target grows geometrically
    /// per level, and past the last anchor it keeps growing at <see cref="TailTargetGrowth"/>. The
    /// draw window keeps stepping up in the tail too (<see cref="WindowForLevel"/>).
    /// </summary>
    public sealed class ProgressionCurve
    {
        /// <summary>Levels between arena zoom-out milestones (20, 40, 60, …). Window shift-ups MUST land on these.</summary>
        public const int MilestoneEvery = 20;

        /// <summary>Extra refill balls per level crossed BEYOND the first in one cash-in cycle.</summary>
        public const int BurstRefillBonus = 2;

        /// <summary>Draw-window step per TAIL milestone: half the authored +4.</summary>
        public const int TailWindowStep = 2;

        /// <summary>Per-level target growth past the last authored stage: 3^4 per milestone span (the authored curve's own rate).</summary>
        public static readonly double TailTargetGrowth = Math.Pow(3, 4.0 / MilestoneEvery);

        readonly ProgressionStage[] stages;

        public ProgressionCurve(IEnumerable<ProgressionStage> authored)
        {
            stages = authored.OrderBy(s => s.FromLevel).ToArray();
            if (stages.Length == 0) throw new ArgumentException("progression needs at least one stage", nameof(authored));
        }

        public IReadOnlyList<ProgressionStage> Stages => stages;

        ProgressionStage Last => stages[stages.Length - 1];

        /// <summary>The shipped curve — a 1:1 port of <c>progression.json</c> on the Phaser master branch.</summary>
        public static ProgressionCurve Default => new ProgressionCurve(new[]
        {
            new ProgressionStage { FromLevel = 1, WindowMin = 1, WindowMax = 1, ScoreBarTarget = 20, BufferBalls = new[] { 1, 1, 1, 1, 1, 1, 1, 1 } },
            new ProgressionStage { FromLevel = 2, WindowMin = 1, WindowMax = 2, ScoreBarTarget = 80, BufferBalls = new[] { 1, 1, 2, 1, 2 } },
            new ProgressionStage { FromLevel = 3, WindowMin = 1, WindowMax = 3, ScoreBarTarget = 130, BufferBalls = new[] { 2, 1, 1, 3, 2 } },
            new ProgressionStage { FromLevel = 4, WindowMin = 1, WindowMax = 4, ScoreBarTarget = 200, BufferBalls = new[] { 1, 2, 3, 2 } },
            new ProgressionStage { FromLevel = 20, WindowMin = 5, WindowMax = 8, ScoreBarTarget = 5_000, Tightness = 0.92, Palette = "dusk" },
            new ProgressionStage { FromLevel = 40, WindowMin = 9, WindowMax = 12, ScoreBarTarget = 400_000, Tightness = 1.05, Palette = "night" },
            new ProgressionStage { FromLevel = 60, WindowMin = 13, WindowMax = 16, ScoreBarTarget = 33_000_000, Tightness = 0.85, Palette = "dawn" },
            new ProgressionStage { FromLevel = 80, WindowMin = 17, WindowMax = 20, ScoreBarTarget = 2_700_000_000, Tightness = 1.05, Palette = "gilded" },
        });

        /// <summary>Fraction of the way from the last milestone to the next, in [0, 1). 0 the level a milestone lands.</summary>
        public static double MilestoneProgress(int level) => (level % MilestoneEvery) / (double)MilestoneEvery;

        /// <summary>
        /// Balls the player is refilled to at a given internal level (1-based). The supply OSCILLATES:
        /// a slowly-rising base (15 → 18 by level 30, flat after) swings ±2 by level parity — even =
        /// roomy "harvest", odd = lean "pressure"; milestone levels always pay the harvest amount.
        /// Sequence: 8, 17, 13, 17, 13, … 18/14 … → 20/16.
        /// </summary>
        public static int BufferForLevel(int level)
        {
            if (level <= 1) return 8; // tutorial start
            int baseCount = Math.Min(15 + level / 10, 18);
            if (level % MilestoneEvery == 0) return baseCount + 2;
            return level % 2 == 0 ? baseCount + 2 : baseCount - 2;
        }

        /// <summary>Palette active at a level: the last stage at-or-below it that authors one, else "workshop".</summary>
        public string PaletteNameForLevel(int level)
        {
            string result = "workshop";
            foreach (var stage in stages)
            {
                if (stage.FromLevel > level) break;
                if (!string.IsNullOrEmpty(stage.Palette)) result = stage.Palette;
            }
            return result;
        }

        /// <summary>
        /// The score-bar target for a level: authored anchors interpolated geometrically per level,
        /// continuing at <see cref="TailTargetGrowth"/> past the last anchor.
        /// </summary>
        public double ScoreBarTargetForLevel(int level)
        {
            var last = Last;
            if (level >= last.FromLevel)
            {
                return Math.Round(last.ScoreBarTarget * Math.Pow(TailTargetGrowth, level - last.FromLevel));
            }
            int i = 0;
            while (i + 1 < stages.Length && stages[i + 1].FromLevel <= level) i++;
            var a = stages[i];
            var b = stages[i + 1];
            double t = (level - a.FromLevel) / (double)(b.FromLevel - a.FromLevel);
            return Math.Round(a.ScoreBarTarget * Math.Pow(b.ScoreBarTarget / a.ScoreBarTarget, t));
        }

        /// <summary>The active authored stage for a level (1-based).</summary>
        public ProgressionStage GetStage(int level)
        {
            var result = stages[0];
            foreach (var stage in stages)
            {
                if (stage.FromLevel <= level) result = stage;
                else break;
            }
            return result;
        }

        /// <summary>True past the last authored stage — the endless TAIL.</summary>
        public bool IsTailLevel(int level) => level > Last.FromLevel;

        /// <summary>
        /// The live draw window for a level: authored through the last stage, then stepping up
        /// <see cref="TailWindowStep"/> tiers at every tail milestone.
        /// </summary>
        public (int min, int max) WindowForLevel(int level)
        {
            var window = GetStage(level).Window;
            if (!IsTailLevel(level)) return window;
            int shifts = (level - Last.FromLevel) / MilestoneEvery;
            return (window.min + TailWindowStep * shifts, window.max + TailWindowStep * shifts);
        }
    }
}
