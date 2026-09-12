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

        [Header("Zone B arena")]
        [Tooltip("Gravity scale on the small Zone B balls (they fall a long way — lighter than Zone A reads better).")]
        public float zoneBGravityScale = 1.6f;
        [Tooltip("Base bounciness of Zone B balls (× the tier material's restitution feel, capped at 0.5).")]
        public float zoneBBounce = 0.35f;
        [Tooltip("Base friction of Zone B balls (× the tier material's friction feel).")]
        public float zoneBFriction = 0.05f;
        public float zoneBLinearDamping = 0.1f;
        [Tooltip("Hard speed cap for Zone B balls (units/s).")]
        public float zoneBMaxSpeed = 30f;
        [Tooltip("Bounciness of gate slabs.")]
        public float gateBounce = 0.4f;
        [Tooltip("Split fan: total angular spread of the copies (radians).")]
        public float splitSpread = 0.8f;
        [Tooltip("Split fan: outward kick given to each copy (units/s).")]
        public float splitKick = 4.5f;
        [Tooltip("Freshly-split copies ignore gates for this long so they don't re-trigger instantly (ms).")]
        public float splitGraceMs = 300f;
        [Tooltip("Safety cap: beyond this many balls in flight, gates stop splitting (balls just bounce).")]
        public int maxBallsInFlight = 48;
        [Tooltip("A Zone B ball resting this long without draining gets a small random nudge (ms). Keeps the door from locking forever.")]
        public float stuckNudgeMs = 2500f;

        [Header("Zone C trap-door")]
        [Tooltip("One edge→edge leg of the marker sweep (ms). The difficulty knob for column timing.")]
        public float sweepMs = 880f;
        [Tooltip("Suck: the grabbed ball slides to the door mouth in this long (ms).")]
        public float suckMs = 150f;
        [Tooltip("Pop: the ball re-inflates at the Zone B entry in this long (ms).")]
        public float popMs = 110f;

        [Header("Phase pan")]
        [Tooltip("Camera pan between the A and B framings (ms). Snappier than a reward beat — it's a scene change.")]
        public float panMs = 650f;

        [Header("Milestone")]
        [Tooltip("Arena zoom-out (and the palette cross-fade that rides it) at a draw-window milestone (ms).")]
        public float milestoneZoomMs = 1200f;
        [Tooltip("Blacklist drain: how long each obsolete ball slides from Zone A down into Zone B (ms).")]
        public float drainMs = 280f;

        [Header("Score bar")]
        [Tooltip("Per-16ms fraction the shown fill closes toward the logical value (glide, not snap).")]
        public float barFillLerp = 0.2f;
        [Tooltip("A live level wrap sweeps the bar to full in this long before snapping empty (ms).")]
        public float wrapFillMs = 150f;
        [Tooltip("Beat the settled final bar holds after the last ball drains, before the pan up (ms).")]
        public float settleDwellMs = 350f;

        [Header("HUD")]
        [Tooltip("Buffer refill: one slot every this many ms (≤10 slots); larger refills tighten the gap.")]
        public float bufferTickMs = 130f;
        public float bufferTickMinMs = 55f;
        [Tooltip("Score fly-up: flight time from the harvest point to the HUD total (ms).")]
        public float scoreFlyMs = 620f;
        public float scoreCountUpMs = 420f;
    }
}
