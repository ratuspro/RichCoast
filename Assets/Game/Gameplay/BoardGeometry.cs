using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone A's board in WORLD units (y-up): a 10-unit-wide tray whose funnel apex sits at the
    /// origin and whose ceiling is the design band height above it. Everything the Core tables
    /// author in design px is converted here, once, through <see cref="DesignSpace"/>.
    ///
    /// The tray is FIXED for the whole run. Milestone "arena growth" is realised by shrinking the
    /// balls in place (<see cref="BallFactory.ArenaScale"/>) rather than growing the tray and zooming
    /// the camera out — the same on-screen result as the Phaser build's zoom, on one camera, with
    /// Zone C/B untouched and the physics bodies staying inside Box2D's comfortable size range.
    /// </summary>
    public sealed class BoardGeometry
    {
        /// <summary>Inward tilt of each half of the funnel floor (degrees) — a shallow V toward centre.</summary>
        public const float FloorFunnelDeg = 5f;

        public static float Units(double designPx) => (float)DesignSpace.ToUnits(designPx);
        public static float Pixels(float units) => (float)DesignSpace.ToPixels(units);

        public float HalfWidth => Units(DesignSpace.Width / 2);
        public float MinX => -HalfWidth;
        public float MaxX => HalfWidth;
        public float ApexY => 0f;
        public float CeilingY => Units(DesignSpace.BoardHeight);
        public float SpawnY => CeilingY - Units(DesignSpace.SpawnY);
        public float DeathLineY => CeilingY - Units(DesignSpace.DeathLineY);
        public float WarnBand => Units(DesignSpace.WarnBand);
        /// <summary>HUD chrome height above the ceiling, in units (the camera leaves this room for the bar).</summary>
        public float HudHeight => Units(DesignSpace.HudHeight);

        /// <summary>Funnel raised side corners: the ramp climbs this much from the apex to each wall.</summary>
        public float FunnelDrop => HalfWidth * Mathf.Tan(FloorFunnelDeg * Mathf.Deg2Rad);

        public Vector2 FloorLeft => new Vector2(MinX, ApexY + FunnelDrop);
        public Vector2 FloorApex => new Vector2(0f, ApexY);
        public Vector2 FloorRight => new Vector2(MaxX, ApexY + FunnelDrop);

        // --- Zones C and B: fixed bands hanging below the funnel apex ----------------------------

        /// <summary>Zone C (the trap-door band) runs from the apex down to this y.</summary>
        public float ZoneCBottomY => -Units(DesignSpace.ZoneCHeight);
        /// <summary>Centre line of the door band — where the sweep markers sit.</summary>
        public float DoorMouthY => ZoneCBottomY / 2f;
        public float ZoneBTopY => ZoneCBottomY;
        public float ZoneBBottomY => ZoneBTopY - Units(DesignSpace.ZoneBHeight);
        /// <summary>Zone B's side walls (the design width).</summary>
        public float ZoneBMinX => -Units(DesignSpace.Width / 2);
        public float ZoneBMaxX => Units(DesignSpace.Width / 2);
        public float ZoneBBallRadius => Units(DesignSpace.ZoneBBallRadius);

        /// <summary>A design-space x (0..390) as a world x.</summary>
        public float DesignXToWorld(double designX) => Units(designX - DesignSpace.Width / 2);
        /// <summary>A world x as a design-space column (0..390).</summary>
        public double WorldXToDesign(float x) => DesignSpace.ToPixels(x) + DesignSpace.Width / 2;
        /// <summary>A Zone B layout point (design px, y DOWN from the band top) as a world position.</summary>
        public Vector2 ZoneBToWorld(double designX, double designYFromTop) =>
            new Vector2(DesignXToWorld(designX), ZoneBTopY - Units(designYFromTop));

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
        public double HeightFromTopPx(float worldY) => DesignSpace.ToPixels(CeilingY - worldY);
    }
}
