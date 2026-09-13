using System;
using RichCoast.Core;
using UnityEngine;
#if RICHCOAST_GAMEANALYTICS
using GameAnalyticsSDK;
#endif

namespace RichCoast.Game
{
    /// <summary>
    /// The backend adapter. Constructed ONLY when <c>Settings.Consent == Granted</c> — the consent
    /// gate is the caller's (<c>GameBootstrap</c>), so that this class cannot be the place a mistake
    /// leaks data.
    /// <para>The whole body is behind <c>RICHCOAST_GAMEANALYTICS</c>. The package is not in
    /// <c>Packages/manifest.json</c> yet and there are no keys, so without the define this compiles
    /// to a sink that records that it would have sent something and sends nothing. That keeps the
    /// headless build and the test suite green while the credential question is still open.</para>
    ///
    /// <para><b>To switch it on:</b> add the GameAnalytics package, enter the Android game key and
    /// secret key in its settings asset, add <c>RICHCOAST_GAMEANALYTICS</c> to Player Settings →
    /// Scripting Define Symbols for Android, then re-run <c>Tools/build-android.sh</c> and confirm
    /// the batchmode build still succeeds before trusting it.</para>
    ///
    /// <para><b>Event mapping.</b> GameAnalytics has no free-form event with named parameters, so the
    /// six designed events flatten onto the two shapes it does have: a run is a PROGRESSION event
    /// (start / complete / fail, where "fail" carries the run's cause), and everything else is a
    /// DESIGN event whose colon-delimited name carries the dimensions that matter most. The full
    /// parameter set always reaches the local sink regardless, so nothing is actually lost — only
    /// the backend's view is lossy.</para>
    /// </summary>
    public sealed class GameAnalyticsSink : IAnalyticsSink
    {
#if RICHCOAST_GAMEANALYTICS
        bool initialised;
#endif

        /// <summary>How many events this sink has accepted — asserted by the consent-gate test.</summary>
        public int Sent { get; private set; }

        public GameAnalyticsSink()
        {
#if RICHCOAST_GAMEANALYTICS
            try
            {
                GameAnalytics.Initialize();
                initialised = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] GameAnalytics init failed, backend disabled: {e.Message}");
            }
#else
            Debug.Log("[Analytics] built without RICHCOAST_GAMEANALYTICS; backend sink is inert");
#endif
        }

        public void Track(in AnalyticsEvent e)
        {
            Sent++;
#if RICHCOAST_GAMEANALYTICS
            if (!initialised) return;
            try { Send(e); }
            catch (Exception ex) { Debug.LogWarning($"[Analytics] send failed: {ex.Message}"); }
#endif
        }

        /// <summary>GameAnalytics batches and ships on its own schedule; there is nothing to push.</summary>
        public void Flush() { }

#if RICHCOAST_GAMEANALYTICS
        static void Send(in AnalyticsEvent e)
        {
            switch (e.Name)
            {
                case "run_start":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "run", Lvl(e));
                    break;

                case "run_end":
                    // Fail, not Complete: an endless game's run always ends by losing, and the cause
                    // rides along as the third dimension so the funnel splits death line vs stalemate.
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, "run", Lvl(e), e.Get("cause"),
                        ScoreAsInt(e.Get("score")));
                    break;

                case "level_up":
                    GameAnalytics.NewDesignEvent($"progression:level_up:{e.Get("level")}",
                        Num(e.Get("seconds_in_level")));
                    break;

                case "golden_gate_hit":
                    GameAnalytics.NewDesignEvent($"golden:hit:x{e.Get("multiplier")}", Num(e.Get("level")));
                    break;

                case "door_tap":
                    GameAnalytics.NewDesignEvent($"door:tap:{(e.Get("grabbed") == "true" ? "hit" : "miss")}",
                        Num(e.Get("sweep_index")));
                    break;

                case "milestone":
                    GameAnalytics.NewDesignEvent($"progression:milestone:{e.Get("level")}",
                        Num(e.Get("arena_scale")));
                    break;
            }
        }

        static string Lvl(in AnalyticsEvent e) => $"level_{e.Get("level")}";

        static float Num(string s) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

        /// <summary>
        /// Progression scores are an int in the SDK, while a run's total is a double that reaches far
        /// past int.MaxValue. Clamping is the honest lossy choice: the exact value is in the local
        /// sink, and a saturated backend number is better than a wrapped one.
        /// </summary>
        static int ScoreAsInt(string s)
        {
            if (!double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v)) return 0;
            if (v >= int.MaxValue) return int.MaxValue;
            if (v <= 0) return 0;
            return (int)v;
        }
#endif
    }
}
