using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneB
{
    /// <summary>
    /// The playfield's static barriers: guide rails, gate dividers, the funnel ramps, and the
    /// outer boundary that keeps a ball from leaving through the sides.
    ///
    /// Walls carry no score and no gate behaviour — they exist to make the cascade's outcome
    /// layout-driven and readable rather than random.
    /// </summary>
    public static class ZoneBWalls
    {
        private static readonly Color RailColor = new Color(0.72f, 0.60f, 0.44f);
        private static readonly Color FillColor = new Color(0.62f, 0.50f, 0.35f);

        /// <summary>Thickness of the outer boundary; comfortably more than a ball travels per step.</summary>
        private const float BoundaryThickness = 40f;

        public static void Build(Transform parent, ZoneBLayout layout)
        {
            foreach (var wall in layout.Walls)
            {
                BuildSegment(parent, wall);
                if (wall.FillBelow) BuildRampFill(parent, wall);
            }

            BuildBoundary(parent);
        }

        /// <summary>One rail: a box rotated to span its two endpoints.</summary>
        private static void BuildSegment(Transform parent, WallDef wall)
        {
            var delta = wall.To - wall.From;
            var length = delta.magnitude;
            if (length <= 0f) return;

            var center = (wall.From + wall.To) * 0.5f;
            // Design space is mirrored in Y, so the on-screen angle is the negated design angle.
            var angle = -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            var thickness = wall.Thickness > 0f ? wall.Thickness : WallDef.DefaultThickness;

            Box(parent, "Rail", center, new Vector2(length, thickness), angle, RailColor, sortingOrder: 2);
        }

        /// <summary>
        /// Fill the wedge under a funnel ramp with solid material. Without it a ball can settle in
        /// the dead corner beside the ramp and never drain, which would hang the whole round.
        /// </summary>
        private static void BuildRampFill(Transform parent, WallDef wall)
        {
            var bottom = Layout.ZoneB.Bottom;
            var outerX = wall.From.x <= Layout.Width * 0.5f ? Layout.ZoneB.X : Layout.ZoneB.Right;
            var minX = Mathf.Min(Mathf.Min(wall.From.x, wall.To.x), outerX);
            var maxX = Mathf.Max(Mathf.Max(wall.From.x, wall.To.x), outerX);
            var top = Mathf.Max(wall.From.y, wall.To.y);

            var size = new Vector2(maxX - minX, bottom - top);
            if (size.x <= 0f || size.y <= 0f) return;

            var center = new Vector2((minX + maxX) * 0.5f, (top + bottom) * 0.5f);
            Box(parent, "Ramp Fill", center, size, 0f, FillColor, sortingOrder: 1);
        }

        /// <summary>Sides and bottom of the zone, so a stray ball cannot leave the world.</summary>
        private static void BuildBoundary(Transform parent)
        {
            var zone = Layout.ZoneB;
            var half = BoundaryThickness * 0.5f;
            var midY = zone.Y + zone.Height * 0.5f;

            Box(parent, "Boundary Left", new Vector2(zone.X - half, midY),
                new Vector2(BoundaryThickness, zone.Height), 0f, RailColor, sortingOrder: 0);
            Box(parent, "Boundary Right", new Vector2(zone.Right + half, midY),
                new Vector2(BoundaryThickness, zone.Height), 0f, RailColor, sortingOrder: 0);
            Box(parent, "Boundary Bottom", new Vector2(zone.CenterX, zone.Bottom + half),
                new Vector2(zone.Width + BoundaryThickness * 2f, BoundaryThickness), 0f, RailColor, sortingOrder: 0);
        }

        private static void Box(Transform parent, string name, Vector2 designCenter, Vector2 size, float angleDegrees, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = PhysicsLayers.ZoneB;
            go.transform.SetPositionAndRotation(DesignSpace.ToWorld(designCenter), Quaternion.Euler(0f, 0f, angleDegrees));

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;

            // The sprite is a child so its scaling can never reach the collider.
            var view = new GameObject("Paint");
            view.transform.SetParent(go.transform, worldPositionStays: false);
            view.transform.localScale = new Vector3(size.x, size.y, 1f);
            PlaceholderArt.AttachRenderer(view, PlaceholderArt.Square, color, sortingOrder);
        }
    }
}
