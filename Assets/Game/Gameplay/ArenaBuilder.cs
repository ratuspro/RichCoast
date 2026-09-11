using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Builds Zone A's tray: static Box2D walls (ceiling, sides, the V funnel floor) plus the
    /// visible pine rails and paper backdrop, from <see cref="BoardGeometry"/>. Rebuildable at a new
    /// scale for the M3 milestone growth (walls only ever move outward).
    /// </summary>
    public sealed class ArenaBuilder
    {
        public const int WallLayer = 9;
        /// <summary>Static-body wall thickness in units (scaled with the arena so fast balls never tunnel).</summary>
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

            float t = WallThickness * geometry.Scale;
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
            float s = geometry.Scale;
            // Paper band behind the tray (the "workbench top").
            var band = Rect("Band", geometry.MinX, geometry.MaxX, geometry.ApexY - geometry.FunnelDrop - 1.5f * s, geometry.CeilingY + geometry.HudHeight, Theme.PaperZoneA, -20);
            band.name = "Band";
            // Solid wood under the funnel V: a quad from the ramp edges down past the apex.
            Quad("FunnelFill", new[]
            {
                new Vector2(geometry.MinX, geometry.FloorLeft.y),
                new Vector2(geometry.FloorApex.x, geometry.FloorApex.y),
                new Vector2(geometry.MaxX, geometry.FloorRight.y),
                new Vector2(geometry.MaxX, geometry.ApexY - 1.2f * s),
                new Vector2(geometry.MinX, geometry.ApexY - 1.2f * s),
            }, Theme.Pine, -10);
            // Rails: side walls + the funnel V, a thick pine stroke with a dark seam.
            Rail(new Vector2(geometry.MinX, geometry.CeilingY), geometry.FloorLeft, RailWidth * s, Theme.Pine, -8);
            Rail(new Vector2(geometry.MaxX, geometry.CeilingY), geometry.FloorRight, RailWidth * s, Theme.Pine, -8);
            Rail(geometry.FloorLeft, geometry.FloorApex, RailWidth * s, Theme.Pine, -8);
            Rail(geometry.FloorApex, geometry.FloorRight, RailWidth * s, Theme.Pine, -8);
            float seam = RailWidth * 0.25f * s;
            Rail(new Vector2(geometry.MinX, geometry.CeilingY), geometry.FloorLeft, seam, Theme.PineShadow, -7);
            Rail(new Vector2(geometry.MaxX, geometry.CeilingY), geometry.FloorRight, seam, Theme.PineShadow, -7);
            Rail(geometry.FloorLeft, geometry.FloorApex, seam, Theme.PineShadow, -7);
            Rail(geometry.FloorApex, geometry.FloorRight, seam, Theme.PineShadow, -7);
        }

        GameObject Rect(string name, float x0, float x1, float y0, float y1, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(visuals, false);
            go.transform.position = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
            go.transform.localScale = new Vector3(x1 - x0, y1 - y0, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallArt.WhitePixel;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        void Rail(Vector2 a, Vector2 b, float width, Color color, int order)
        {
            var d = b - a;
            var go = new GameObject("Rail");
            go.transform.SetParent(visuals, false);
            go.transform.position = (a + b) / 2f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(d.magnitude + width, width, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallArt.WhitePixel;
            sr.color = color;
            sr.sortingOrder = order;
        }

        void Quad(string name, Vector2[] points, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(visuals, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = name };
            var verts = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) verts[i] = points[i];
            var tris = new int[(points.Length - 2) * 3];
            for (int i = 0; i < points.Length - 2; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2;
                tris[i * 3 + 2] = i + 1;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };
            mr.sortingOrder = order;
        }
    }
}
