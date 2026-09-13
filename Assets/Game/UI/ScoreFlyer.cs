using System;
using PrimeTween;
using RichCoast.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.UI
{
    /// <summary>
    /// The overlay fly-up (port of the Phaser <c>overlayFx.launchOverlayFlyer</c>): a token arcs from a
    /// screen point to a HUD anchor along a jittered quadratic bezier, shrinking, shedding a short
    /// fading trail, then fires <c>onArrive</c>. Two tokens ride it: the harvest "+N" text and the
    /// buffer-refill brass dot. Lives on the top-most overlay canvas so it rides above every world
    /// object and HUD element.
    /// </summary>
    public static class ScoreFlyer
    {
        const float TextBowJitter = 110f; // reference px
        const float TrailFadeS = 0.22f;

        /// <summary>Fly a brass "+N" text token to <paramref name="target"/>.</summary>
        public static void Launch(RectTransform overlay, string label, Vector2 startScreen, RectTransform target, float durationS, Action onArrive)
        {
            var token = UiKit.Text(overlay, "Flyer", label, 60f, Theme.BrassBright);
            token.outlineWidth = 0.25f;
            token.outlineColor = Theme.Ink;
            var rt = (RectTransform)token.transform;
            rt.sizeDelta = new Vector2(600f, 120f);
            Fly(overlay, rt, startScreen, target, durationS, TextBowJitter, 0.55f, Theme.BrassBright, onArrive);
        }

        /// <summary>Fly a small solid dot (a buffer-refill particle) to <paramref name="target"/>, at constant size.</summary>
        public static void LaunchDot(RectTransform overlay, string name, Color color, float size, Vector2 startScreen, RectTransform target, float durationS, float bowJitter, Action onArrive)
        {
            var dot = UiKit.Image(overlay, name, color, BallArt.Disc);
            var rt = (RectTransform)dot.transform;
            rt.sizeDelta = new Vector2(size, size);
            Fly(overlay, rt, startScreen, target, durationS, bowJitter, 1f, color, onArrive);
        }

        static void Fly(RectTransform overlay, RectTransform rt, Vector2 startScreen, RectTransform target, float durationS, float bowJitter, float scaleTo, Color trailColor, Action onArrive)
        {
            var canvas = overlay.GetComponentInParent<Canvas>();
            var cam = canvas.worldCamera;
            var start = ToLocal(overlay, startScreen, cam);
            var endScreen = RectTransformUtility.WorldToScreenPoint(cam, target.position);
            var end = ToLocal(overlay, endScreen, cam);
            var control = new Vector2((start.x + end.x) / 2f + UnityEngine.Random.Range(-bowJitter, bowJitter), Mathf.Lerp(start.y, end.y, 0.45f));
            rt.anchoredPosition = start;

            int frame = 0;
            // The token is the tween's target, so a scene unload mid-flight silently ends the flight
            // (no landing on a HUD that no longer exists).
            Tween.Custom(rt, 0f, 1f, durationS, (r, t) =>
            {
                var p = Bezier(start, control, end, t);
                r.anchoredPosition = p;
                r.localScale = Vector3.one * Mathf.Lerp(1f, scaleTo, t);
                if (frame++ % 3 == 0) ShedTrail(overlay, p, trailColor);
            }, Ease.InOutSine).OnComplete(() =>
            {
                if (rt != null) UnityEngine.Object.Destroy(rt.gameObject);
                onArrive?.Invoke();
            }, warnIfTargetDestroyed: false);
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
            Tween.Scale(rt, 0.3f, TrailFadeS).OnComplete(() => { if (rt != null) UnityEngine.Object.Destroy(rt.gameObject); }, warnIfTargetDestroyed: false);
        }
    }
}
