using System;
using System.Globalization;
using System.Text;

namespace RichCoast.Core
{
    /// <summary>
    /// One measurement: a snake_case name plus a handful of typed parameters.
    /// <para>A fixed six-slot inline buffer rather than a dictionary — these are built on the
    /// gameplay thread (a door tap is per-second, not per-frame, but there is no reason to hand the
    /// GC anything at all) and six covers the widest event in the design with room to spare. A
    /// seventh parameter is a signal the event is really two events.</para>
    /// <para>Values are stored as strings ALREADY FORMATTED in invariant culture. A Portuguese
    /// device writes "1,5" for a float under the current culture and a backend silently reads it as
    /// a different number; formatting once at the boundary makes that unrepresentable.</para>
    /// </summary>
    public struct AnalyticsEvent
    {
        public const int MaxParams = 6;

        public string Name;

        string k0, k1, k2, k3, k4, k5;
        string v0, v1, v2, v3, v4, v5;
        int count;

        public int ParamCount => count;

        public AnalyticsEvent(string name)
        {
            Name = name;
            k0 = k1 = k2 = k3 = k4 = k5 = null;
            v0 = v1 = v2 = v3 = v4 = v5 = null;
            count = 0;
        }

        public string KeyAt(int i)
        {
            switch (i)
            {
                case 0: return k0;
                case 1: return k1;
                case 2: return k2;
                case 3: return k3;
                case 4: return k4;
                case 5: return k5;
                default: return null;
            }
        }

        public string ValueAt(int i)
        {
            switch (i)
            {
                case 0: return v0;
                case 1: return v1;
                case 2: return v2;
                case 3: return v3;
                case 4: return v4;
                case 5: return v5;
                default: return null;
            }
        }

        /// <summary>Look a parameter up by key; null when absent. For tests and the debug overlay.</summary>
        public string Get(string key)
        {
            for (int i = 0; i < count; i++)
                if (string.Equals(KeyAt(i), key, StringComparison.Ordinal)) return ValueAt(i);
            return null;
        }

        /// <summary>
        /// Append a parameter. Silently ignores anything past <see cref="MaxParams"/>: a dropped
        /// measurement is a worse outcome than a lost one, and analytics must never throw into a run.
        /// </summary>
        public AnalyticsEvent With(string key, string value)
        {
            if (count >= MaxParams || string.IsNullOrEmpty(key)) return this;
            switch (count)
            {
                case 0: k0 = key; v0 = value; break;
                case 1: k1 = key; v1 = value; break;
                case 2: k2 = key; v2 = value; break;
                case 3: k3 = key; v3 = value; break;
                case 4: k4 = key; v4 = value; break;
                case 5: k5 = key; v5 = value; break;
            }
            count++;
            return this;
        }

        public AnalyticsEvent With(string key, int value) =>
            With(key, value.ToString(CultureInfo.InvariantCulture));

        public AnalyticsEvent With(string key, bool value) =>
            With(key, value ? "true" : "false");

        /// <summary>
        /// Doubles go in as "G17" — the same choice, for the same reason, as <see cref="SaveNum"/>:
        /// scores reach 3^19 per ball and compound, and anything shorter loses digits off the top.
        /// </summary>
        public AnalyticsEvent With(string key, double value) =>
            With(key, value.ToString("G17", CultureInfo.InvariantCulture));

        /// <summary>
        /// A double rounded to three places and written without trailing zeros — for durations,
        /// positions and scale factors, none of which deserve seventeen digits. Money uses
        /// <see cref="With(string,double)"/> instead, where every digit matters.
        /// </summary>
        public AnalyticsEvent WithRounded(string key, double value) =>
            With(key, Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture));

        /// <summary>One JSON object, for the local sink's line-delimited file and the debug overlay.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder(96);
            sb.Append("{\"event\":\"").Append(Escape(Name)).Append('"');
            for (int i = 0; i < count; i++)
                sb.Append(",\"").Append(Escape(KeyAt(i))).Append("\":\"").Append(Escape(ValueAt(i))).Append('"');
            sb.Append('}');
            return sb.ToString();
        }

        public override string ToString()
        {
            var sb = new StringBuilder(64);
            sb.Append(Name);
            for (int i = 0; i < count; i++)
                sb.Append(' ').Append(KeyAt(i)).Append('=').Append(ValueAt(i));
            return sb.ToString();
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf('"') < 0 && s.IndexOf('\\') < 0 && s.IndexOf('\n') < 0) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }
    }
}
