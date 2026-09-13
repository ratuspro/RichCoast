using System.Globalization;
using System.Threading;
using NUnit.Framework;
using RichCoast.Core;

namespace RichCoast.Tests.EditMode
{
    /// <summary>
    /// The measurement payload. Two things matter here and both are the kind of bug that is invisible
    /// until a backend has months of wrong numbers in it: the culture a number is written in, and how
    /// many digits of a score survive.
    /// </summary>
    public class AnalyticsEventTests
    {
        [Test]
        public void ParamsReadBackByKey()
        {
            var e = new AnalyticsEvent("run_end").With("cause", "stalemate").With("level", 12);

            Assert.AreEqual("stalemate", e.Get("cause"));
            Assert.AreEqual("12", e.Get("level"));
            Assert.IsNull(e.Get("nope"));
            Assert.AreEqual(2, e.ParamCount);
        }

        [Test]
        public void NumbersAreWrittenInInvariantCultureWhateverTheDeviceIs()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                // pt-PT writes 1,5 for one-and-a-half. A backend reading that as a thousands separator
                // turns 1.5 seconds into 15, silently, forever.
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pt-PT");
                var e = new AnalyticsEvent("level_up").WithRounded("seconds_in_level", 1.5);
                Assert.AreEqual("1.5", e.Get("seconds_in_level"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void MoneyKeepsEveryDigit()
        {
            // The same trap SaveNum documents: a score is a double that reaches 3^19 per ball and
            // compounds, and a shorter format quietly loses digits off the top.
            const double score = 987654321987.65432;
            var e = new AnalyticsEvent("run_end").With("score", score);

            Assert.AreEqual(score, double.Parse(e.Get("score"), CultureInfo.InvariantCulture),
                "a money value must survive the round trip exactly");
        }

        [Test]
        public void RoundedValuesDropTrailingNoise()
        {
            var e = new AnalyticsEvent("milestone").WithRounded("arena_scale", 1.2000000000000002);
            Assert.AreEqual("1.2", e.Get("arena_scale"));
        }

        [Test]
        public void ParamsPastTheCapAreDroppedRatherThanThrowing()
        {
            var e = new AnalyticsEvent("wide");
            for (int i = 0; i < AnalyticsEvent.MaxParams + 3; i++) e = e.With($"k{i}", i);

            Assert.AreEqual(AnalyticsEvent.MaxParams, e.ParamCount);
            Assert.IsNull(e.Get("k6"), "the overflow is dropped, not wrapped over an earlier key");
            Assert.AreEqual("0", e.Get("k0"), "and the earlier keys survive intact");
        }

        [Test]
        public void JsonIsOneObjectPerEvent()
        {
            var json = new AnalyticsEvent("door_tap").With("grabbed", true).With("sweep_index", 4).ToJson();
            Assert.AreEqual("{\"event\":\"door_tap\",\"grabbed\":\"true\",\"sweep_index\":\"4\"}", json);
        }

        [Test]
        public void JsonEscapesWhatWouldBreakTheLine()
        {
            var json = new AnalyticsEvent("odd").With("k", "a\"b\\c\nd").ToJson();
            Assert.IsFalse(json.Contains("\n"), "a raw newline would split one event across two JSONL lines");
            Assert.AreEqual("{\"event\":\"odd\",\"k\":\"a\\\"b\\\\c\\nd\"}", json);
        }
    }
}
