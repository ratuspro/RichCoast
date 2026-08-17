using RichCoast.Core;
using UnityEngine;

namespace RichCoast.View
{
    /// <summary>
    /// Placeholder ball look: one tinted disc sprite, sized by the tier's radius, with a small
    /// scale punch on merge so chains stay readable.
    ///
    /// This is the throwaway half of the view seam — gameplay only ever calls
    /// <see cref="IBallView"/>, so replacing this with the real material treatment (or a lit 3D
    /// mesh) touches no gameplay code.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FlatBallView : MonoBehaviour, IBallView
    {
        private const float PopScale = 1.18f;
        private const float PopSeconds = 0.14f;

        private SpriteRenderer _renderer;
        private float _diameter = 1f;
        private float _popRemaining;

        /// <summary>Build a ready-to-use ball view; the caller parents and positions it.</summary>
        public static FlatBallView Create(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            PlaceholderArt.AttachRenderer(go, PlaceholderArt.Disc, Color.white, sortingOrder);
            return go.AddComponent<FlatBallView>();
        }

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        public void SetTier(int tier, float radius, TierMaterial material)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _diameter = radius * 2f;
            _renderer.color = material.Def.BaseColor;
            // A wrapped tier (past the end of the ladder) reuses a colour, so the cycle has to read
            // somehow even in grey-box: brighten it a step per completed lap.
            if (material.Cycle > 0)
            {
                _renderer.color = Color.Lerp(_renderer.color, Color.white, Mathf.Min(0.35f * material.Cycle, 0.7f));
            }
            ApplyScale();
        }

        public void SetPose(Vector2 position, float rotationDegrees)
        {
            var t = transform;
            t.position = DesignSpace.ToWorld(position);
            // Design space is mirrored in Y, so a design-space rotation reads reversed on screen.
            t.localRotation = Quaternion.Euler(0f, 0f, -rotationDegrees);
        }

        public void SetVisible(bool visible)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.enabled = visible;
        }

        public void PlayMergePop() => _popRemaining = PopSeconds;

        private void LateUpdate()
        {
            if (_popRemaining <= 0f) return;
            _popRemaining = Mathf.Max(0f, _popRemaining - Time.deltaTime);
            ApplyScale();
        }

        private void ApplyScale()
        {
            // Ease out from the punch back to the true size; at rest this is exactly the diameter,
            // so the view never disagrees with the collider.
            var t = _popRemaining / PopSeconds;
            var punch = 1f + (PopScale - 1f) * t * t;
            transform.localScale = Vector3.one * (_diameter * punch);
        }
    }
}
