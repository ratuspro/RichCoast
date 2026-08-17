using RichCoast.Core;
using UnityEngine;

namespace RichCoast.View
{
    /// <summary>
    /// How gameplay talks to whatever is drawing a ball. Gameplay pushes data — tier, radius,
    /// position — and never reaches for a renderer, a material or a sprite, so the placeholder
    /// look shipped during the migration can be replaced by the real "Bright Workshop" materials
    /// (or a fully 3D treatment) without a single gameplay edit.
    /// </summary>
    public interface IBallView
    {
        /// <summary>Adopt a tier's identity: material, colour, size. Called on spawn and on merge.</summary>
        void SetTier(int tier, float radius, TierMaterial material);

        /// <summary>Follow the simulation. Called once per rendered frame, never per physics step.</summary>
        void SetPose(Vector2 position, float rotationDegrees);

        /// <summary>Show or hide without destroying — every ball is pooled.</summary>
        void SetVisible(bool visible);

        /// <summary>One-shot feedback when this ball is the result of a merge.</summary>
        void PlayMergePop();
    }

    /// <summary>
    /// The Zone A arena's chrome: walls, funnel, death line. Driven by the arena scale, which the
    /// milestone zoom animates.
    /// </summary>
    public interface IArenaView
    {
        /// <summary>Rebuild the boundary geometry for an arena scale (1 = the base arena).</summary>
        void SetArenaScale(float scale);

        /// <summary>Red overflow warning: a slow ball is inside the warning band below the line.</summary>
        void SetDeathLineWarning(bool warning);
    }

    /// <summary>The run's readouts. One interface so the HUD can be swapped or stubbed in tests.</summary>
    public interface IHudView
    {
        void SetScore(double total);
        void SetBuffer(int count);
        void SetNextBall(int tier, TierMaterial material);
        void SetScoreBar(double filled, double target);
        void ShowGameOver(double finalScore, int level);
    }
}
