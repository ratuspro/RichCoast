using System;
using System.Globalization;

namespace RichCoast.Core
{
    public static class NumberFormat
    {
        static readonly string[] Units = { "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No" };

        /// <summary>
        /// Compact display form of a score/value (port of <c>compactValue</c>): values are 3^(tier-1),
        /// so they outgrow any fixed digit budget fast. ≤5 characters: 981 → "981", 19683 → "20K",
        /// 1594323 → "1.6M", idle-game units up to 1e33 ("150Qa"), then exponent form ("4e34").
        /// </summary>
        public static string Compact(double value)
        {
            if (value < 1000) return value.ToString("0", CultureInfo.InvariantCulture);
            double n = value;
            int unit = -1;
            while (n >= 1000 && unit < Units.Length - 1)
            {
                n /= 1000;
                unit++;
            }
            if (n >= 1000)
            {
                // "4e+34" → "4e34", matching the web build's display.
                return value.ToString("0e+0", CultureInfo.InvariantCulture).Replace("e+", "e");
            }
            string body = n >= 10
                ? Math.Round(n).ToString("0", CultureInfo.InvariantCulture)
                : (Math.Round(n * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
            return body + Units[unit];
        }
    }
}
