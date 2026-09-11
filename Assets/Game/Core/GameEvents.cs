using System;

namespace RichCoast.Core
{
    /// <summary>A ball's identity, independent of where it lives.</summary>
    public readonly struct BallSpec
    {
        public readonly int Tier;
        public readonly double Value;

        public BallSpec(int tier)
        {
            Tier = tier;
            Value = TierMath.ValueForTier(tier);
        }
    }

    /// <summary>Zone C → Zone B: a ball entered Zone B at design-space column <see cref="X"/>.</summary>
    public readonly struct BallDroppedEvent
    {
        public readonly BallSpec Ball;
        public readonly double X;

        public BallDroppedEvent(BallSpec ball, double x)
        {
            Ball = ball;
            X = x;
        }
    }

    /// <summary>Zone B → HUD: a round's haul is banked; fly it from the given screen point to the total.</summary>
    public readonly struct ScoreHarvestedEvent
    {
        public readonly double Amount;
        public readonly double ScreenX;
        public readonly double ScreenY;

        public ScoreHarvestedEvent(double amount, double screenX, double screenY)
        {
            Amount = amount;
            ScreenX = screenX;
            ScreenY = screenY;
        }
    }

    /// <summary>Zone A → all: the internal level advanced; carries the new stage parameters.</summary>
    public readonly struct ProgressionChangedEvent
    {
        public readonly int Level;
        public readonly int MinTier;
        public readonly int MaxTier;
        public readonly int BufferCapacity;
        public readonly double ScoreBarTarget;

        public ProgressionChangedEvent(int level, int minTier, int maxTier, int bufferCapacity, double scoreBarTarget)
        {
            Level = level;
            MinTier = minTier;
            MaxTier = maxTier;
            BufferCapacity = bufferCapacity;
            ScoreBarTarget = scoreBarTarget;
        }
    }

    /// <summary>The two exclusive play phases and the pan transitions between them.</summary>
    public enum GamePhase { A, AToB, B, BToA }

    /// <summary>
    /// THE SEAM between the game's halves — the typed replacement for the Phaser string event bus.
    /// Zones never reference each other; they publish and subscribe here. Static so any system can
    /// reach it without wiring, and <see cref="Reset"/> clears every subscriber on a restart /
    /// between tests. Payload names and meanings are frozen per TECH_SPEC.md.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Zone C → Zone B: trap-door fired; Zone B spawns one ball.</summary>
        public static event Action<BallDroppedEvent> BallDropped;
        /// <summary>Zone B → Zone C: ≥1 ball in flight; Zone C locks the trap-door.</summary>
        public static event Action ZoneBBusy;
        /// <summary>Zone B → Zone C: no balls in flight; Zone C may re-arm.</summary>
        public static event Action ZoneBEmpty;
        /// <summary>Zone B → all: the live lifetime total (feeds Zone A's game-over mirror, not the HUD number).</summary>
        public static event Action<double> ScoreChanged;
        /// <summary>Zone B → A: one score-bar level was cashed in (fires once PER LEVEL in a roll-through).</summary>
        public static event Action ScoreBarFilled;
        /// <summary>Zone B → PhaseDirector: the whole cash-in roll finished — the pan-up trigger.</summary>
        public static event Action ScoreBarCashedIn;
        /// <summary>Zone B → HUD: score-bar fill changed (filled, target).</summary>
        public static event Action<double, double> ScoreBarChanged;
        /// <summary>Zone B → HUD: a round's haul is banked; THIS drives the HUD number.</summary>
        public static event Action<ScoreHarvestedEvent> ScoreHarvested;
        /// <summary>Zone A → HUD: balls left to drop changed.</summary>
        public static event Action<int> BallBufferChanged;
        /// <summary>Zone A → all: internal level advanced.</summary>
        public static event Action<ProgressionChangedEvent> ProgressionChanged;
        /// <summary>Zone A → Zone C: milestone arena zoom-out animating (true) / landed (false).</summary>
        public static event Action<bool> ArenaZoom;
        /// <summary>PhaseDirector → all: the gameplay phase (or pan transition) changed.</summary>
        public static event Action<GamePhase> PhaseChanged;
        /// <summary>Zone A → PhaseDirector: buffer empty AND board settled — pan down.</summary>
        public static event Action ZoneADepleted;
        /// <summary>Zone A → all: the run ended (final score). Replaces the Phaser build's in-zone overlay wiring.</summary>
        public static event Action<double> GameOver;

        public static void RaiseBallDropped(BallDroppedEvent e) => BallDropped?.Invoke(e);
        public static void RaiseZoneBBusy() => ZoneBBusy?.Invoke();
        public static void RaiseZoneBEmpty() => ZoneBEmpty?.Invoke();
        public static void RaiseScoreChanged(double total) => ScoreChanged?.Invoke(total);
        public static void RaiseScoreBarFilled() => ScoreBarFilled?.Invoke();
        public static void RaiseScoreBarCashedIn() => ScoreBarCashedIn?.Invoke();
        public static void RaiseScoreBarChanged(double filled, double target) => ScoreBarChanged?.Invoke(filled, target);
        public static void RaiseScoreHarvested(ScoreHarvestedEvent e) => ScoreHarvested?.Invoke(e);
        public static void RaiseBallBufferChanged(int count) => BallBufferChanged?.Invoke(count);
        public static void RaiseProgressionChanged(ProgressionChangedEvent e) => ProgressionChanged?.Invoke(e);
        public static void RaiseArenaZoom(bool active) => ArenaZoom?.Invoke(active);
        public static void RaisePhaseChanged(GamePhase phase) => PhaseChanged?.Invoke(phase);
        public static void RaiseZoneADepleted() => ZoneADepleted?.Invoke();
        public static void RaiseGameOver(double finalScore) => GameOver?.Invoke(finalScore);

        /// <summary>Drop every subscriber — call on scene reload and between tests so handlers never leak.</summary>
        public static void Reset()
        {
            BallDropped = null;
            ZoneBBusy = null;
            ZoneBEmpty = null;
            ScoreChanged = null;
            ScoreBarFilled = null;
            ScoreBarCashedIn = null;
            ScoreBarChanged = null;
            ScoreHarvested = null;
            BallBufferChanged = null;
            ProgressionChanged = null;
            ArenaZoom = null;
            PhaseChanged = null;
            ZoneADepleted = null;
            GameOver = null;
        }
    }
}
