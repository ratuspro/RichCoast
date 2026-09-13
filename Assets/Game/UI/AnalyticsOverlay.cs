using System.Text;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;

namespace RichCoast.UI
{
    /// <summary>
    /// The local sink made visible: the most recent events, newest first, over a dark scrim.
    /// <para>This is what makes a local-only sink worth having before any backend key exists — the
    /// parent spec's "only what you can read off a device you hold", turned into something you can
    /// actually read while holding it. Toggled with the A key in the editor, and by a four-finger
    /// touch on device so it needs no chrome of its own.</para>
    /// <para>Development builds and the editor ONLY. <see cref="Build"/> returns null in a release
    /// build, so a player can never surface it by accident.</para>
    /// </summary>
    public sealed class AnalyticsOverlay : MonoBehaviour
    {
        const int Lines = 22;

        AnalyticsService service;
        TMPro.TextMeshProUGUI text;
        RectTransform panel;
        readonly StringBuilder sb = new StringBuilder(2048);
        bool shown;

        /// <summary>Null outside a development build — the overlay does not exist to be found.</summary>
        public static AnalyticsOverlay Build(Canvas overlayCanvas, AnalyticsService service)
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return null;

            var root = UiKit.Stretch(UiKit.Panel(overlayCanvas.transform, "AnalyticsOverlay"));
            var scrim = UiKit.Image(root, "Scrim", new Color(0.04f, 0.05f, 0.06f, 0.92f));
            UiKit.Stretch((RectTransform)scrim.transform);
            scrim.raycastTarget = false; // a debug view must never eat a tap

            var body = UiKit.Text(root, "AnalyticsLog", "", 30f, new Color(0.85f, 0.78f, 0.55f),
                TMPro.TextAlignmentOptions.TopLeft, TMPro.FontStyles.Normal);
            UiKit.Place((RectTransform)body.transform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 2100f), Vector2.zero);

            var overlay = root.gameObject.AddComponent<AnalyticsOverlay>();
            overlay.service = service;
            overlay.text = body;
            overlay.panel = root;
            root.gameObject.SetActive(false);
            return overlay;
        }

        public void Toggle()
        {
            shown = !shown;
            panel.gameObject.SetActive(shown);
            if (shown) Refresh();
        }

        void Update()
        {
            if (!shown || service == null) return;
            Refresh();
        }

        void Refresh()
        {
            sb.Clear();
            sb.Append("ANALYTICS  backend=").Append(service.BackendActive ? "on" : "off")
              .Append("  in_run=").Append(service.InRun ? "yes" : "no")
              .Append("  buffered=").Append(service.Local.Count).Append('\n')
              .Append(RingBufferSink.FilePath).Append("\n\n");

            var recent = service.Local.Recent;
            // Newest first: the thing you just did is the thing you are looking for.
            for (int i = recent.Count - 1, n = 0; i >= 0 && n < Lines; i--, n++)
                sb.Append(recent[i].ToString()).Append('\n');

            if (recent.Count == 0) sb.Append("(nothing recorded yet)");
            text.text = sb.ToString();
        }
    }
}
