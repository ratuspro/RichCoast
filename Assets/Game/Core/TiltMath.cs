using System;

namespace RichCoast.Core
{
    /// <summary>
    /// The cabinet tilt: the player's one scarce move against a board that has locked up.
    ///
    /// <para>Two properties make it work, and both were learned the hard way from a board that refused
    /// to mix.</para>
    ///
    /// <para><b>The train sums to zero.</b> Lurches alternate and decay, so the naive train
    /// (+1, −0.7, +0.4) nets a shove in the first direction — and the whole board walks that way, a
    /// little further with every tilt. <see cref="PulseAmplitude"/> subtracts the train's mean, so the
    /// cabinet rocks and comes back instead of wandering across the room.</para>
    ///
    /// <para><b>A uniform kick does nothing.</b> Adding the SAME velocity to every ball is a rigid
    /// translation: the board slides and every ball keeps its neighbours, which is the one outcome the
    /// mechanic cannot afford. So a tilt is modelled as the cabinet ROCKING about the funnel apex —
    /// angular, not linear. Tangential velocity about that pivot is (−ω·y, ω·x), so a ball high in the
    /// pile is thrown sideways harder than one nestled in the V, and the two flanks move in opposite
    /// vertical directions. That shear is what actually shuffles a pile.</para>
    ///
    /// <para>A uniform upward lift rides on top, and that is the GAMBLE: it is what pops a ball out of
    /// the pocket its neighbours make, and what can strand one above the death line.</para>
    ///
    /// <para>Pure and engine-free so the train is EditMode-testable; <c>ZoneASystem</c> supplies the
    /// timing and <c>Board</c> applies the result.</para>
    /// </summary>
    public static class TiltMath
    {
        /// <summary>Weakest a pulse gets, as a fraction of the first. Keeps the last lurch a settle, not a nudge.</summary>
        public const double MinFalloff = 0.4;

        /// <summary>Lurch direction: the cabinet rocks one way, then the other.</summary>
        public static double PulseSignX(int pulse) => (pulse & 1) == 0 ? 1.0 : -1.0;

        /// <summary>
        /// How hard pulse <paramref name="pulse"/> hits, falling linearly from 1 to
        /// <see cref="MinFalloff"/> across the train. A single-pulse train hits at full strength.
        /// </summary>
        public static double PulseFalloff(int pulse, int pulses)
        {
            if (pulses <= 1) return 1.0;
            double t = Clamp01(pulse / (double)(pulses - 1));
            return 1.0 - t * (1.0 - MinFalloff);
        }

        /// <summary>
        /// Signed strength of one lurch, mean-corrected so the whole train sums to zero. Without the
        /// correction a decaying alternation leaves a net impulse, and every tilt walks the board a
        /// little further in the first lurch's direction.
        /// <para>A single-pulse train cannot be both non-zero and balanced, so it is left uncorrected —
        /// one lurch is a shove by definition.</para>
        /// </summary>
        public static double PulseAmplitude(int pulse, int pulses)
        {
            double raw = PulseSignX(pulse) * PulseFalloff(pulse, pulses);
            if (pulses <= 1) return raw;
            double sum = 0;
            for (int i = 0; i < pulses; i++) sum += PulseSignX(i) * PulseFalloff(i, pulses);
            return raw - sum / pulses;
        }

        /// <summary>
        /// The velocity delta this lurch adds to a ball at <paramref name="x"/>, <paramref name="y"/>
        /// (world units, funnel apex at the origin, y up).
        ///
        /// <para><paramref name="rock"/> is the angular amplitude about the apex — the shear that mixes.
        /// <paramref name="slide"/> is a uniform sideways shove that keeps the low balls moving at all,
        /// because pure rotation barely touches a ball resting in the V where y is almost zero.
        /// <paramref name="lift"/> is the upward hop; it does NOT alternate, so every lurch lifts.</para>
        /// </summary>
        public static Vec2 Kick(int pulse, int pulses, double x, double y, double slide, double rock, double lift)
        {
            double a = PulseAmplitude(pulse, pulses);
            double f = PulseFalloff(pulse, pulses);
            return new Vec2(a * (slide - rock * y), a * rock * x + lift * f);
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
