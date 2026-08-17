using System;
using UnityEngine;

namespace RichCoast.Core
{
    public enum GateKind
    {
        /// <summary>A fixed bar: a centre and an angle. Most are near-horizontal.</summary>
        Static,

        /// <summary>Slides back and forth between two endpoints on a fixed period; angle holds.</summary>
        Translating,

        /// <summary>Spins continuously around its centre at a fixed angular speed.</summary>
        Rotating,
    }

    /// <summary>
    /// One gate: a rigid bar that splits any ball touching it into <see cref="Multiplier"/> copies
    /// of the same value. Ported from the original <c>zoneB/zoneLayout.ts</c>, which is why one
    /// struct carries all three kinds' fields — the layouts are authored data, and a flat record
    /// keeps them readable as a table.
    /// </summary>
    [Serializable]
    public struct GateDef
    {
        public GateKind Kind;

        /// <summary>Centre (static, rotating) or the A endpoint (translating).</summary>
        public Vector2 Center;

        /// <summary>The B endpoint of a translating gate's travel.</summary>
        public Vector2 To;

        /// <summary>Radians; 0 = horizontal. Ignored by rotating gates, which drive their own.</summary>
        public float Angle;

        public float Length;

        /// <summary>A ball hitting this gate becomes this many balls of the same value.</summary>
        public int Multiplier;

        /// <summary>Milliseconds for one full A→B→A cycle (translating gates).</summary>
        public float PeriodMs;

        /// <summary>Radians per millisecond; positive = clockwise (rotating gates).</summary>
        public float SpeedRadPerMs;

        public static GateDef StaticGate(float cx, float cy, float angle, float length, int multiplier) =>
            new GateDef
            {
                Kind = GateKind.Static,
                Center = new Vector2(cx, cy),
                Angle = angle,
                Length = length,
                Multiplier = multiplier,
            };

        public static GateDef TranslatingGate(float ax, float ay, float bx, float by, float angle, float length, int multiplier, float periodMs) =>
            new GateDef
            {
                Kind = GateKind.Translating,
                Center = new Vector2(ax, ay),
                To = new Vector2(bx, by),
                Angle = angle,
                Length = length,
                Multiplier = multiplier,
                PeriodMs = periodMs,
            };

        public static GateDef RotatingGate(float cx, float cy, float length, int multiplier, float speedRadPerMs) =>
            new GateDef
            {
                Kind = GateKind.Rotating,
                Center = new Vector2(cx, cy),
                Length = length,
                Multiplier = multiplier,
                SpeedRadPerMs = speedRadPerMs,
            };

        /// <summary>
        /// Where the gate sits and how it is turned at a given age. Pure, so the motion is
        /// unit-testable without a physics world.
        /// </summary>
        public (Vector2 center, float angle) PoseAt(float elapsedMs)
        {
            switch (Kind)
            {
                case GateKind.Translating:
                {
                    // Ping-pong across the period: a sine would ease at the ends, which reads as
                    // the gate hesitating exactly where a ball is most likely to arrive.
                    var period = Mathf.Max(1f, PeriodMs);
                    var t = Mathf.PingPong(elapsedMs / (period * 0.5f), 1f);
                    return (Vector2.Lerp(Center, To, t), Angle);
                }
                case GateKind.Rotating:
                    return (Center, Angle + SpeedRadPerMs * elapsedMs);
                default:
                    return (Center, Angle);
            }
        }
    }

    /// <summary>A sensor that captures balls and turns them into score.</summary>
    [Serializable]
    public struct CollectorDef
    {
        /// <summary>Top-left corner in design space.</summary>
        public Vector2 Position;

        public Vector2 Size;

        /// <summary>A ball entering scores <c>value × this</c>.</summary>
        public float ScoreMultiplier;

        public CollectorDef(float x, float y, float width, float height, float scoreMultiplier)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(width, height);
            ScoreMultiplier = scoreMultiplier;
        }

        public Vector2 Center => Position + Size * 0.5f;
    }

    /// <summary>A static line segment: a physical barrier with no gate behaviour and no score.</summary>
    [Serializable]
    public struct WallDef
    {
        public Vector2 From;
        public Vector2 To;
        public float Thickness;

        /// <summary>Fill the area between this rail and Zone B's bottom (the solid funnel ramps).</summary>
        public bool FillBelow;

        public const float DefaultThickness = 6f;

        public WallDef(float x1, float y1, float x2, float y2, float thickness = DefaultThickness, bool fillBelow = false)
        {
            From = new Vector2(x1, y1);
            To = new Vector2(x2, y2);
            Thickness = thickness;
            FillBelow = fillBelow;
        }
    }

    /// <summary>
    /// One complete Zone B playfield: stacked multiplier shelves split by guide rails, funnelling
    /// into a bottom collector. Outcomes must feel layout-driven and readable — the geometry is
    /// the game here, not randomness.
    /// </summary>
    public sealed class ZoneBLayout
    {
        public readonly GateDef[] Gates;
        public readonly CollectorDef[] Collectors;
        public readonly WallDef[] Walls;

        public ZoneBLayout(GateDef[] gates, CollectorDef[] collectors, WallDef[] walls)
        {
            Gates = gates;
            Collectors = collectors;
            Walls = walls;
        }
    }
}
