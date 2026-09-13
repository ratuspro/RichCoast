using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Inspector port of <see cref="ZoneBGenParams"/>: the grammar Zone B's arena is generated from,
    /// re-rolled every time the arena drains. Every distance is design px with y measured DOWN from
    /// the Zone B band top (390 × 687); the Zone B ball has a 10 px radius.
    /// <see cref="ToParams"/> builds the engine-free record the generator reads.
    /// </summary>
    [CreateAssetMenu(menuName = "RichCoast/Zone B Arena", fileName = "ZoneBArena")]
    public sealed class ZoneBArenaSO : ScriptableObject
    {
        [Header("Row depths (design px from the band top)")]
        [Tooltip("The barrier row — every ordinary drop is guaranteed to hit a gate here.")]
        public Vector2 row1Y = new Vector2(160f, 185f);
        public Vector2 row2Y = new Vector2(330f, 365f);
        public Vector2 row3Y = new Vector2(485f, 515f);

        [Header("Openings")]
        [Tooltip("Barrier-row cracks. Kept NARROWER than a ball (20 px) so the mouth is the only way through.")]
        public Vector2 crackWidth = new Vector2(10f, 16f);
        [Tooltip("Passable gaps in the two lower rows.")]
        public Vector2 gapWidth = new Vector2(44f, 64f);
        [Tooltip("Shortest gate a row may produce.")]
        public float minGateLen = 28f;
        [Tooltip("Vertical divider dropped from each crack so a ball cannot perch in it.")]
        public float dividerDrop = 90f;

        [Header("Golden path")]
        [Tooltip("The mouth's aperture. Must clear a ball diameter (20) plus the chute clearance.")]
        public float goldenMouthWidth = 32f;
        [Tooltip("Payout of the one gilded gate (inclusive). Every other gate stays in 2..4.")]
        public Vector2Int goldenMultiplier = new Vector2Int(6, 8);
        public float goldenGateLength = 60f;
        [Tooltip("How far below the barrier row the gilded gate hangs.")]
        public Vector2 goldenGateDrop = new Vector2(100f, 130f);
        [Tooltip("Half-width of the chute, measured to the rail CENTRES.")]
        public float chuteHalfWidth = 19f;
        [Tooltip("Clear air a ball keeps either side while falling through the chute. Raise it if drops clip a rail.")]
        public float chuteClearance = 2f;
        [Tooltip("How far the chute rails rise above the barrier row.")]
        public float chuteRise = 24f;

        [Header("Density")]
        public Vector2Int row2Gaps = new Vector2Int(2, 3);
        public Vector2Int row3Gaps = new Vector2Int(2, 3);
        public Vector2Int diagonals = new Vector2Int(2, 4);
        public Vector2 diagonalRun = new Vector2(40f, 95f);

        [Tooltip("Re-rolls allowed before the generator gives up on a seed.")]
        public int maxRerolls = 24;

        public ZoneBGenParams ToParams() => new ZoneBGenParams
        {
            Row1YMin = row1Y.x, Row1YMax = row1Y.y,
            Row2YMin = row2Y.x, Row2YMax = row2Y.y,
            Row3YMin = row3Y.x, Row3YMax = row3Y.y,
            CrackMin = crackWidth.x, CrackMax = crackWidth.y,
            GapMin = gapWidth.x, GapMax = gapWidth.y,
            MinGateLen = minGateLen,
            DividerDrop = dividerDrop,
            GoldenMouthWidth = goldenMouthWidth,
            GoldenMultiplierMin = goldenMultiplier.x,
            GoldenMultiplierMax = goldenMultiplier.y,
            GoldenGateLength = goldenGateLength,
            GoldenGateDropMin = goldenGateDrop.x,
            GoldenGateDropMax = goldenGateDrop.y,
            ChuteHalfWidth = chuteHalfWidth,
            ChuteClearance = chuteClearance,
            ChuteRise = chuteRise,
            Row2GapsMin = row2Gaps.x, Row2GapsMax = row2Gaps.y,
            Row3GapsMin = row3Gaps.x, Row3GapsMax = row3Gaps.y,
            DiagonalsMin = diagonals.x, DiagonalsMax = diagonals.y,
            DiagonalRunMin = diagonalRun.x, DiagonalRunMax = diagonalRun.y,
            MaxRerolls = maxRerolls,
        };
    }
}
