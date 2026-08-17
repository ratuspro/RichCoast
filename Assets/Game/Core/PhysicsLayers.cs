namespace RichCoast.Core
{
    /// <summary>
    /// Collision layers, one pair per zone.
    ///
    /// The zones share one physics world but must never touch each other's bodies. This is not
    /// theoretical: Zone A's walls and funnel scale with the arena, so at a late milestone they
    /// reach hundreds of units past the Zone A/C seam and straight through Zone B's playfield. A
    /// Zone B ball colliding with an invisible Zone A wall would look like the arena eating balls
    /// at random, and only at high levels.
    ///
    /// Layer numbers are hard-coded because Unity has no runtime API for naming layers; the editor
    /// setup writes the matching names into the tag manager, and a test asserts the two agree.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int ZoneA = 8;
        public const int ZoneB = 9;

        public const string ZoneAName = "ZoneA";
        public const string ZoneBName = "ZoneB";
    }
}
