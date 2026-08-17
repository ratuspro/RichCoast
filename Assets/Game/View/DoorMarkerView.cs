using RichCoast.Core;
using UnityEngine;

namespace RichCoast.View
{
    /// <summary>
    /// Placeholder trap-door chrome: the nine boundary columns as a row of pips, with the swept
    /// one lit. The player reads the sweep from which pip is bright, so the lit/dim contrast is
    /// the only thing here that actually matters for play.
    /// </summary>
    public sealed class DoorMarkerView : MonoBehaviour
    {
        private static readonly Color Dim = new Color(0.72f, 0.60f, 0.44f, 0.45f);
        private static readonly Color Lit = new Color(0.95f, 0.74f, 0.25f);
        private static readonly Color Frozen = new Color(1f, 0.98f, 0.86f);

        private const float PipSize = 12f;

        private SpriteRenderer[] _pips;
        private int _current = -1;

        public static DoorMarkerView Create(Transform parent, float columnMargin)
        {
            var go = new GameObject("Trap Door");
            go.transform.SetParent(parent, worldPositionStays: false);
            var view = go.AddComponent<DoorMarkerView>();
            view.Build(columnMargin);
            return view;
        }

        private void Build(float columnMargin)
        {
            _pips = new SpriteRenderer[DoorSweep.Columns];
            var y = Layout.ZoneC.Y + Layout.ZoneC.Height * 0.5f;

            for (var i = 0; i < _pips.Length; i++)
            {
                var pip = new GameObject($"Column {i}");
                pip.transform.SetParent(transform, worldPositionStays: false);
                pip.transform.position = DesignSpace.ToWorld(new Vector2(DoorSweep.ColumnX(i, columnMargin), y));
                pip.transform.localScale = Vector3.one * PipSize;
                _pips[i] = PlaceholderArt.AttachRenderer(pip, PlaceholderArt.Disc, Dim, sortingOrder: 6);
            }
        }

        /// <summary>Light the swept column and dim the rest.</summary>
        public void SetColumn(int index)
        {
            if (index == _current) return;
            _current = index;
            for (var i = 0; i < _pips.Length; i++) _pips[i].color = i == index ? Lit : Dim;
        }

        /// <summary>Flash the column the player froze — the one the ball will enter through.</summary>
        public void SetFrozen(int index)
        {
            _current = -1;
            for (var i = 0; i < _pips.Length; i++) _pips[i].color = i == index ? Frozen : Dim;
        }

        /// <summary>Dim the whole row while the door is locked, so a dead tap is visibly dead.</summary>
        public void SetArmed(bool armed)
        {
            if (armed) return;
            if (_current == -1) return;
            _current = -1;
            for (var i = 0; i < _pips.Length; i++) _pips[i].color = Dim;
        }
    }
}
