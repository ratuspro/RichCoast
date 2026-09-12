using PrimeTween;
using RichCoast.Core;
using RichCoast.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.UI
{
    /// <summary>
    /// The top-of-screen HUD (port of the Phaser <c>HUD</c> + Zone A's queue row): a cream chrome
    /// bar with a wood base rule, a numberless milestone progress bar (left), the compact score
    /// total (centre, hero), and the queue row (right: balls-left count + next-ball preview). Pure
    /// consumer of <see cref="GameEvents"/>. The shown total is driven ONLY by harvest landings — a
    /// flying "+N" token (<see cref="ScoreFlyer"/>) — so it stays frozen during a round and jumps once.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const float BarHeight = 116f; // 42 design px × (1080/390)
        const float MilestoneW = 313f, MilestoneH = 56f;

        GameFeelSO feel;
        RectTransform overlay;
        TextMeshProUGUI scoreText;
        TextMeshProUGUI countText;
        Image previewImage;
        RectTransform milestoneFill;
        double shownTotal;
        Tween countPop, scorePop, countUp;

        public RectTransform ScoreAnchor => (RectTransform)scoreText.transform;
        public RectTransform CountAnchor => (RectTransform)countText.transform;

        public static HudView Build(Canvas hudCanvas, Canvas overlayCanvas, GameFeelSO feel)
        {
            var safe = UiKit.Stretch(UiKit.Panel(hudCanvas.transform, "SafeArea"));
            safe.gameObject.AddComponent<SafeArea>();
            var hud = safe.gameObject.AddComponent<HudView>();
            hud.feel = feel;
            hud.overlay = (RectTransform)overlayCanvas.transform;
            hud.BuildBar(safe);
            hud.Subscribe();
            return hud;
        }

        void BuildBar(RectTransform safe)
        {
            var bar = UiKit.Image(safe, "Bar", Theme.Cream);
            var barRt = (RectTransform)bar.transform;
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.sizeDelta = new Vector2(0f, BarHeight);
            barRt.anchoredPosition = Vector2.zero;

            // Base rule: a wood line with a thin brass accent above it.
            var rule = UiKit.Image(barRt, "BaseRule", Theme.PineDark);
            var ruleRt = (RectTransform)rule.transform;
            ruleRt.anchorMin = new Vector2(0f, 0f);
            ruleRt.anchorMax = new Vector2(1f, 0f);
            ruleRt.pivot = new Vector2(0.5f, 0f);
            ruleRt.sizeDelta = new Vector2(0f, 6f);
            var brass = UiKit.Image(barRt, "BrassRule", new Color(Theme.Brass.r, Theme.Brass.g, Theme.Brass.b, 0.8f));
            var brassRt = (RectTransform)brass.transform;
            brassRt.anchorMin = new Vector2(0f, 0f);
            brassRt.anchorMax = new Vector2(1f, 0f);
            brassRt.pivot = new Vector2(0.5f, 0f);
            brassRt.sizeDelta = new Vector2(0f, 3f);
            brassRt.anchoredPosition = new Vector2(0f, 6f);

            // Milestone bar (left): pine track, brass fill, no numbers.
            var track = UiKit.Image(barRt, "MilestoneTrack", Theme.PineShadow);
            UiKit.Place((RectTransform)track.transform, new Vector2(0f, 0.5f), new Vector2(MilestoneW, MilestoneH), new Vector2(38f, 0f));
            var trackOutline = track.gameObject.AddComponent<Outline>();
            trackOutline.effectColor = Theme.Ink;
            trackOutline.effectDistance = new Vector2(2f, -2f);
            var fill = UiKit.Image(track.transform, "MilestoneFill", Theme.BrassBright);
            milestoneFill = (RectTransform)fill.transform;
            milestoneFill.anchorMin = new Vector2(0f, 0f);
            milestoneFill.anchorMax = new Vector2(0f, 1f);
            milestoneFill.pivot = new Vector2(0f, 0.5f);
            milestoneFill.offsetMin = new Vector2(3f, 3f);
            milestoneFill.offsetMax = new Vector2(3f, -3f);
            milestoneFill.sizeDelta = new Vector2(0f, -6f);

            // Score (centre, hero).
            scoreText = UiKit.Text(barRt, "Score", "0", 78f, Theme.Ink);
            UiKit.Place((RectTransform)scoreText.transform, new Vector2(0.5f, 0.5f), new Vector2(500f, BarHeight), Vector2.zero);

            // Queue row (right): "N left" then the next-ball preview at the edge.
            previewImage = UiKit.Image(barRt, "NextPreview", Color.white);
            UiKit.Place((RectTransform)previewImage.transform, new Vector2(1f, 0.5f), new Vector2(66f, 66f), new Vector2(-38f, 0f));
            previewImage.preserveAspect = true;
            countText = UiKit.Text(barRt, "BallsLeft", "", 40f, Theme.Ink, TextAlignmentOptions.MidlineRight);
            UiKit.Place((RectTransform)countText.transform, new Vector2(1f, 0.5f), new Vector2(300f, BarHeight), new Vector2(-126f, 0f));
        }

        void Subscribe()
        {
            GameEvents.BallBufferChanged += SetBallsLeft;
            GameEvents.ProgressionChanged += e => SetMilestoneProgress(ProgressionCurve.MilestoneProgress(e.Level));
            GameEvents.ScoreHarvested += FlyHarvest;
        }

        public void SetNextTier(int tier)
        {
            previewImage.sprite = BallArt.SpriteForTier(tier);
        }

        void SetBallsLeft(int count)
        {
            countText.text = $"{count} left";
            countPop.Stop();
            var rt = (RectTransform)countText.transform;
            rt.localScale = Vector3.one * 1.35f;
            countPop = Tween.Scale(rt, 1f, 0.16f, Ease.OutBack);
        }

        void SetMilestoneProgress(double fraction)
        {
            float width = (MilestoneW - 6f) * (float)fraction;
            Tween.UISizeDelta(milestoneFill, new Vector2(width, milestoneFill.sizeDelta.y), 0.25f, Ease.OutCubic);
        }

        /// <summary>Fly a brass "+N" token from the harvest point up to the total; count up on landing.</summary>
        void FlyHarvest(ScoreHarvestedEvent e)
        {
            if (e.Amount <= 0) return;
            var start = new Vector2((float)e.ScreenX, (float)e.ScreenY);
            ScoreFlyer.Launch(overlay, $"+{NumberFormat.Compact(e.Amount)}", start, ScoreAnchor, feel.scoreFlyMs / 1000f, () => LandHarvest(e.Amount));
        }

        void LandHarvest(double amount)
        {
            double from = shownTotal;
            shownTotal += amount;
            double to = shownTotal;
            countUp.Stop();
            countUp = Tween.Custom(this, 0f, 1f, feel.scoreCountUpMs / 1000f, (self, t) =>
            {
                self.scoreText.text = NumberFormat.Compact(System.Math.Round(from + (to - from) * t));
            }, Ease.OutCubic).OnComplete(() => scoreText.text = NumberFormat.Compact(to), warnIfTargetDestroyed: false);
            scorePop.Stop();
            var rt = (RectTransform)scoreText.transform;
            rt.localScale = Vector3.one;
            scorePop = Tween.Scale(rt, 1.35f, 0.16f, Ease.InOutSine, cycles: 2, CycleMode.Yoyo);
        }
    }
}
