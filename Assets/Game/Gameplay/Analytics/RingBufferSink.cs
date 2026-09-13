using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The local sink: the last <see cref="Capacity"/> events in memory for the debug overlay, and a
    /// line-delimited JSON tail on disk so a device you are holding can be read with
    /// <c>adb shell run-as … cat</c> after a session.
    /// <para>This data NEVER leaves the device, which is why it runs regardless of consent — under
    /// Play's data-safety definition it is not collection. It is also the thing that makes the whole
    /// sub-project useful before a backend key exists.</para>
    /// <para>The file is capped and rotated rather than appended forever: a long-lived install must
    /// not be able to fill a player's storage with telemetry.</para>
    /// </summary>
    public sealed class RingBufferSink : IAnalyticsSink
    {
        public const int Capacity = 128;
        const string FileName = "analytics.jsonl";
        const long MaxBytes = 256 * 1024;

        readonly Queue<AnalyticsEvent> recent = new Queue<AnalyticsEvent>(Capacity);
        readonly StringBuilder pending = new StringBuilder(4096);
        readonly bool writeToDisk;

        /// <summary>Tests point this at a temp directory so the suite never writes into a real install.</summary>
        public static string PathOverride;

        public RingBufferSink(bool writeToDisk = true)
        {
            this.writeToDisk = writeToDisk;
        }

        public static string FilePath =>
            string.IsNullOrEmpty(PathOverride) ? Path.Combine(Application.persistentDataPath, FileName) : PathOverride;

        /// <summary>Newest last. Copied out, so the overlay cannot mutate the buffer while iterating.</summary>
        public IReadOnlyList<AnalyticsEvent> Recent => new List<AnalyticsEvent>(recent);

        public int Count => recent.Count;

        public void Track(in AnalyticsEvent e)
        {
            if (recent.Count >= Capacity) recent.Dequeue();
            recent.Enqueue(e);
            if (writeToDisk) pending.Append(e.ToJson()).Append('\n');
        }

        public void Flush()
        {
            if (!writeToDisk || pending.Length == 0) return;
            try
            {
                string path = FilePath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                // Rotate before the append rather than after, so the cap is never exceeded on disk.
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Delete(path);
                File.AppendAllText(path, pending.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] local sink write failed: {ex.Message}");
            }
            finally
            {
                // Cleared either way: a sink that retries forever would grow without bound.
                pending.Length = 0;
            }
        }

        /// <summary>Wipe the on-disk tail and the buffer — the "delete my data" half of consent.</summary>
        public void Clear()
        {
            recent.Clear();
            pending.Length = 0;
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception ex) { Debug.LogWarning($"[Analytics] local sink clear failed: {ex.Message}"); }
        }
    }
}
