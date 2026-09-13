using System;
using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;

namespace RichCoast.UI
{
    /// <summary>
    /// The analytics consent gate: shown once before the title on a fresh install, and re-openable
    /// from the title's PRIVACY button so the choice can be withdrawn — which Play requires, and
    /// which is the only reason a granted consent is worth anything.
    /// <para>Built in the same pine-and-brass cabinet language as <see cref="TitleView"/>, because
    /// this IS part of the machine and dressing it as a system alert would read as someone else's
    /// screen bolted onto the game.</para>
    /// <para>ACCEPT and DECLINE are the SAME SIZE and the same weight. The brass fill goes on
    /// neither: making the answer you prefer louder than the other is the dark pattern this screen
    /// exists to avoid, and a consent collected that way is not consent.</para>
    /// </summary>
    public static class ConsentView
    {
        const string Body =
            "RichCoast can send anonymous play data — how far runs get, where they end, " +
            "and whether the trap-door lands — so the game can be tuned.\n\n" +
            "No accounts, no names, no location, nothing that identifies you or your device to us.\n\n" +
            "The game plays exactly the same either way, and you can change this any time " +
            "from PRIVACY on the title screen.";

        /// <summary>
        /// <paramref name="onChoice"/> receives the new state and is responsible for persisting it.
        /// <paramref name="onDismiss"/> fires after either button, once the view is gone.
        /// </summary>
        public static GameObject Show(Canvas overlayCanvas, ConsentState current,
            Action<ConsentState> onChoice, Action onDismiss = null)
        {
            var root = UiKit.Stretch(UiKit.Panel(overlayCanvas.transform, "Consent"));

            var cabinet = UiKit.Image(root, "Cabinet", Theme.PineShadow);
            UiKit.Themed(cabinet, ThemeKey.PineShadow);
            UiKit.Stretch((RectTransform)cabinet.transform);
            cabinet.raycastTarget = true; // nothing underneath is reachable while this is up

            var frame = UiKit.Image(root, "PanelFrame", Theme.PineDark);
            UiKit.Themed(frame, ThemeKey.PineDark);
            UiKit.Place((RectTransform)frame.transform, Mid, new Vector2(980f, 1180f), Vector2.zero);

            var face = UiKit.Image(root, "PanelFace", Theme.Cream);
            UiKit.Themed(face, ThemeKey.Cream);
            UiKit.Place((RectTransform)face.transform, Mid, new Vector2(940f, 1140f), Vector2.zero);

            var group = UiKit.Panel(root, "Group");
            UiKit.Place(group, Mid, new Vector2(980f, 1180f), Vector2.zero);

            var heading = UiKit.Text(group, "ConsentTitle", "PLAY DATA", 68f, Theme.Ink);
            UiKit.Themed(heading, ThemeKey.Ink);
            UiKit.Place((RectTransform)heading.transform, Mid, new Vector2(860f, 110f), new Vector2(0f, 440f));

            var body = UiKit.Text(group, "ConsentBody", Body, 40f, Theme.Ink,
                TMPro.TextAlignmentOptions.TopLeft, TMPro.FontStyles.Normal);
            UiKit.Themed(body, ThemeKey.Ink);
            UiKit.Place((RectTransform)body.transform, Mid, new Vector2(820f, 640f), new Vector2(0f, 40f));
            // UiKit defaults to NoWrap, which is right for the scores and button labels it exists for
            // and wrong for the only actual PARAGRAPH in the game — it would run off the panel.
            body.textWrappingMode = TMPro.TextWrappingModes.Normal;
            body.lineSpacing = 8f;
            body.paragraphSpacing = 14f;

            // Equal weight, side by side: two answers, not a suggestion and an escape hatch.
            var accept = UiKit.Button(group, "ConsentAccept", "ALLOW", Theme.Cream, Theme.PineDark, Theme.PineDark, 48f);
            UiKit.Place((RectTransform)accept.transform, Mid, new Vector2(400f, 140f), new Vector2(-215f, -330f));

            var decline = UiKit.Button(group, "ConsentDecline", "NO THANKS", Theme.Cream, Theme.PineDark, Theme.PineDark, 48f);
            UiKit.Place((RectTransform)decline.transform, Mid, new Vector2(400f, 140f), new Vector2(215f, -330f));

            var note = UiKit.Text(group, "ConsentPolicy", PrivacyPolicy.ShortNotice, 32f, Theme.PineDark,
                TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Normal);
            UiKit.Themed(note, ThemeKey.PineDark);
            UiKit.Place((RectTransform)note.transform, Mid, new Vector2(860f, 90f), new Vector2(0f, -460f));

            var go = root.gameObject;
            GameEvents.RaiseModalOpen(true);

            void Choose(ConsentState state)
            {
                GameEvents.RaiseModalOpen(false);
                UnityEngine.Object.Destroy(go);
                onChoice?.Invoke(state);
                onDismiss?.Invoke();
            }

            accept.onClick.AddListener(() => Choose(ConsentState.Granted));
            decline.onClick.AddListener(() => Choose(ConsentState.Denied));

            // A quiet mark of what is already chosen, when this is reopened rather than first seen.
            if (current != ConsentState.Unasked)
            {
                var chosen = current == ConsentState.Granted ? accept : decline;
                var img = chosen.GetComponent<UnityEngine.UI.Image>();
                if (img != null) { img.color = Theme.Brass; UiKit.Themed(img, ThemeKey.Brass); }
            }

            group.localScale = Vector3.one * 0.9f;
            Tween.Scale(group, 1f, 0.3f, Ease.OutBack);
            return go;
        }

        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);
    }
}
