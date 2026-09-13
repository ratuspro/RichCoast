using System;
using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;

namespace RichCoast.UI
{
    /// <summary>
    /// The app's front door: the wordmark, the lifetime best, and the way into a run.
    /// <para>With a saved run present, CONTINUE is primary and NEW RUN is secondary AND confirmed —
    /// tapping past a saved run by accident destroys it, which is the one irreversible thing this
    /// screen can do.</para>
    /// </summary>
    public static class TitleView
    {
        public static GameObject Show(Canvas overlayCanvas, SaveData save,
            Action<RunSnapshot> onStart, Action<bool> onSound, Action<bool> onHaptics)
        {
            var root = UiKit.Stretch(UiKit.Panel(overlayCanvas.transform, "TitleScreen"));
            var back = UiKit.Image(root, "Backdrop", Theme.PineDark);
            UiKit.Themed(back, ThemeKey.PineDark);
            UiKit.Stretch((RectTransform)back.transform);
            back.raycastTarget = true;

            var group = UiKit.Panel(root, "Group");
            UiKit.Place(group, new Vector2(0.5f, 0.5f), new Vector2(1000f, 1500f), Vector2.zero);

            var wordmark = UiKit.Text(group, "Wordmark", "RICHCOAST", 130f, Theme.BrassBright);
            UiKit.Place((RectTransform)wordmark.transform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 180f), new Vector2(0f, 470f));

            var best = save.records.bestScore > 0 ? $"BEST  {NumberFormat.Compact(save.records.bestScore)}" : "";
            var bestText = UiKit.Text(group, "TitleBest", best, 56f, Theme.Cream, style: TMPro.FontStyles.Normal);
            UiKit.Place((RectTransform)bestText.transform, new Vector2(0.5f, 0.5f), new Vector2(1000f, 100f), new Vector2(0f, 330f));

            var go = root.gameObject;
            if (save.hasRun)
            {
                var cont = UiKit.Button(group, "Continue", "CONTINUE", Theme.Brass, Theme.BrassBright, Theme.Ink, 62f);
                UiKit.Place((RectTransform)cont.transform, new Vector2(0.5f, 0.5f), new Vector2(640f, 160f), new Vector2(0f, 70f));
                cont.onClick.AddListener(() =>
                {
                    var run = save.run;
                    UnityEngine.Object.Destroy(go);
                    onStart?.Invoke(run);
                });

                var fresh = UiKit.Button(group, "NewRun", "NEW RUN", Theme.PineShadow, Theme.Brass, Theme.Cream, 48f);
                UiKit.Place((RectTransform)fresh.transform, new Vector2(0.5f, 0.5f), new Vector2(520f, 130f), new Vector2(0f, -110f));
                fresh.onClick.AddListener(() => ConfirmView.Show(overlayCanvas, "DISCARD SAVED RUN?", "NEW RUN",
                    () => { UnityEngine.Object.Destroy(go); onStart?.Invoke(null); }));
            }
            else
            {
                var play = UiKit.Button(group, "Play", "PLAY", Theme.Brass, Theme.BrassBright, Theme.Ink, 62f);
                UiKit.Place((RectTransform)play.transform, new Vector2(0.5f, 0.5f), new Vector2(640f, 160f), new Vector2(0f, 20f));
                play.onClick.AddListener(() => { UnityEngine.Object.Destroy(go); onStart?.Invoke(null); });
            }

            var sound = UiKit.Toggle(group, "SoundToggle", "SOUND", save.settings.soundOn, onSound);
            UiKit.Place((RectTransform)sound.transform, new Vector2(0.5f, 0.5f), new Vector2(460f, 110f), new Vector2(0f, -370f));
            var haptics = UiKit.Toggle(group, "HapticsToggle", "HAPTICS", save.settings.hapticsOn, onHaptics);
            UiKit.Place((RectTransform)haptics.transform, new Vector2(0.5f, 0.5f), new Vector2(460f, 110f), new Vector2(0f, -500f));

            group.localScale = Vector3.one * 0.9f;
            Tween.Scale(group, 1f, 0.35f, Ease.OutBack);
            return go;
        }
    }
}
