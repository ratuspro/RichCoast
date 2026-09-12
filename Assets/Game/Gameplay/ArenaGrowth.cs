using System;
using PrimeTween;

namespace RichCoast.Game
{
    /// <summary>
    /// The milestone arena growth (port of <c>ArenaView.grow</c>) on the ONE camera. Zone A is a
    /// power-of-three merge board with no tier ceiling, so balls grow without bound; to make room the
    /// arena GROWS at milestones: <see cref="BoardGeometry.Scale"/> multiplies by the factor the
    /// caller computes (neutral ball-growth match × the stage's tightness), the tray is rebuilt with
    /// its walls/floor outward (always away from the balls, so no static-into-dynamic overlap), the
    /// balls' gravity is re-normalised to the new scale, and the camera's A framing zooms out
    /// (<see cref="CameraRig.ViewScale"/> tweened to the new scale) so balls keep their real physics
    /// size yet appear smaller and keep their relative positions. The funnel apex stays pinned at
    /// the Zone A/C boundary, so the arena only ever grows UP and OUT — never down into Zone B.
    /// Owned by Zone A; Zone C reacts to the <c>ArenaZoom</c> event for its input lock.
    /// </summary>
    public sealed class ArenaGrowth
    {
        readonly BoardGeometry geometry;
        readonly ArenaBuilder builder;
        readonly CameraRig rig;
        readonly Board board;
        readonly GameFeelSO feel;
        Tween tween;

        public bool IsAnimating { get; private set; }

        public ArenaGrowth(BoardGeometry geometry, ArenaBuilder builder, CameraRig rig, Board board, GameFeelSO feel)
        {
            this.geometry = geometry;
            this.builder = builder;
            this.rig = rig;
            this.board = board;
            this.feel = feel;
        }

        /// <summary>Grow the arena one milestone step by <paramref name="factor"/>; <paramref name="onComplete"/> fires when the camera lands.</summary>
        public void Grow(float factor, Action onComplete)
        {
            IsAnimating = true;
            geometry.SetScale(geometry.Scale * factor);
            builder.Build();
            board.OnArenaScaled();

            tween.Stop();
            float from = rig.ViewScale;
            float to = geometry.Scale;
            float seconds = feel.milestoneZoomMs / 1000f;
            tween = Tween.Custom(rig, from, to, seconds, (r, v) => r.ViewScale = v, Ease.InOutCubic)
                .OnComplete(() =>
                {
                    rig.ViewScale = to;
                    IsAnimating = false;
                    onComplete?.Invoke();
                }, warnIfTargetDestroyed: false); // a scene unload mid-zoom takes the rig with it
        }
    }
}
