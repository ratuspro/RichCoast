using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneB
{
    /// <summary>
    /// A drain: a sensor area that captures balls and turns them into score. A ball entering a ×M
    /// collector scores <c>value × M</c> and leaves play.
    ///
    /// The shipped layouts both use a single bottom collector fed by two funnel ramps, but nothing
    /// here assumes that — any number, anywhere, each with its own multiplier.
    /// </summary>
    public sealed class Collector : MonoBehaviour
    {
        private static readonly Color Paint = new Color(0.36f, 0.28f, 0.20f, 0.55f);

        public float ScoreMultiplier { get; private set; }

        public static Collector Create(Transform parent, CollectorDef def)
        {
            var go = new GameObject("Collector");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = PhysicsLayers.ZoneB;
            go.transform.position = DesignSpace.ToWorld(def.Center);

            var collector = go.AddComponent<Collector>();
            collector.ScoreMultiplier = def.ScoreMultiplier;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = def.Size;
            collider.isTrigger = true; // a sensor: it captures, it does not deflect

            var view = new GameObject("Mouth");
            view.transform.SetParent(go.transform, worldPositionStays: false);
            view.transform.localScale = new Vector3(def.Size.x, def.Size.y, 1f);
            PlaceholderArt.AttachRenderer(view, PlaceholderArt.Square, Paint, sortingOrder: 3);

            return collector;
        }
    }
}
