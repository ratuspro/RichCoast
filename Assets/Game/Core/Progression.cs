using System;
using System.Collections.Generic;
using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>An inclusive tier range — the pool balls are drawn from.</summary>
    public readonly struct TierWindow
    {
        public readonly int Min;
        public readonly int Max;

        public TierWindow(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public TierWindow Shifted(int by) => new TierWindow(Min + by, Max + by);
        public bool Contains(int tier) => tier >= Min && tier <= Max;
        public override string ToString() => $"[{Min},{Max}]";
    }

    /// <summary>
    /// One authored progression stage. Applies from <see cref="FromLevel"/> until the next stage;
    /// levels between stages hold the previous stage's values.
    /// </summary>
    public readonly struct ProgressionStage
    {
        public readonly int FromLevel;
        public readonly TierWindow Window;
        public readonly double ScoreBarTarget;

        /// <summary>
        /// Milestone difficulty knob (1 = neutral): multiplies the arena-growth factor that would
        /// keep apparent ball size constant. &lt;1 = arena grows less than the balls = tighter;
        /// &gt;1 = a roomier breather. Only meaningful on stages whose window shifts.
        /// </summary>
        public readonly float Tightness;

        /// <summary>
        /// Environment palette this milestone swaps to. Authored on window-shift stages; levels
        /// between/after authored stages hold the last one (author-then-hold).
        /// Empty = inherit.
        /// </summary>
        public readonly string Palette;

        public ProgressionStage(int fromLevel, TierWindow window, double scoreBarTarget, float tightness = 1f, string palette = null)
        {
            FromLevel = fromLevel;
            Window = window;
            ScoreBarTarget = scoreBarTarget;
            Tightness = tightness;
            Palette = palette;
        }
    }

    /// <summary>
    /// Resolves the authored stage table into the per-level parameters the game reads: the ball
    /// draw window, the score-bar target, the arena-growth tightness and the palette.
    ///
    /// Ported from <c>core/Progression.ts</c> + <c>progression.json</c>. The shifts happen
    /// silently — no level-up screen; the game simply becomes different as the player advances.
    /// The one visible beat is the arena-growth camera zoom at window-shift milestones.
    /// </summary>
    public sealed class Progression
    {
        /// <summary>
        /// Levels between arena zoom-out milestones (20, 40, 60, …). The draw-window shift-ups in
        /// the stage table MUST land on these same levels — the milestone reads the new window's
        /// floor as its blacklist threshold, so a window that stepped between milestones would
        /// desync the arena scale and blacklist from the spawn pool.
        /// </summary>
        public const int MilestoneEvery = 20;

        /// <summary>
        /// Draw-window step per TAIL milestone: half the authored +4, so the material ladder keeps
        /// advancing (wrapping past the ladder end) but the endgame climbs gently.
        /// </summary>
        public const int TailWindowStep = 2;

        /// <summary>
        /// Extra refill balls per level crossed BEYOND the first in one cash-in cycle. Only the
        /// final level's refill runs after a burst, so without this a 4-level burst pays exactly
        /// like a 1-level fill — the bonus makes the roll-through jackpot tangible in the resource
        /// that matters.
        /// </summary>
        public const int BurstRefillBonus = 2;

        public const string BasePalette = "workshop";

        /// <summary>
        /// Per-level growth of the score-bar target past the last authored stage. Ball values
        /// triple per tier and (pre-tail) the window shifts +4 tiers every milestone, so value
        /// magnitude — and the authored anchor curve tracking it — grows ×3⁴ per milestone span.
        /// The tail continues that same rate so one good drain keeps earning ~one level forever;
        /// a flat tail would let ever-tripling balls cross a frozen target thousands of times in
        /// one drain.
        /// </summary>
        public static readonly double TailTargetGrowth = Math.Pow(3.0, 4.0 / MilestoneEvery);

        private readonly ProgressionStage[] _stages;

        public Progression(IEnumerable<ProgressionStage> stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var list = new List<ProgressionStage>(stages);
            if (list.Count == 0) throw new ArgumentException("at least one stage is required", nameof(stages));
            list.Sort((a, b) => a.FromLevel.CompareTo(b.FromLevel));
            _stages = list.ToArray();
        }

        public IReadOnlyList<ProgressionStage> Stages => _stages;

        private ProgressionStage LastStage => _stages[_stages.Length - 1];

        /// <summary>
        /// Fraction of the way from the last milestone to the next, in [0, 1). 0 the level a
        /// milestone lands (the HUD bar resets), climbing back toward 1 after.
        /// </summary>
        public static float MilestoneProgress(int level) => (level % MilestoneEvery) / (float)MilestoneEvery;

        /// <summary>
        /// Balls the player is refilled to at a given internal level (1-based) — the single source
        /// of truth for the ball-supply ramp, deliberately decoupled from the stage table.
        ///
        /// The supply OSCILLATES instead of growing forever: a slowly-rising base (15 → 18 by
        /// level 30, flat after) swings ±2 by level parity — even levels are roomy "harvest"
        /// refills, odd levels are lean "pressure" refills that lean on balls carried over on the
        /// board. Milestone levels always pay the harvest amount (a breather to enjoy the new draw
        /// window). The cap of 20 drops is deliberate: difficulty comes from ball size versus
        /// arena room, never from a long drop chore.
        /// </summary>
        public static int BufferForLevel(int level)
        {
            if (level <= 1) return 8; // tutorial start: window [1,1], a couple of merge chains' worth
            var b = Mathf.Min(15 + level / 10, 18);
            if (level % MilestoneEvery == 0) return b + 2; // milestones are always a breather
            return level % 2 == 0 ? b + 2 : b - 2;
        }

        /// <summary>The active stage for a given internal level (1-based).</summary>
        public ProgressionStage GetStage(int level)
        {
            var result = _stages[0];
            for (var i = 0; i < _stages.Length; i++)
            {
                if (_stages[i].FromLevel > level) break;
                result = _stages[i];
            }
            return result;
        }

        /// <summary>
        /// True past the last authored stage — the endless TAIL, where windows and zooms follow
        /// the self-healing policies here instead of authored stage data.
        /// </summary>
        public bool IsTailLevel(int level) => level > LastStage.FromLevel;

        /// <summary>
        /// The live draw window for a level. Authored through the last stage; past it the window
        /// keeps stepping up <see cref="TailWindowStep"/> tiers at every milestone, so the
        /// supply's value keeps growing (×3² per step) against the tail target's ×3⁴ — that gap,
        /// plus the under-neutral tail arena zoom, is the designed endgame squeeze.
        /// </summary>
        public TierWindow WindowForLevel(int level)
        {
            var window = GetStage(level).Window;
            if (!IsTailLevel(level)) return window;
            var shifts = (level - LastStage.FromLevel) / MilestoneEvery;
            return window.Shifted(TailWindowStep * shifts);
        }

        /// <summary>
        /// The score-bar target for a level. Authored stages are ANCHORS, not plateaus: between
        /// two anchors the target grows geometrically per level, and past the last anchor it keeps
        /// growing at <see cref="TailTargetGrowth"/> per level.
        ///
        /// Per-level growth matters because one Zone B drain can cross several levels in a burst —
        /// each crossed level must immediately raise the next bar, so a monster drain self-limits
        /// to a few celebratory level-ups instead of wrapping a flat plateau many times over.
        /// </summary>
        public double ScoreBarTargetForLevel(int level)
        {
            var last = LastStage;
            if (level >= last.FromLevel)
            {
                return Math.Round(last.ScoreBarTarget * Math.Pow(TailTargetGrowth, level - last.FromLevel));
            }

            var i = 0;
            while (i + 1 < _stages.Length && _stages[i + 1].FromLevel <= level) i++;
            var a = _stages[i];
            var b = _stages[i + 1];
            var t = (level - a.FromLevel) / (double)(b.FromLevel - a.FromLevel);
            return Math.Round(a.ScoreBarTarget * Math.Pow(b.ScoreBarTarget / a.ScoreBarTarget, t));
        }

        /// <summary>
        /// The environment palette active at a level: the last stage at-or-below the level that
        /// authors one, else <see cref="BasePalette"/>. Author-then-hold — past the last authored
        /// palette the look stays put, mirroring how window shifts self-heal.
        /// </summary>
        public string PaletteNameForLevel(int level)
        {
            var result = BasePalette;
            for (var i = 0; i < _stages.Length; i++)
            {
                if (_stages[i].FromLevel > level) break;
                if (!string.IsNullOrEmpty(_stages[i].Palette)) result = _stages[i].Palette;
            }
            return result;
        }
    }
}
