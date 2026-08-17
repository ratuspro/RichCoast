using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneB
{
    /// <summary>
    /// A multiplier bar. A ball touching it is replaced by <see cref="Multiplier"/> copies of the
    /// same value, which may go on to hit further gates — that cascade is where the run's score
    /// actually comes from.
    ///
    /// Static gates never move; translating and rotating ones animate from the pure
    /// <see cref="GateDef.PoseAt"/>, so their motion is testable without a physics world and a
    /// kinematic body carries it into the simulation.
    /// </summary>
    public sealed class Gate : MonoBehaviour
    {
        private static readonly Color HighPaint = new Color(0.30f, 0.60f, 0.35f);  // green sign
        private static readonly Color LowPaint = new Color(0.79f, 0.60f, 0.20f);   // brass sign

        private GateDef _def;
        private Rigidbody2D _body;
        private float _elapsedMs;

        public int Multiplier => _def.Multiplier;

        /// <summary>The bar's current centre in design space — split copies are placed relative to it.</summary>
        public Vector2 DesignCenter => DesignSpace.ToDesign(transform.position);

        public static Gate Create(Transform parent, GateDef def)
        {
            var go = new GameObject($"Gate x{def.Multiplier}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = PhysicsLayers.ZoneB;

            var gate = go.AddComponent<Gate>();
            gate._def = def;

            var body = go.AddComponent<Rigidbody2D>();
            // Kinematic: gates push balls around and are never pushed back, and a moving gate must
            // sweep its contacts rather than teleport through them.
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            gate._body = body;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(def.Length, Tuning.GateThickness);

            // Signs read at a glance: green for the big multipliers, brass for the rest.
            var view = new GameObject("Sign");
            view.transform.SetParent(go.transform, worldPositionStays: false);
            view.transform.localScale = new Vector3(def.Length, Tuning.GateThickness, 1f);
            PlaceholderArt.AttachRenderer(view, PlaceholderArt.Square,
                def.Multiplier >= 4 ? HighPaint : LowPaint, sortingOrder: 4);

            gate.ApplyPose(0f);
            return gate;
        }

        /// <summary>Advance any motion this gate has. Static gates do nothing here.</summary>
        public void Tick(float deltaMs)
        {
            if (_def.Kind == GateKind.Static) return;
            _elapsedMs += deltaMs;
            ApplyPose(_elapsedMs);
        }

        private void ApplyPose(float elapsedMs)
        {
            var (center, angle) = _def.PoseAt(elapsedMs);
            var position = DesignSpace.ToWorld(center);
            // Design space is mirrored in Y, so a design-space angle turns the other way on screen.
            var rotation = -angle * Mathf.Rad2Deg;

            // Driven from Update rather than FixedUpdate, so this sets the body's pose directly;
            // MovePosition would silently queue for a physics step that has already run.
            if (_body != null)
            {
                _body.position = position;
                _body.rotation = rotation;
            }
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotation));
        }
    }
}
