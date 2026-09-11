using System;
using PrimeTween;
using RichCoast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.UI
{
    /// <summary>
    /// The overlay fly-up (port of the Phaser <c>overlayFx.launchOverlayFlyer</c>): a token arcs from a
    /// screen point to a HUD anchor along a jittered quadratic bezier, shrinking, shedding a short
    /// fading trail, then fires <c>onArrive</c>. Lives on the top-most overlay canvas so it rides
    /// above every world object and HUD element.
    /// </summary>
    public static class ScoreFlyer
    {
        const float BowJitter = 110f; // reference px
        const float TrailFadeS = 0.22f;

        public static void Launch(RectTransform overlay, string label, Vector2 startScreen, RectTransform target, float durationS, Action onArrive)
        {
            var canvas = overlay.GetComponentInParent<Canvas>();
            var cam = canvas.worldCamera;
            var start = ToLocal(overlay, startScreen, cam);
            var endScreen = RectTransformUtility.WorldToScreenPoint(cam, target.position);
            var end = ToLocal(overlay, endScreen, cam);
            var control = new Vector2((start.x + end.x) / 2f + UnityEngine.Random.Range(-BowJitter, BowJitter), Mathf.Lerp(start.y, end.y, 0.45f));

            var token = UiKit.Text(overlay, "Flyer", label, 60f, Theme.BrassBright);
            token.outlineWidth = 0.25f;
            token.outlineColor = Theme.Ink;
            var rt = (RectTransform)token.transform;
            rt.sizeDelta = new Vector2(600f, 120f);
            rt.anchoredPosition = start;

            int frame = 0;
            Tween.Custom(0f, 1f, durationS, t =>
            {
                if (rt == null) return;
                var p = Bezier(start, control, end, t);
                rt.anchoredPosition = p;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
                if (frame++ % 3 == 0) ShedTrail(overlay, p, Theme.BrassBright);
            }, Ease.InOutSine).OnComplete(() =>
            {
                if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
                onArrive?.Invoke();
            });
        }

        static Vector2 ToLocal(RectTransform overlay, Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, cam, out var local);
            return local;
        }

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        static void ShedTrail(RectTransform overlay, Vector2 at, Color color)
        {
            var mote = UiKit.Image(overlay, "Mote", new Color(color.r, color.g, color.b, 0.55f), BallArt.SoftDot);
            var rt = (RectTransform)mote.transform;
            rt.sizeDelta = new Vector2(22f, 22f);
            rt.anchoredPosition = at;
            Tween.Alpha(mote, 0f, TrailFadeS);
            Tween.Scale(rt, 0.3f, TrailFadeS).OnComplete(() => { if (rt != null) UnityEngine.Object.Destroy(rt.gameObject); });
        }
    }
}
