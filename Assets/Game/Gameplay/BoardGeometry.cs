using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone A's board in WORLD units (y-up): a 10-unit-wide tray whose funnel apex sits at the
    /// origin and whose ceiling is the design band height above it. Everything the Core tables
    /// author in design px is converted here, once, through <see cref="DesignSpace"/>.
    ///
    /// The <see cref="Scale"/> is the milestone arena-growth factor (M3): the tray grows UP and OUT
    /// around a fixed apex, never down, so the geometry is expressed as apex-relative × s.
    /// </summary>
    public sealed class BoardGeometry
    {
        /// <summary>Inward tilt of each half of the funnel floor (degrees) — a shallow V toward centre.</summary>
        public const float FloorFunnelDeg = 5f;

        public float Scale { get; private set; } = 1f;

        public static float Units(double designPx) => (float)DesignSpace.ToUnits(designPx);
        public static float Pixels(float units) => (float)DesignSpace.ToPixels(units);

        public float HalfWidth => Units(DesignSpace.Width / 2) * Scale;
        public float MinX => -HalfWidth;
        public float MaxX => HalfWidth;
        public float ApexY => 0f;
        public float CeilingY => Units(DesignSpace.BoardHeight) * Scale;
        public float SpawnY => CeilingY - Units(DesignSpace.SpawnY) * Scale;
        public float DeathLineY => CeilingY - Units(DesignSpace.DeathLineY) * Scale;
        public float WarnBand => Units(DesignSpace.WarnBand) * Scale;
        /// <summary>HUD chrome height above the ceiling, in units (the camera leaves this room for the bar).</summary>
        public float HudHeight => Units(DesignSpace.HudHeight);

        /// <summary>Funnel raised side corners: the ramp climbs this much from the apex to each wall.</summary>
        public float FunnelDrop => HalfWidth * Mathf.Tan(FloorFunnelDeg * Mathf.Deg2Rad);

        public Vector2 FloorLeft => new Vector2(MinX, ApexY + FunnelDrop);
        public Vector2 FloorApex => new Vector2(0f, ApexY);
        public Vector2 FloorRight => new Vector2(MaxX, ApexY + FunnelDrop);

        public void SetScale(float s) => Scale = s;

        /// <summary>Y of the funnel ramp surface directly under world-x <paramref name="x"/>.</summary>
        public float RampYAt(float x)
        {
            Vector2 a, b;
            if (x <= 0f) { a = FloorLeft; b = FloorApex; }
            else { a = FloorApex; b = FloorRight; }
            float t = Mathf.InverseLerp(a.x, b.x, x);
            return Mathf.Lerp(a.y, b.y, t);
        }

        /// <summary>A world y expressed as the Core tables' "design px below the band top".</summary>
        public double HeightFromTopPx(float worldY) => DesignSpace.ToPixels((CeilingY - worldY) / Scale);
    }
}
