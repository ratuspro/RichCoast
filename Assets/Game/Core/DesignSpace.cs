using UnityEngine;

namespace RichCoast.Core
{
    /// <summary>
    /// The one place design space meets Unity world space.
    ///
    /// The whole design — Layout's bands, the spawn row, the death line, every tuned radius — is
    /// authored y-DOWN from the top-left corner, as the original build was. Unity is y-up. Rather
    /// than rewrite the tuning (and lose the ability to compare against the original numbers), the
    /// world simply mirrors Y: <c>worldY = -designY</c>. Everything else — X, distances, radii,
    /// speeds — is identical in both spaces, so only vertical positions ever need converting.
    ///
    /// One design unit is one world unit, so radii and speeds carry over untouched.
    /// </summary>
    public static class DesignSpace
    {
        public static Vector3 ToWorld(Vector2 design) => new Vector3(design.x, -design.y, 0f);

        public static Vector2 ToDesign(Vector3 world) => new Vector2(world.x, -world.y);

        public static float ToWorldY(float designY) => -designY;

        public static float ToDesignY(float worldY) => -worldY;

        /// <summary>Design-space velocity → world velocity (only Y flips).</summary>
        public static Vector2 VelocityToWorld(Vector2 design) => new Vector2(design.x, -design.y);

        /// <summary>World velocity → design-space velocity.</summary>
        public static Vector2 VelocityToDesign(Vector2 world) => new Vector2(world.x, -world.y);
    }
}
