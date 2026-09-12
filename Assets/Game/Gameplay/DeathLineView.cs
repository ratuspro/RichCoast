using PrimeTween;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The red overflow threshold line. Hidden by default — it only surfaces, pulsing, when a ball
    /// rests close to it (driven by the board's danger detection). Idempotent so the pulse isn't
    /// restarted every frame the danger persists.
    /// </summary>
    public sealed class DeathLineView
    {
        const float Thickness = 0.05f;
        readonly SpriteRenderer line;
        readonly BoardGeometry geometry;
        Tween pulse;
        bool danger;

        public DeathLineView(Transform parent, BoardGeometry geometry)
        {
            this.geometry = geometry;
            var go = new GameObject("DeathLine");
            go.transform.SetParent(parent, false);
            line = go.AddComponent<SpriteRenderer>();
            line.sprite = BallArt.WhitePixel;
            line.color = Theme.Danger;
            Themed.Bind(line, ThemeKey.Danger);
            line.sortingOrder = 5;
            line.enabled = false;
            Reposition();
        }

        public void Reposition()
        {
            line.transform.position = new Vector3(0f, geometry.DeathLineY, 0f);
            line.transform.localScale = new Vector3(geometry.MaxX - geometry.MinX, Thickness, 1f);
        }

        public void SetDanger(bool on)
        {
            if (on == danger) return;
            danger = on;
            pulse.Stop();
            if (on)
            {
                line.enabled = true;
                pulse = Tween.Alpha(line, 0.9f, 0.25f, 0.5f, Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
            }
            else
            {
                line.enabled = false;
            }
        }
    }
}
