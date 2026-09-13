using System;
using System.Collections.Generic;
using System.Globalization;

namespace RichCoast.Core
{
    /// <summary>
    /// Round-trip-safe number text for the save file.
    /// <para>JsonUtility does NOT round-trip large doubles: 987654321987.65432 comes back as
    /// 987654321987.65442, losing about four digits. Scores reach 3^19 ≈ 1.16e9 per ball and compound
    /// from there, so every MONEY value is stored as "G17" text — the shortest guaranteed-exact form —
    /// and parsed back on read. Positions and the arena scale are left as native numbers: their
    /// magnitudes are small and sub-pixel drift there is invisible.</para>
    /// </summary>
    public static class SaveNum
    {
        public static string Format(double v) => v.ToString("G17", CultureInfo.InvariantCulture);

        public static double Parse(string s) =>
            !string.IsNullOrEmpty(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : 0;
    }

    /// <summary>One board ball in a snapshot, in DESIGN PX — x across the board, y below the ceiling.</summary>
    [Serializable]
    public sealed class BallSpawn
    {
        public int tier;
        public double x;
        public double yFromTop;
    }

    /// <summary>
    /// The DURABLE half of a run — everything needed to rebuild it, and nothing tween-bound.
    /// Deliberately excludes velocities, the phase and Zone B's arena seed: the arena reshuffles on
    /// every drain, and balls are only ever captured at rest, so both would be noise.
    /// <para>The money fields are lowercase PROPERTIES over "…Text" string fields (see
    /// <see cref="SaveNum"/>); JsonUtility serialises the fields, callers use the properties.</para>
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        public int level = 1;
        public int ballBuffer;
        public float arenaScale = 1f;
        public int currentTier = 1;
        public int nextTier = 1;
        public List<BallSpawn> board = new List<BallSpawn>();

        public string scoreText = "0";
        public string barFilledText = "0";
        public string barTargetText = "1";
        public string zoneBTotalText = "0";

        public double score { get => SaveNum.Parse(scoreText); set => scoreText = SaveNum.Format(value); }
        public double barFilled { get => SaveNum.Parse(barFilledText); set => barFilledText = SaveNum.Format(value); }
        public double barTarget { get => SaveNum.Parse(barTargetText); set => barTargetText = SaveNum.Format(value); }
        public double zoneBTotal { get => SaveNum.Parse(zoneBTotalText); set => zoneBTotalText = SaveNum.Format(value); }
    }

    /// <summary>Lifetime bests. Survives a discarded run — a corrupt snapshot must never cost a best score.</summary>
    [Serializable]
    public sealed class Records
    {
        public int bestLevel;
        public int runsPlayed;
        public string bestScoreText = "0";

        public double bestScore { get => SaveNum.Parse(bestScoreText); set => bestScoreText = SaveNum.Format(value); }

        /// <summary>
        /// Fold a finished run in. Returns true only when the SCORE is a new best — that return drives
        /// the game-over flourish, so beating the best LEVEL alone deliberately does not trigger it.
        /// </summary>
        public bool Merge(double score, int level)
        {
            runsPlayed += 1;
            if (level > bestLevel) bestLevel = level;
            if (score <= bestScore) return false;
            bestScore = score;
            return true;
        }
    }

    /// <summary>The player's persisted preferences. Each one has exactly one sink in the Game layer.</summary>
    [Serializable]
    public sealed class Settings
    {
        public bool soundOn = true;
        public bool hapticsOn = true;
    }

    /// <summary>
    /// The whole save. The three payloads are INDEPENDENT: a run that fails validation is dropped on
    /// its own and records/settings still load. Losing an interrupted run is a shrug; losing a best
    /// score because the run blob had a bad ball position is not.
    /// <para><see cref="hasRun"/> exists because JsonUtility cannot represent a null nested class —
    /// it writes <c>{}</c> and reads back a default instance, so <c>run != null</c> is meaningless.
    /// Always ask <see cref="hasRun"/>.</para>
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int schemaVersion = SaveSchema.Current;
        public Records records = new Records();
        public Settings settings = new Settings();
        public bool hasRun;
        public RunSnapshot run = new RunSnapshot();

        public void ClearRun()
        {
            hasRun = false;
            run = new RunSnapshot();
        }

        public void SetRun(RunSnapshot snapshot)
        {
            run = snapshot ?? new RunSnapshot();
            hasRun = snapshot != null;
        }
    }
}
