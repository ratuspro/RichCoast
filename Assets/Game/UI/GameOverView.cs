using System;
using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.UI
{
    /// <summary>Full-screen game-over overlay: scrim, "GAME OVER", the final score, and RESTART.</summary>
    public static class GameOverView
    {
        public static void Show(Canvas overlayCanvas, double finalScore, Action onRestart)
        {
            var root = UiKit.Stretch(UiKit.Panel(overlayCanvas.transform, "GameOver"));
            var scrim = UiKit.Image(root, "Scrim", new Color(Theme.Scrim.r, Theme.Scrim.g, Theme.Scrim.b, 0f));
            UiKit.Stretch((RectTransform)scrim.transform);
            scrim.raycastTarget = true; // swallow taps under the panel
            Tween.Alpha(scrim, 0.85f, 0.35f);

            var group = UiKit.Panel(root, "Group");
            UiKit.Place(group, new Vector2(0.5f, 0.5f), new Vector2(900f, 700f), Vector2.zero);
            var cg = group.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var title = UiKit.Text(group, "Title", "GAME OVER", 120f, Theme.BrassBright);
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 160f), new Vector2(0f, 200f));
            var score = UiKit.Text(group, "Score", $"Score: {NumberFormat.Compact(finalScore)}", 66f, Theme.Cream, style: TMPro.FontStyles.Normal);
            UiKit.Place((RectTransform)score.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 100f), new Vector2(0f, 60f));
            var button = UiKit.Button(group, "Restart", "RESTART", Theme.PineDark, Theme.BrassBright, Theme.Cream, 60f);
            UiKit.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(560f, 156f), new Vector2(0f, -150f));
            button.onClick.AddListener(() =>
            {
                button.interactable = false;
                onRestart?.Invoke();
            });

            group.localScale = Vector3.one * 0.8f;
            Tween.Alpha(cg, 1f, 0.3f, startDelay: 0.15f);
            Tween.Scale(group, 1f, 0.45f, Ease.OutBack, startDelay: 0.15f);
        }
    }
}
