using System;
using System.IO;
using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The save file: one JSON document at <c>persistentDataPath/save.json</c>, written atomically
    /// (temp + replace) so a kill mid-write cannot truncate it.
    /// <para>Every failure path — missing, unreadable, malformed, wrong version, invalid run —
    /// degrades to usable data rather than an exception: persistence must never be able to take
    /// gameplay down with it.</para>
    /// <para>Records, settings and the run are independent. A run that fails
    /// <see cref="SaveSchema.ValidateRun"/> is dropped ALONE; losing an interrupted run is a shrug,
    /// losing a best score is not.</para>
    /// </summary>
    public static class SaveStore
    {
        const string FileName = "save.json";

        /// <summary>Tests point this at a temp directory so running the suite never clobbers a real save.</summary>
        public static string PathOverride;

        public static string FilePath =>
            string.IsNullOrEmpty(PathOverride) ? Path.Combine(Application.persistentDataPath, FileName) : PathOverride;

        public static SaveData Load(ProgressionCurve curve)
        {
            var fresh = new SaveData();
            string path = FilePath;
            string json;
            try
            {
                if (!File.Exists(path)) return fresh;
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] unreadable save, starting fresh: {e.Message}");
                return fresh;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] malformed save, starting fresh: {e.Message}");
                return fresh;
            }
            if (data == null) return fresh;

            // A hand-edited or partial file can omit whole objects; JsonUtility leaves those null.
            if (data.records == null) data.records = new Records();
            if (data.settings == null) data.settings = new Settings();
            if (data.run == null) data.run = new RunSnapshot();

            // Discard, don't migrate: the format will churn through M5, and a stale run is the cheap
            // thing to lose. Records and settings are the parts worth carrying across versions.
            if (data.schemaVersion != SaveSchema.Current)
            {
                Debug.Log($"[SaveStore] save v{data.schemaVersion} != v{SaveSchema.Current}; dropping the run, keeping records");
                data.schemaVersion = SaveSchema.Current;
                data.ClearRun();
                return data;
            }

            if (data.hasRun && !SaveSchema.ValidateRun(data.run, curve, out var reason))
            {
                Debug.LogWarning($"[SaveStore] invalid run discarded ({reason}); records and settings kept");
                data.ClearRun();
            }
            return data;
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;
            data.schemaVersion = SaveSchema.Current;
            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(temp, JsonUtility.ToJson(data));
                // Replace needs an existing destination; Move covers the first-ever write.
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] save failed: {e.Message}");
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ }
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"[SaveStore] delete failed: {e.Message}"); }
        }
    }
}
