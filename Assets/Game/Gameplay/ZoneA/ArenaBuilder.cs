using RichCoast.Core;
using RichCoast.View;
using UnityEngine;

namespace RichCoast.Gameplay.ZoneA
{
    /// <summary>
    /// Zone A's physical boundary — ceiling, two side walls, floor — plus the placeholder chrome
    /// that draws them and the death-line warning.
    ///
    /// Rebuilt (never re-created) whenever the arena scale changes at a milestone: the same four
    /// colliders are repositioned, so a zoom costs no allocation. The floor is the fixed anchor;
    /// the arena only ever grows upward and outward, never into Zone B.
    /// </summary>
    public sealed class ArenaBuilder : IArenaView
    {
        private static readonly Color WallColor = new Color(0.72f, 0.60f, 0.44f);
        private static readonly Color DeathLineColor = new Color(0.85f, 0.24f, 0.20f);

        private readonly Transform _parent;
        private readonly Wall _ceiling;
        private readonly Wall _left;
        private readonly Wall _right;
        private readonly Wall _floor;
        private readonly SpriteRenderer _deathLine;

        private ArenaGeometry _arena;

        public ArenaBuilder(Transform parent, ArenaGeometry arena)
        {
            _parent = parent;
            _arena = arena;

            _ceiling = new Wall("Ceiling", parent);
            _left = new Wall("Wall Left", parent);
            _right = new Wall("Wall Right", parent);
            _floor = new Wall("Floor", parent);

            var lineGo = new GameObject("Death Line");
            lineGo.transform.SetParent(parent, worldPositionStays: false);
            _deathLine = PlaceholderArt.AttachRenderer(lineGo, PlaceholderArt.Square, DeathLineColor, sortingOrder: 5);
            _deathLine.enabled = false;

            SetArenaScale(arena.Scale);
        }

        public void SetArenaScale(float scale)
        {
            _arena = new ArenaGeometry(scale);
            var t = _arena.ScaledWallThickness;
            var interiorWidth = _arena.Width;
            var interiorHeight = _arena.Height;

            // Each wall sits fully OUTSIDE the interior, so the interior stays exactly the arena.
            _ceiling.SetRect(_arena.CenterX, _arena.CeilingY - t * 0.5f, interiorWidth + t * 2f, t);
            _floor.SetRect(_arena.CenterX, _arena.FloorY + t * 0.5f, interiorWidth + t * 2f, t, Tuning.FloorFriction);
            _left.SetRect(_arena.MinX - t * 0.5f, _arena.CeilingY + interiorHeight * 0.5f, t, interiorHeight);
            _right.SetRect(_arena.MaxX + t * 0.5f, _arena.CeilingY + interiorHeight * 0.5f, t, interiorHeight);

            _deathLine.transform.position = DesignSpace.ToWorld(new Vector2(_arena.CenterX, _arena.DeathLineY));
            _deathLine.transform.localScale = new Vector3(interiorWidth, 2f * scale, 1f);
        }

        public void SetDeathLineWarning(bool warning) => _deathLine.enabled = warning;

        /// <summary>One static boundary: a box collider and the sprite that shows it.</summary>
        private sealed class Wall
        {
            private readonly Transform _transform;
            private readonly BoxCollider2D _collider;
            private PhysicsMaterial2D _surface;

            public Wall(string name, Transform parent)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, worldPositionStays: false);
                go.layer = parent.gameObject.layer;
                _transform = go.transform;
                _collider = go.AddComponent<BoxCollider2D>();
                PlaceholderArt.AttachRenderer(go, PlaceholderArt.Square, WallColor, sortingOrder: 0);
            }

            /// <param name="friction">
            /// Surface friction; the floor uses a low value so smaller balls slide toward the
            /// funnel apex instead of parking where they land.
            /// </param>
            public void SetRect(float designCenterX, float designCenterY, float width, float height, float friction = -1f)
            {
                _transform.position = DesignSpace.ToWorld(new Vector2(designCenterX, designCenterY));
                // Both the unit sprite and the unit collider ride the transform scale, so one
                // scale sets the wall's real size and its drawn size together — they cannot drift.
                _transform.localScale = new Vector3(width, height, 1f);
                _collider.size = Vector2.one;

                if (friction >= 0f)
                {
                    _surface ??= new PhysicsMaterial2D("ArenaFloor") { hideFlags = HideFlags.HideAndDontSave };
                    _surface.friction = friction;
                    _surface.bounciness = 0f;
                    _collider.sharedMaterial = _surface;
                }
            }
        }
    }
}
