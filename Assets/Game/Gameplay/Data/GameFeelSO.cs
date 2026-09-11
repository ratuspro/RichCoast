using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// THE feel-tuning file. Every knob that changes how the board plays or how juice reads lives
    /// here, editable live in play mode. Lengths are world units (10 = board width) unless noted.
    /// </summary>
    [CreateAssetMenu(menuName = "RichCoast/Game Feel", fileName = "GameFeel")]
    public sealed class GameFeelSO : ScriptableObject
    {
        [Header("Physics")]
        [Tooltip("Gravity scale on every ball (world gravity is -9.81). Higher = snappier drops.")]
        public float gravityScale = 2.5f;
        [Tooltip("Linear damping on balls (air friction).")]
        public float linearDamping = 0.15f;
        [Tooltip("Angular damping on balls.")]
        public float angularDamping = 0.6f;
        [Tooltip("Funnel floor friction (contacts combine as the smaller value → governs sliding toward the apex).")]
        public float floorFriction = 0.02f;
        [Tooltip("Hard per-frame speed cap (units/s) — the anti-tunnel backstop.")]
        public float maxBallSpeed = 24f;

        [Header("Merge blast")]
        [Tooltip("Neighbours within this distance of a merge get nudged outward.")]
        public float blastRadius = 1.6f;
        [Tooltip("Peak outward velocity kick at the merge point (units/s), linear falloff to 0 at the radius.")]
        public float blastStrength = 2.6f;

        [Header("Overflow / game over")]
        [Tooltip("A ball slower than this (units/s) counts as resting.")]
        public float restSpeed = 1.2f;
        [Tooltip("How long a ball must rest above the death line before the run ends (ms).")]
        public float restMs = 1000f;

        [Header("Input")]
        [Tooltip("Lock-out after a drop so the next ball can't be slammed into the same spot (ms).")]
        public float dropCooldownMs = 250f;

        [Header("Squash & stretch")]
        [Tooltip("Landing squash: x-stretch amount at the strongest impact (0.2 = 120% wide, ~83% tall).")]
        public float landSquash = 0.22f;
        [Tooltip("Impact speed (units/s) at which landSquash is fully applied.")]
        public float landSquashFullSpeed = 12f;
        [Tooltip("Minimum impact speed (units/s) that triggers a squash at all.")]
        public float landSquashMinSpeed = 2.5f;
        public float landSquashMs = 140f;
        [Tooltip("Merge-birth pop: the merged ball scales from this fraction up to 1 with a back-ease.")]
        public float birthScaleFrom = 0.55f;
        public float birthPopMs = 260f;
        [Tooltip("Merge punch: neighbours briefly scale by this when the blast hits them.")]
        public float blastPunch = 0.1f;
        [Tooltip("Idle wobble: subtle breathing scale amplitude on resting balls (0 = off).")]
        public float idleWobble = 0.012f;
        public float idleWobbleHz = 0.9f;

        [Header("Merge burst")]
        public int burstParticles = 12;
        public float burstSpeedMin = 1.2f;
        public float burstSpeedMax = 3.4f;
        public float burstLifeMs = 380f;
        [Tooltip("Flash ring: expands from the merged ball's radius to this multiple while fading.")]
        public float flashRingScale = 1.9f;
        public float flashRingMs = 240f;

        [Header("Audio")]
        [Tooltip("Merges closer together than this (ms) climb in pitch.")]
        public float comboWindowMs = 1500f;
        public int comboMaxStep = 8;
        [Range(0f, 1f)] public float masterVolume = 0.6f;

        [Header("Haptics")]
        [Tooltip("Light tap on any merge (ms / amplitude 1-255).")]
        public int mergeHapticMs = 12;
        public int mergeHapticAmp = 90;
        [Tooltip("Tier at which the heavier merge haptic kicks in.")]
        public int heavyHapticFromTier = 5;
        public int heavyHapticMs = 28;
        public int heavyHapticAmp = 200;
        public int dropHapticMs = 6;
        public int dropHapticAmp = 45;

        [Header("HUD")]
        [Tooltip("Buffer refill: one slot every this many ms (≤10 slots); larger refills tighten the gap.")]
        public float bufferTickMs = 130f;
        public float bufferTickMinMs = 55f;
        [Tooltip("Score fly-up: flight time from the harvest point to the HUD total (ms).")]
        public float scoreFlyMs = 620f;
        public float scoreCountUpMs = 420f;
    }
}
