using System;
using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;

namespace RichCoast.UI
{
    /// <summary>
    /// The app's front door, built to read as THE SAME OBJECT as the game: a cream panel inset in a
    /// pine cabinet, with the wordmark struck on a brass maker's plate. The plate is the one loud
    /// element; everything around it stays quiet.
    /// <para>The settings toggles deliberately sit OUTSIDE the panel, on the cabinet — they are the
    /// machine's switches, not actions in the game, and the split says so without a label.</para>
    /// <para>With a saved run present, CONTINUE is primary and NEW RUN is secondary AND confirmed:
    /// tapping past a saved run destroys it, the one irreversible thing this screen can do.</para>
    /// </summary>
    public static class TitleView
    {
        public static GameObject Show(Canvas overlayCanvas, SaveData save,
            Action<RunSnapshot> onStart, Action<bool> onSound, Action<bool> onHaptics)
        {
            var root = UiKit.Stretch(UiKit.Panel(overlayCanvas.transform, "TitleScreen"));

            // The cabinet: full-bleed pine, the machine's body.
            var cabinet = UiKit.Image(root, "Cabinet", Theme.PineShadow);
            UiKit.Themed(cabinet, ThemeKey.PineShadow);
            UiKit.Stretch((RectTransform)cabinet.transform);
            cabinet.raycastTarget = true; // swallow taps on the backdrop

            // The panel: a pine frame around a cream face, mirroring the board the game plays on.
            var frame = UiKit.Image(root, "PanelFrame", Theme.PineDark);
            UiKit.Themed(frame, ThemeKey.PineDark);
            UiKit.Place((RectTransform)frame.transform, Mid, new Vector2(980f, 1000f), new Vector2(0f, 150f));

            var face = UiKit.Image(root, "PanelFace", Theme.Cream);
            UiKit.Themed(face, ThemeKey.Cream);
            UiKit.Place((RectTransform)face.transform, Mid, new Vector2(940f, 960f), new Vector2(0f, 150f));

            var group = UiKit.Panel(root, "Group");
            UiKit.Place(group, Mid, new Vector2(980f, 1000f), new Vector2(0f, 150f));

            // The maker's plate — brass, ink-struck, letter-spaced like something stamped rather than set.
            var plate = UiKit.Image(group, "Nameplate", Theme.Brass);
            UiKit.Themed(plate, ThemeKey.Brass);
            UiKit.Place((RectTransform)plate.transform, Mid, new Vector2(860f, 200f), new Vector2(0f, 320f));
            var plateEdge = plate.gameObject.AddComponent<UnityEngine.UI.Outline>();
            plateEdge.effectColor = Theme.Ink;
            plateEdge.effectDistance = new Vector2(5f, -5f);
            UiKit.Themed(plateEdge, ThemeKey.Ink);

            var wordmark = UiKit.Text(plate.transform, "Wordmark", "RICHCOAST", 96f, Theme.Ink);
            UiKit.Themed(wordmark, ThemeKey.Ink);
            UiKit.Stretch((RectTransform)wordmark.transform);
            wordmark.characterSpacing = 8f;

            var best = save.records.bestScore > 0 ? $"Best {NumberFormat.Compact(save.records.bestScore)}" : "";
            var bestText = UiKit.Text(group, "TitleBest", best, 52f, Theme.Ink, style: TMPro.FontStyles.Normal);
            UiKit.Themed(bestText, ThemeKey.Ink);
            UiKit.Place((RectTransform)bestText.transform, Mid, new Vector2(900f, 90f), new Vector2(0f, 150f));

            var go = root.gameObject;
            if (save.hasRun)
            {
                // Brass fill, ink text: the lever you pull.
                var cont = UiKit.Button(group, "Continue", "CONTINUE", Theme.Brass, Theme.Ink, Theme.Ink, 62f);
                UiKit.Place((RectTransform)cont.transform, Mid, new Vector2(660f, 170f), new Vector2(0f, -80f));
                cont.onClick.AddListener(() =>
                {
                    var run = save.run;
                    UnityEngine.Object.Destroy(go);
                    onStart?.Invoke(run);
                });

                // Cream on pine outline: quieter, and it is the destructive one.
                var fresh = UiKit.Button(group, "NewRun", "NEW RUN", Theme.Cream, Theme.PineDark, Theme.PineDark, 46f);
                UiKit.Place((RectTransform)fresh.transform, Mid, new Vector2(560f, 130f), new Vector2(0f, -270f));
                fresh.onClick.AddListener(() => ConfirmView.Show(overlayCanvas, "DISCARD SAVED RUN?", "NEW RUN",
                    () => { UnityEngine.Object.Destroy(go); onStart?.Invoke(null); }));
            }
            else
            {
                var play = UiKit.Button(group, "Play", "PLAY", Theme.Brass, Theme.Ink, Theme.Ink, 62f);
                UiKit.Place((RectTransform)play.transform, Mid, new Vector2(660f, 170f), new Vector2(0f, -140f));
                play.onClick.AddListener(() => { UnityEngine.Object.Destroy(go); onStart?.Invoke(null); });
            }

            // Out on the cabinet: switches, not actions. Side by side because they are peers.
            var sound = UiKit.Toggle(root, "SoundToggle", "SOUND", save.settings.soundOn, onSound);
            UiKit.Place((RectTransform)sound.transform, Mid, new Vector2(450f, 120f), new Vector2(-235f, -560f));
            var haptics = UiKit.Toggle(root, "HapticsToggle", "HAPTICS", save.settings.hapticsOn, onHaptics);
            UiKit.Place((RectTransform)haptics.transform, Mid, new Vector2(450f, 120f), new Vector2(235f, -560f));

            // One orchestrated moment: the panel seats itself, the plate lands a beat later.
            group.localScale = Vector3.one * 0.94f;
            Tween.Scale(group, 1f, 0.35f, Ease.OutBack);
            plate.transform.localScale = new Vector3(1f, 0.7f, 1f);
            Tween.Scale(plate.transform, Vector3.one, 0.4f, Ease.OutBack, startDelay: 0.12f);
            return go;
        }

        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    }
}
