using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace RichCoast.EditorTools
{
    /// <summary>
    /// Unity only adds <c>android.permission.VIBRATE</c> when it sees a <c>Handheld.Vibrate</c>
    /// call, and <see cref="RichCoast.Game.Haptics"/> drives the vibrator through JNI instead —
    /// so without this the first haptic pulse throws a SecurityException and haptics silently
    /// disable themselves for the session. Patch the generated Gradle project's manifest rather
    /// than maintaining a custom manifest template.
    /// </summary>
    public sealed class AndroidManifestPatcher : IPostGenerateGradleAndroidProject
    {
        const string Permission = "<uses-permission android:name=\"android.permission.VIBRATE\" />";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest))
            {
                Debug.LogError($"[AndroidManifestPatcher] manifest not found at {manifest}");
                return;
            }
            var xml = File.ReadAllText(manifest);
            if (xml.Contains("android.permission.VIBRATE"))
            {
                Debug.Log("[AndroidManifestPatcher] VIBRATE already declared");
                return;
            }
            var idx = xml.IndexOf("<application", System.StringComparison.Ordinal);
            if (idx < 0)
            {
                Debug.LogError("[AndroidManifestPatcher] no <application> element in manifest");
                return;
            }
            xml = xml.Insert(idx, Permission + "\n  ");
            File.WriteAllText(manifest, xml);
            Debug.Log("[AndroidManifestPatcher] added android.permission.VIBRATE");
        }
    }
}
