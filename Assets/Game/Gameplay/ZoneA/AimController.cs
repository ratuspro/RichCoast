using RichCoast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RichCoast.Gameplay.ZoneA
{
    /// <summary>
    /// Drag-to-aim, release-to-drop. Input is accepted anywhere on the screen — not only over the
    /// board — because on a phone the player's thumb naturally sits low, and forcing them to reach
    /// the top of a portrait screen to aim is the single worst thing this control scheme can do.
    ///
    /// Reports the aim column in DESIGN space; whether a drop is legal (buffer, phase, cooldown)
    /// is the system's call, not the input layer's.
    /// </summary>
    public sealed class AimController
    {
        private readonly Camera _camera;
        private float _cooldownMs;
        private bool _pressed;

        public AimController(Camera camera, float initialAimX)
        {
            _camera = camera;
            AimX = initialAimX;
        }

        /// <summary>Current aim column, design space.</summary>
        public float AimX { get; private set; }

        /// <summary>False while input is locked — during a pan, a milestone zoom, or the drop cooldown.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>True on the frame the player released a drag (or tapped) with input enabled.</summary>
        public bool ReleasedThisFrame { get; private set; }

        public void StartCooldown() => _cooldownMs = Tuning.DropCooldownMs;

        public void Tick(float deltaMs)
        {
            ReleasedThisFrame = false;
            _cooldownMs = Mathf.Max(0f, _cooldownMs - deltaMs);

            var pointer = Pointer.current;
            if (pointer == null) return;

            var down = pointer.press.isPressed;
            if (down) TrackAim(pointer.position.ReadValue());

            var released = _pressed && !down;
            _pressed = down;

            if (released && Enabled && _cooldownMs <= 0f) ReleasedThisFrame = true;
        }

        /// <summary>
        /// Follow the pointer's column only. Vertical movement is deliberately ignored: the drop
        /// row is fixed, so letting the ball track the finger's y would only add jitter.
        /// </summary>
        private void TrackAim(Vector2 screenPosition)
        {
            var world = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
            AimX = DesignSpace.ToDesign(world).x;
        }

        /// <summary>Snap the aim to a column, e.g. after a milestone re-frames the arena.</summary>
        public void SetAim(float designX) => AimX = designX;
    }
}
