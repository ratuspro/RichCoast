using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Gameplay
{
    /// <summary>
    /// Owns the A↔B camera pan and the phase state it broadcasts.
    ///
    /// The world is taller than the screen and never moves; the camera pans between the two
    /// framings. The pure <see cref="PhaseMachine"/> decides WHEN, this decides how it looks, and
    /// every zone learns about it through <see cref="PhaseChanged"/>.
    ///
    /// </summary>
    public sealed class PhaseDirector : IGameSystem
    {
        /// <summary>Pan duration (ms). Long enough to read as a move, short enough not to nag.</summary>
        private const float PanMs = 450f;

        private readonly EventBus _bus;
        private readonly CameraRig _rig;
        private readonly float _panDistance;
        private readonly PhaseMachine _machine = new PhaseMachine();

        private float _pan;
        private float _panFrom;
        private float _panTo;
        private float _panElapsed = -1f;
        private bool _announced;

        public PhaseDirector(EventBus bus, CameraRig rig)
        {
            _bus = bus;
            _rig = rig;
            _panDistance = rig.PanDistance;
        }

        public GamePhase Phase => _machine.Phase;

        public void Create()
        {
            _bus.Subscribe<ZoneADepleted>(OnDepleted);
            _bus.Subscribe<ScoreBarCashedIn>(OnCashedIn);
            ApplyPan(0f);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ZoneADepleted>(OnDepleted);
            _bus.Unsubscribe<ScoreBarCashedIn>(OnCashedIn);
        }

        public void Tick(float deltaMs)
        {
            // Announced on the first tick rather than in Create(), so systems built after this one
            // still hear the opening phase.
            if (!_announced)
            {
                _announced = true;
                _bus.Emit(new PhaseChanged(_machine.Phase));
            }

            if (_panElapsed < 0f) return;

            _panElapsed += deltaMs;
            var t = Mathf.Clamp01(_panElapsed / PanMs);
            ApplyPan(Mathf.Lerp(_panFrom, _panTo, Smooth(t)));

            if (t < 1f) return;

            _panElapsed = -1f;
            Step(PhaseInput.PanDone);
        }

        private void OnDepleted(ZoneADepleted _) => Step(PhaseInput.Depleted);

        private void OnCashedIn(ScoreBarCashedIn _) => Step(PhaseInput.BarFilled);

        private void Step(PhaseInput input)
        {
            var step = _machine.Step(input);

            if (step.StartPan.HasValue)
            {
                _panFrom = _pan;
                _panTo = step.StartPan.Value == GamePhase.B ? _panDistance : 0f;
                _panElapsed = 0f;
            }

            if (step.Changed) _bus.Emit(new PhaseChanged(step.Phase));
        }

        /// <summary>Hand the pan to the rig, which re-frames both cameras from it.</summary>
        private void ApplyPan(float pan)
        {
            _pan = pan;
            _rig.Pan = pan;
        }

        /// <summary>Smoothstep: the pan should ease at both ends, never snap.</summary>
        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
