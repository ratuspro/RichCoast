using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game.Dev
{
    /// <summary>
    /// MILESTONE 1 STAND-IN for Zones B + C (the Unity twin of the Phaser <c>?zone=ac</c> stub).
    /// Until the real trap-door + split arena land in M2, this fakes the score bar so the Zone A
    /// slice has a complete loop: when Zone A reports depletion (buffer spent + board settled) it
    /// "banks" the board's total value through a pessimistic ×<see cref="GateMultiplier"/> cascade,
    /// rolls the score bar through any levels it crosses (each crossing is a real
    /// <c>ScoreBarFilled</c> level-up that refills the buffer), and fires <c>ScoreHarvested</c> so the
    /// HUD's fly-up number plays. The balls STAY on the board (nothing is sucked out yet), so the
    /// tray fills up over the run and the death line / game over get exercised.
    /// Delete this file when M2 replaces it — nothing else references it.
    /// </summary>
    public sealed class ZoneBStub
    {
        public const double GateMultiplier = 4;
        const int MaxLevelsPerCashIn = 10;
        const float BankDelayMs = 600f;

        readonly Board board;
        readonly BoardGeometry geometry;
        readonly Camera camera;
        double total;
        double filled;
        double target = 20;
        float pendingMs = -1f;

        public ZoneBStub(Board board, BoardGeometry geometry, Camera camera)
        {
            this.board = board;
            this.geometry = geometry;
            this.camera = camera;
            GameEvents.ProgressionChanged += e => target = e.ScoreBarTarget;
            GameEvents.ZoneADepleted += () => pendingMs = 0f;
        }

        public void Tick(float deltaMs)
        {
            if (pendingMs < 0f) return;
            pendingMs += deltaMs;
            if (pendingMs < BankDelayMs) return;
            pendingMs = -1f;
            Bank();
        }

        void Bank()
        {
            double haul = board.TotalValue() * GateMultiplier;
            if (haul <= 0) return;
            GameEvents.RaiseZoneBBusy();
            total += haul;
            GameEvents.RaiseScoreChanged(total);

            filled += haul;
            int levels = 0;
            GameEvents.RaiseScoreBarChanged(filled, target);
            while (filled >= target && levels < MaxLevelsPerCashIn)
            {
                filled -= target;
                levels++;
                GameEvents.RaiseScoreBarFilled(); // synchronously raises ProgressionChanged → new target
            }
            if (filled >= target) filled = target * 0.99; // forfeit the overflow past the cap
            GameEvents.RaiseScoreBarChanged(filled, target);

            // Launch the fly-up from the tray's funnel apex (where Zone B's haul label will sit).
            var screen = camera.WorldToScreenPoint(new Vector3(0f, geometry.ApexY - 0.6f, 0f));
            GameEvents.RaiseScoreHarvested(new ScoreHarvestedEvent(haul, screen.x, screen.y));
            if (levels > 0) Sfx.Instance?.Goal();
            GameEvents.RaiseZoneBEmpty();
            GameEvents.RaiseScoreBarCashedIn();
        }
    }
}
