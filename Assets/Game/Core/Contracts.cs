using System;

namespace RichCoast.Core
{
    /// <summary>
    /// THE SEAM between the zones.
    ///
    /// Zones couple only through the payload types and <see cref="IGameSystem"/> declared here —
    /// never by calling into each other's code. Changing anything in this file affects every
    /// zone, so treat it as frozen unless the change is deliberate.
    ///
    /// Kept free of MonoBehaviour/scene types so the seam unit-tests without a play loop.
    /// </summary>
    public static class Tiers
    {
        /// <summary>
        /// Size of the base tier table — the radius table has this many entries. It is NOT a
        /// gameplay ceiling: merges are uncapped, so tiers climb past this, with balls cycling
        /// materials (modulo) and growing by formula beyond the table. Tier 1 is the smallest ball.
        /// </summary>
        public const int TierCount = 10;

        /// <summary>
        /// Tier (1-based) → ball value: 3^(tier-1). tier 1→1, 2→3, 3→9, 4→27, …
        /// Merges only join two equal balls and yield 1.5*(V+V) = 3V, so each merge triples the
        /// value — making the value ladder powers of three.
        /// </summary>
        public static double TierToValue(int tier) => Math.Pow(3.0, tier - 1);
    }

    /// <summary>
    /// A ball's identity, independent of where it lives. Shared by the drop payload and by the
    /// data carried on every ball body, so any system can read a ball's identity off a query.
    /// </summary>
    public readonly struct BallSpec
    {
        public readonly double Value;
        public readonly int Tier;

        public BallSpec(double value, int tier)
        {
            Value = value;
            Tier = tier;
        }

        public static BallSpec FromTier(int tier) => new BallSpec(Tiers.TierToValue(tier), tier);
    }

    /// <summary>
    /// The two exclusive play phases and the pan transitions between them.
    /// A = drop balls in Zone A; B = tap Zone C's trap-door to feed Zone B;
    /// AToB / BToA = the camera pan is animating and ALL input is locked.
    /// </summary>
    public enum GamePhase
    {
        A,
        AToB,
        B,
        BToA,
    }

    // -----------------------------------------------------------------------
    // Cross-zone events. One struct per event; the bus is keyed by these types,
    // so there are no event-name strings anywhere in the game.
    // -----------------------------------------------------------------------

    /// <summary>Zone C → Zone B: trap-door fired; Zone B spawns one ball at entry <see cref="X"/>.</summary>
    public readonly struct BallDropped
    {
        public readonly BallSpec Ball;

        /// <summary>
        /// Horizontal entry into Zone B, in world space. Chosen by the player: Zone C runs a
        /// marker that sweeps left↔right across the trap-door band and a tap freezes it — the
        /// marker's column at that instant is the entry. Inset so a ball never spawns into a
        /// side wall. WHERE a ball enters is a timing skill, not a fixed column.
        /// </summary>
        public readonly float X;

        public BallDropped(BallSpec ball, float x)
        {
            Ball = ball;
            X = x;
        }
    }

    /// <summary>Zone B → Zone C: ≥1 ball in flight; Zone C locks the trap-door.</summary>
    public readonly struct ZoneBBusy { }

    /// <summary>Zone B → Zone C: no balls in flight; Zone C may re-arm the trap-door.</summary>
    public readonly struct ZoneBEmpty { }

    /// <summary>Zone B → HUD: running cumulative score total changed.</summary>
    public readonly struct ScoreChanged
    {
        public readonly double Total;
        public ScoreChanged(double total) => Total = total;
    }

    /// <summary>Zone B → HUD: score bar progress changed (drives the fill bar visual).</summary>
    public readonly struct ScoreBarChanged
    {
        public readonly double Filled;
        public readonly double Target;

        public ScoreBarChanged(double filled, double target)
        {
            Filled = filled;
            Target = target;
        }
    }

    /// <summary>
    /// Zone B → all: one score-bar level was cashed in; Zone A advances a level and refills.
    /// Fires once PER LEVEL during a multi-level roll-through (the bar can cross several targets
    /// in one drain), so Zone A may receive a burst of these before the pan up.
    /// </summary>
    public readonly struct ScoreBarFilled { }

    /// <summary>
    /// Zone B → PhaseDirector: the cash-in has FULLY resolved (the whole multi-level roll
    /// finished). This — not <see cref="ScoreBarFilled"/> — is the pan-up trigger, so the roll
    /// plays out entirely in the B framing before the camera leaves Zone B.
    /// </summary>
    public readonly struct ScoreBarCashedIn { }

    /// <summary>
    /// Zone B → HUD: a B-round's accumulated haul is banked and cashing in. Carries the amount
    /// to add to the shown total and the screen-space launch point, so the HUD flies a number
    /// token from there to the score total and increments it on landing. This — not
    /// <see cref="ScoreChanged"/> — drives the HUD number, so it freezes during a B round and
    /// jumps only when the haul lands.
    /// </summary>
    public readonly struct ScoreHarvested
    {
        public readonly double Amount;
        public readonly float X;
        public readonly float Y;

        public ScoreHarvested(double amount, float x, float y)
        {
            Amount = amount;
            X = x;
            Y = y;
        }
    }

    /// <summary>Zone A → HUD: ball buffer count changed.</summary>
    public readonly struct BallBufferChanged
    {
        public readonly int Count;
        public BallBufferChanged(int count) => Count = count;
    }

    /// <summary>Zone A → all: internal level advanced; carries the new stage parameters.</summary>
    public readonly struct ProgressionChanged
    {
        public readonly int Level;
        public readonly int MinTier;
        public readonly int MaxTier;
        public readonly int BufferCapacity;
        public readonly double ScoreBarTarget;

        public ProgressionChanged(int level, int minTier, int maxTier, int bufferCapacity, double scoreBarTarget)
        {
            Level = level;
            MinTier = minTier;
            MaxTier = maxTier;
            BufferCapacity = bufferCapacity;
            ScoreBarTarget = scoreBarTarget;
        }
    }

    /// <summary>Zone A → Zone C: a milestone arena zoom-out is animating; lock input while active.</summary>
    public readonly struct ArenaZoom
    {
        public readonly bool Active;
        public ArenaZoom(bool active) => Active = active;
    }

    /// <summary>
    /// PhaseDirector → all: the gameplay phase (or pan transition) changed. Zone A aims only in
    /// A, Zone C's trap-door arms only in B; both stay locked through the pan states.
    /// </summary>
    public readonly struct PhaseChanged
    {
        public readonly GamePhase Phase;
        public PhaseChanged(GamePhase phase) => Phase = phase;
    }

    /// <summary>
    /// Zone A → PhaseDirector: ball buffer empty AND the board has settled — begin the pan down
    /// into the Zone B phase.
    /// </summary>
    public readonly struct ZoneADepleted { }

    /// <summary>The run ended (overflow or stalemate); carries the final score.</summary>
    public readonly struct GameOver
    {
        public readonly double FinalScore;
        public readonly int Level;

        public GameOver(double finalScore, int level)
        {
            FinalScore = finalScore;
            Level = level;
        }
    }

    /// <summary>
    /// Every zone (and stub/harness) implements this so the bootstrap can wire them all
    /// identically: build, tick, tear down. A system talks to other systems ONLY via the event
    /// bus — never by importing or calling another zone's code.
    /// </summary>
    public interface IGameSystem : IDisposable
    {
        void Create();

        /// <param name="deltaMs">Frame time in milliseconds, matching the ported tuning units.</param>
        void Tick(float deltaMs);
    }
}
