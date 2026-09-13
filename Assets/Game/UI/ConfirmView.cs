using System;
using PrimeTween;
using RichCoast.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.UI
{
    /// <summary>
    /// A two-button confirm scrim, used by the two irreversible actions in the game: NEW RUN (which
    /// discards a saved run) and QUIT. Returns the root so the caller can dismiss it.
    /// <para>It raises <c>GameEvents.ModalOpen</c> ITSELF rather than leaving that to callers: the
    /// scrim's <c>raycastTarget</c> only stops uGUI, while Zone A's aim and Zone C's trap-door both
    /// read <c>Pointer.current</c> directly — so without the event the game keeps playing underneath,
    /// and a caller that forgot to pair open/close would leave it that way.</para>
    /// </summary>
    public static class ConfirmView
    {
        public static GameObject Show(Canvas overlayCanvas, string title, string confirmLabel, Action onConfirm, Action onCancel = null)
        {
            var root = UiKit.Stretch(UiKit.Panel(overlayCanvas.transform, "Confirm"));
            var scrim = UiKit.Image(root, "Scrim", new Color(Theme.Scrim.r, Theme.Scrim.g, Theme.Scrim.b, 0f));
            UiKit.Stretch((RectTransform)scrim.transform);
            scrim.raycastTarget = true; // swallow taps on whatever is underneath
            Tween.Alpha(scrim, 0.85f, 0.2f);

            var group = UiKit.Panel(root, "Group");
            UiKit.Place(group, new Vector2(0.5f, 0.5f), new Vector2(860f, 460f), Vector2.zero);

            var label = UiKit.Text(group, "ConfirmTitle", title, 62f, Theme.BrassBright);
            UiKit.Place((RectTransform)label.transform, new Vector2(0.5f, 0.5f), new Vector2(860f, 150f), new Vector2(0f, 110f));

            var yes = UiKit.Button(group, "ConfirmYes", confirmLabel, Theme.PineDark, Theme.BrassBright, Theme.Cream, 52f);
            UiKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0.5f), new Vector2(390f, 130f), new Vector2(-210f, -90f));
            var no = UiKit.Button(group, "ConfirmNo", "CANCEL", Theme.PineShadow, Theme.Brass, Theme.Cream, 52f);
            UiKit.Place((RectTransform)no.transform, new Vector2(0.5f, 0.5f), new Vector2(390f, 130f), new Vector2(210f, -90f));

            var go = root.gameObject;
            RichCoast.Core.GameEvents.RaiseModalOpen(true);
            yes.onClick.AddListener(() =>
            {
                RichCoast.Core.GameEvents.RaiseModalOpen(false);
                UnityEngine.Object.Destroy(go);
                onConfirm?.Invoke();
            });
            no.onClick.AddListener(() =>
            {
                RichCoast.Core.GameEvents.RaiseModalOpen(false);
                UnityEngine.Object.Destroy(go);
                onCancel?.Invoke();
            });

            group.localScale = Vector3.one * 0.85f;
            Tween.Scale(group, 1f, 0.3f, Ease.OutBack);
            return go;
        }
    }
}
