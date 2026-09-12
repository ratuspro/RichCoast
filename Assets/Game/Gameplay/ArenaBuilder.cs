using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Builds Zone A's tray: static Box2D walls (ceiling, sides, the V funnel floor) plus the
    /// visible pine rails and paper backdrop, from <see cref="BoardGeometry"/>. Built once: the tray is
    /// fixed for the run (milestone growth shrinks the balls instead — see <see cref="ArenaGrowth"/>).
    /// Every painted surface is theme-bound, so palette cross-fades restyle it.
    /// </summary>
    public sealed class ArenaBuilder
    {
        public const int WallLayer = 9;
        /// <summary>Static-body wall thickness in units.</summary>
        const float WallThickness = 1.0f;
        const float RailWidth = 0.26f;

        readonly Transform root;
        readonly BoardGeometry geometry;
        readonly GameFeelSO feel;
        Transform walls;
        Transform visuals;
        PhysicsMaterial2D wallMaterial;
        PhysicsMaterial2D floorMaterial;

        public ArenaBuilder(Transform root, BoardGeometry geometry, GameFeelSO feel)
        {
            this.root = root;
            this.geometry = geometry;
            this.feel = feel;
        }

        public void Build()
        {
            if (walls != null) Object.Destroy(walls.gameObject);
            if (visuals != null) Object.Destroy(visuals.gameObject);
            walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root, false);

            wallMaterial ??= new PhysicsMaterial2D("wall") { friction = 0.3f, bounciness = 0.05f };
            floorMaterial ??= new PhysicsMaterial2D("floor") { friction = feel.floorFriction, bounciness = 0.05f };

            float t = WallThickness;
            float left = geometry.MinX, right = geometry.MaxX, top = geometry.CeilingY;
            float midY = (top + geometry.ApexY) / 2f;
            float spanY = top - geometry.ApexY + 2f * t;

            AddWall("Ceiling", new Vector2(0f, top + t / 2f), new Vector2(right - left + 2f * t, t), 0f, wallMaterial);
            AddWall("Left", new Vector2(left - t / 2f, midY), new Vector2(t, spanY), 0f, wallMaterial);
            AddWall("Right", new Vector2(right + t / 2f, midY), new Vector2(t, spanY), 0f, wallMaterial);
            AddFloorSegment("FloorLeft", geometry.FloorLeft, geometry.FloorApex, t);
            AddFloorSegment("FloorRight", geometry.FloorApex, geometry.FloorRight, t);

            BuildVisuals();
        }

        void AddWall(string name, Vector2 centre, Vector2 size, float angleDeg, PhysicsMaterial2D material)
        {
            var go = new GameObject(name) { layer = WallLayer };
            go.transform.SetParent(walls, false);
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;
            box.sharedMaterial = material;
        }

        /// <summary>One static rotated rectangle whose TOP edge runs from p0 to p1, extending downward.</summary>
        void AddFloorSegment(string name, Vector2 p0, Vector2 p1, float thickness)
        {
            var d = p1 - p0;
            float len = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var normal = new Vector2(d.y, -d.x) / len; // downward-pointing perpendicular
            var centre = (p0 + p1) / 2f + normal * (thickness / 2f);
            AddWall(name, centre, new Vector2(len + 0.2f, thickness), angle, floorMaterial);
        }

        void BuildVisuals()
        {
            // Paint below the apex stops at Zone C's divider.
            float below = geometry.ZoneCBottomY;
            // Paper band behind the tray (the "workbench top").
            WorldArt.Rect(visuals, "Band", geometry.MinX, geometry.MaxX, below, geometry.CeilingY + geometry.HudHeight, ThemeKey.PaperZoneA, -20);
            // Solid wood under the funnel V: a quad from the ramp edges down to the Zone C divider.
            WorldArt.Quad(visuals, "FunnelFill", new[]
            {
                new Vector2(geometry.MinX, geometry.FloorLeft.y),
                new Vector2(geometry.FloorApex.x, geometry.FloorApex.y),
                new Vector2(geometry.MaxX, geometry.FloorRight.y),
                new Vector2(geometry.MaxX, below),
                new Vector2(geometry.MinX, below),
            }, ThemeKey.Pine, -10);
            // Rails: side walls + the funnel V, a thick pine stroke with a dark seam.
            WorldArt.Rail(visuals, new Vector2(geometry.MinX, geometry.CeilingY), geometry.FloorLeft, RailWidth, ThemeKey.Pine, -8);
            WorldArt.Rail(visuals, new Vector2(geometry.MaxX, geometry.CeilingY), geometry.FloorRight, RailWidth, ThemeKey.Pine, -8);
            WorldArt.Rail(visuals, geometry.FloorLeft, geometry.FloorApex, RailWidth, ThemeKey.Pine, -8);
            WorldArt.Rail(visuals, geometry.FloorApex, geometry.FloorRight, RailWidth, ThemeKey.Pine, -8);
            float seam = RailWidth * 0.25f;
            WorldArt.Rail(visuals, new Vector2(geometry.MinX, geometry.CeilingY), geometry.FloorLeft, seam, ThemeKey.PineShadow, -7);
            WorldArt.Rail(visuals, new Vector2(geometry.MaxX, geometry.CeilingY), geometry.FloorRight, seam, ThemeKey.PineShadow, -7);
            WorldArt.Rail(visuals, geometry.FloorLeft, geometry.FloorApex, seam, ThemeKey.PineShadow, -7);
            WorldArt.Rail(visuals, geometry.FloorApex, geometry.FloorRight, seam, ThemeKey.PineShadow, -7);
        }
    }
}
