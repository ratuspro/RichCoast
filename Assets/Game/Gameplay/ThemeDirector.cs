using PrimeTween;
using RichCoast.Core;

namespace RichCoast.Game
{
    /// <summary>
    /// Scene-level owner of the milestone colour swap (port of <c>ThemeDirector.ts</c>).
    /// <c>ProgressionChanged</c> tells it which authored palette the run has earned
    /// (<see cref="ProgressionCurve.PaletteNameForLevel"/>); the next <c>ArenaZoom(true)</c> — only
    /// ever raised by the milestone zoom, whose input freeze we piggyback — cross-fades the active
    /// <see cref="Theme"/> there, raising <c>ThemeChanged</c> per tick so every bound surface
    /// restyles in step with the zoom. It never touches a zone: <see cref="Theme"/> is the shared
    /// data, the event is the repaint signal.
    /// </summary>
    public sealed class ThemeDirector
    {
        readonly ProgressionCurve curve;
        readonly GameFeelSO feel;
        string current = Palettes.WorkshopName;
        string target = Palettes.WorkshopName;
        Tween tween;

        public string CurrentPaletteName => current;

        public ThemeDirector(ProgressionCurve curve, GameFeelSO feel)
        {
            this.curve = curve;
            this.feel = feel;
            GameEvents.ProgressionChanged += e => target = curve.PaletteNameForLevel(e.Level);
            GameEvents.ArenaZoom += active => { if (active) BeginFade(); };
        }

        void BeginFade()
        {
            if (target == current) return;
            var from = Theme.Active;
            var to = Palettes.Get(target);
            current = target;

            tween.Stop();
            float seconds = feel.milestoneZoomMs / 1000f;
            if (seconds <= 0f)
            {
                Theme.Apply(to);
                GameEvents.RaiseThemeChanged();
                return;
            }
            tween = Tween.Custom(this, 0f, 1f, seconds, (self, t) =>
            {
                Theme.Apply(Palette.Lerp(from, to, t));
                GameEvents.RaiseThemeChanged();
            }, Ease.InOutSine).OnComplete(this, self =>
            {
                Theme.Apply(to);
                GameEvents.RaiseThemeChanged();
            }, warnIfTargetDestroyed: false);
        }
    }
}
