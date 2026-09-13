using PrimeTween;
using RichCoast.Core;

namespace RichCoast.Game
{
    /// <summary>
    /// Scene-level choreographer of the two-phase flow (port of <c>PhaseDirector.ts</c>). Listens for
    /// the two triggers — <c>ZoneADepleted</c> (A → pan down) and <c>ScoreBarCashedIn</c> (B → pan up,
    /// fired once Zone B's whole roll-through has finished) — drives the one camera's pan through
    /// <see cref="CameraRig.Pan"/>, and broadcasts <c>PhaseChanged</c> so each zone applies its own
    /// input lock. The state lives in the pure <see cref="PhaseMachine"/>. Never calls into a zone.
    /// </summary>
    public sealed class PhaseDirector
    {
        readonly CameraRig rig;
        readonly GameFeelSO feel;
        PhaseState state = PhaseMachine.Initial;
        Tween tween;

        public GamePhase Phase => state.Phase;

        public PhaseDirector(CameraRig rig, GameFeelSO feel)
        {
            this.rig = rig;
            this.feel = feel;
            GameEvents.ZoneADepleted += () => Step(PhaseInput.Depleted);
            GameEvents.ScoreBarCashedIn += () => Step(PhaseInput.BarFilled);
        }

        /// <summary>Announce the initial phase — call LAST, after every system has subscribed.</summary>
        public void Start()
        {
            rig.Pan = 0f;
            GameEvents.RaisePhaseChanged(state.Phase);
        }

        void Step(PhaseInput input)
        {
            var result = PhaseMachine.Step(state, input);
            state = result.State;
            if (result.StartPan.HasValue) StartPan(result.StartPan.Value == GamePhase.B ? 1f : 0f);
            if (result.Changed) GameEvents.RaisePhaseChanged(state.Phase);
        }

        void StartPan(float target)
        {
            // The FSM never overlaps pans (a turnaround waits for PanDone), but a stale tween would
            // fight the rig — stop defensively.
            tween.Stop();
            float from = rig.Pan;
            float seconds = feel.panMs / 1000f;
            // The pan's own cue: a soft glide (down into Zone B, up back to Zone A) and a light tap.
            Sfx.Instance?.Pan(up: target < from);
            Haptics.Pulse(feel.panHapticMs, feel.panHapticAmp);
            tween = Tween.Custom(rig, from, target, seconds, (r, v) => r.Pan = v, Ease.InOutSine)
                .OnComplete(this, self =>
                {
                    self.rig.Pan = target;
                    self.Step(PhaseInput.PanDone);
                }, warnIfTargetDestroyed: false); // a scene unload mid-pan takes the rig with it
        }
    }
}
