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
        public static void Show(Canvas overlayCanvas, double finalScore, Records records, bool newBest, Action onRestart, Action onTitle)
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
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 160f), new Vector2(0f, 250f));
            var score = UiKit.Text(group, "Score", $"Score: {NumberFormat.Compact(finalScore)}", 66f, Theme.Cream, style: TMPro.FontStyles.Normal);
            UiKit.Place((RectTransform)score.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 100f), new Vector2(0f, 130f));

            // A new best REPLACES the Best: line rather than sitting beside it — this run is the record.
            var bestText = newBest
                ? UiKit.Text(group, "Best", "NEW BEST!", 72f, Theme.BrassBright)
                : UiKit.Text(group, "Best", $"Best: {NumberFormat.Compact(records.bestScore)}", 52f, Theme.Brass, style: TMPro.FontStyles.Normal);
            UiKit.Place((RectTransform)bestText.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 90f), new Vector2(0f, 45f));
            if (newBest)
            {
                bestText.transform.localScale = Vector3.one * 0.6f;
                Tween.Scale(bestText.transform, 1f, 0.5f, Ease.OutBack, startDelay: 0.35f);
            }

            var button = UiKit.Button(group, "Restart", "RESTART", Theme.PineDark, Theme.BrassBright, Theme.Cream, 60f);
            UiKit.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(560f, 150f), new Vector2(0f, -100f));
            button.onClick.AddListener(() =>
            {
                button.interactable = false;
                onRestart?.Invoke();
            });

            var home = UiKit.Button(group, "MenuButton", "MENU", Theme.PineShadow, Theme.Brass, Theme.Cream, 48f);
            UiKit.Place((RectTransform)home.transform, new Vector2(0.5f, 0.5f), new Vector2(430f, 120f), new Vector2(0f, -245f));
            home.onClick.AddListener(() =>
            {
                home.interactable = false;
                onTitle?.Invoke();
            });

            group.localScale = Vector3.one * 0.8f;
            Tween.Alpha(cg, 1f, 0.3f, startDelay: 0.15f);
            Tween.Scale(group, 1f, 0.45f, Ease.OutBack, startDelay: 0.15f);
        }
    }
}
