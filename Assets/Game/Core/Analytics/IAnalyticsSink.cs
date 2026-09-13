namespace RichCoast.Core
{
    /// <summary>
    /// Where measurements go. The whole point of the interface is that the sub-project's open fork —
    /// local-only or a backend — stays a one-class decision rather than something the recorder knows.
    /// <para>Implementations MUST swallow their own failures. A sink that throws would take a run
    /// down with it, and no measurement is worth that.</para>
    /// </summary>
    public interface IAnalyticsSink
    {
        void Track(in AnalyticsEvent e);

        /// <summary>Push anything buffered. Called on pause, quit and run end; may be a no-op.</summary>
        void Flush();
    }
}
