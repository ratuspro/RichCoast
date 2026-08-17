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
    /// Milestone note: the original ran two cameras (a Zone A arena viewport plus the main camera)
    /// so Zone A stayed partly visible during the B phase. That split lands with the real Zone B;
    /// for now a single camera pans the full distance, which is behaviourally identical from every
    /// system's point of view.
    /// </summary>
    public sealed class PhaseDirector : IGameSystem
    {
        /// <summary>Pan duration (ms). Long enough to read as a move, short enough not to nag.</summary>
        private const float PanMs = 450f;

        private readonly EventBus _bus;
        private readonly Camera _camera;
        private readonly float _panDistance;
        private readonly PhaseMachine _machine = new PhaseMachine();

        private float _pan;
        private float _panFrom;
        private float _panTo;
        private float _panElapsed = -1f;

        public PhaseDirector(EventBus bus, Camera camera, float panDistance)
        {
            _bus = bus;
            _camera = camera;
            _panDistance = panDistance;
        }

        public GamePhase Phase => _machine.Phase;

        public void Create()
        {
            _bus.Subscribe<ZoneADepleted>(OnDepleted);
            _bus.Subscribe<ScoreBarCashedIn>(OnCashedIn);
            ApplyPan(0f);
            _bus.Emit(new PhaseChanged(_machine.Phase));
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ZoneADepleted>(OnDepleted);
            _bus.Unsubscribe<ScoreBarCashedIn>(OnCashedIn);
        }

        public void Tick(float deltaMs)
        {
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

        /// <summary>Move the camera down by the pan; design space is y-down, so the world y falls.</summary>
        private void ApplyPan(float pan)
        {
            _pan = pan;
            var position = _camera.transform.position;
            _camera.transform.position = new Vector3(position.x, DesignSpace.ToWorldY(BaseCenterY() + pan), position.z);
        }

        private float BaseCenterY() => _camera.orthographicSize;

        /// <summary>Smoothstep: the pan should ease at both ends, never snap.</summary>
        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
