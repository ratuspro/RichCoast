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
    /// total (centre, hero), the queue row (right: balls-left count + next-ball preview), and the
    /// phase ribbon hanging under the bar ("DROP" / "TAP THE DOOR"). Pure consumer of
    /// <see cref="GameEvents"/>. The shown total is driven ONLY by harvest landings — a flying "+N"
    /// token (<see cref="ScoreFlyer"/>) — so it stays frozen during a round and jumps once; the
    /// buffer count pops per landed slot, each slot's brass particle arriving as it lands.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        const float BarHeight = 116f; // 42 design px × (1080/390)
        const float MilestoneW = 313f, MilestoneH = 56f;
        const float RibbonW = 380f, RibbonH = 54f, RibbonGap = 14f, RibbonSlide = 26f;
        const float DotSize = 24f;
        const float RefPxPerDesignPx = 1080f / 390f;

        GameFeelSO feel;
        RectTransform overlay;
        Image barImage;
        TextMeshProUGUI scoreText;
        TextMeshProUGUI countText;
        Image previewImage;
        RectTransform milestoneFill;
        RectTransform ribbon;
        CanvasGroup ribbonGroup;
        TextMeshProUGUI ribbonLabel;
        float ribbonBaseY;
        double shownTotal;
        Tween countPop, scorePop, countUp, barFlash, milestonePop, ribbonFade, ribbonSlide;

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
            hud.BuildRibbon(safe);
            hud.Subscribe();
            return hud;
        }

        void BuildBar(RectTransform safe)
        {
            barImage = UiKit.Image(safe, "Bar", Theme.Cream);
            UiKit.Themed(barImage, ThemeKey.Cream);
            var barRt = (RectTransform)barImage.transform;
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.sizeDelta = new Vector2(0f, BarHeight);
            barRt.anchoredPosition = Vector2.zero;

            // Base rule: a wood line with a thin brass accent above it.
            var rule = UiKit.Image(barRt, "BaseRule", Theme.PineDark);
            UiKit.Themed(rule, ThemeKey.PineDark);
            var ruleRt = (RectTransform)rule.transform;
            ruleRt.anchorMin = new Vector2(0f, 0f);
            ruleRt.anchorMax = new Vector2(1f, 0f);
            ruleRt.pivot = new Vector2(0.5f, 0f);
            ruleRt.sizeDelta = new Vector2(0f, 6f);
            var brass = UiKit.Image(barRt, "BrassRule", new Color(Theme.Brass.r, Theme.Brass.g, Theme.Brass.b, 0.8f));
            UiKit.Themed(brass, ThemeKey.Brass);
            var brassRt = (RectTransform)brass.transform;
            brassRt.anchorMin = new Vector2(0f, 0f);
            brassRt.anchorMax = new Vector2(1f, 0f);
            brassRt.pivot = new Vector2(0.5f, 0f);
            brassRt.sizeDelta = new Vector2(0f, 3f);
            brassRt.anchoredPosition = new Vector2(0f, 6f);

            // Milestone bar (left): pine track, brass fill, no numbers.
            var track = UiKit.Image(barRt, "MilestoneTrack", Theme.PineShadow);
            UiKit.Themed(track, ThemeKey.PineShadow);
            UiKit.Place((RectTransform)track.transform, new Vector2(0f, 0.5f), new Vector2(MilestoneW, MilestoneH), new Vector2(38f, 0f));
            var trackOutline = track.gameObject.AddComponent<Outline>();
            trackOutline.effectColor = Theme.Ink;
            trackOutline.effectDistance = new Vector2(2f, -2f);
            UiKit.Themed(trackOutline, ThemeKey.Ink);
            var fill = UiKit.Image(track.transform, "MilestoneFill", Theme.BrassBright);
            UiKit.Themed(fill, ThemeKey.BrassBright);
            milestoneFill = (RectTransform)fill.transform;
            milestoneFill.anchorMin = new Vector2(0f, 0f);
            milestoneFill.anchorMax = new Vector2(0f, 1f);
            milestoneFill.pivot = new Vector2(0f, 0.5f);
            milestoneFill.offsetMin = new Vector2(3f, 3f);
            milestoneFill.offsetMax = new Vector2(3f, -3f);
            milestoneFill.sizeDelta = new Vector2(0f, -6f);

            // Score (centre, hero).
            scoreText = UiKit.Text(barRt, "Score", "0", 78f, Theme.Ink);
            Game.Themed.Bind(scoreText, ThemeKey.Ink);
            UiKit.Place((RectTransform)scoreText.transform, new Vector2(0.5f, 0.5f), new Vector2(500f, BarHeight), Vector2.zero);

            // Queue row (right): "N left" then the next-ball preview at the edge.
            previewImage = UiKit.Image(barRt, "NextPreview", Color.white);
            UiKit.Place((RectTransform)previewImage.transform, new Vector2(1f, 0.5f), new Vector2(66f, 66f), new Vector2(-38f, 0f));
            previewImage.preserveAspect = true;
            countText = UiKit.Text(barRt, "BallsLeft", "", 40f, Theme.Ink, TextAlignmentOptions.MidlineRight);
            Game.Themed.Bind(countText, ThemeKey.Ink);
            UiKit.Place((RectTransform)countText.transform, new Vector2(1f, 0.5f), new Vector2(300f, BarHeight), new Vector2(-126f, 0f));
        }

        /// <summary>
        /// The phase ribbon: a brass sign hanging under the bar's base rule, centred, that names what
        /// the player does now. It slides up + fades out when a pan starts and drops back in with the
        /// new word when the pan lands. Sits in the 78 design px between the ceiling and the spawn row,
        /// clear of the aim ghost.
        /// </summary>
        void BuildRibbon(RectTransform safe)
        {
            var pill = UiKit.Image(safe, "PhaseRibbon", Theme.Brass);
            UiKit.Themed(pill, ThemeKey.Brass);
            ribbon = (RectTransform)pill.transform;
            UiKit.Place(ribbon, new Vector2(0.5f, 1f), new Vector2(RibbonW, RibbonH), new Vector2(0f, -(BarHeight + RibbonGap)));
            ribbonBaseY = ribbon.anchoredPosition.y;
            var outline = pill.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.Ink;
            outline.effectDistance = new Vector2(2f, -2f);
            UiKit.Themed(outline, ThemeKey.Ink);
            ribbonLabel = UiKit.Text(ribbon, "PhaseRibbonLabel", "", 34f, Theme.Ink);
            Game.Themed.Bind(ribbonLabel, ThemeKey.Ink);
            ribbonLabel.characterSpacing = 8f;
            UiKit.Stretch((RectTransform)ribbonLabel.transform);
            ribbonGroup = pill.gameObject.AddComponent<CanvasGroup>();
            ribbonGroup.alpha = 0f;
            ribbonGroup.blocksRaycasts = false;
            ribbonGroup.interactable = false;
        }

        void Subscribe()
        {
            GameEvents.BallBufferChanged += SetBallsLeft;
            GameEvents.BufferSlotLaunched += FlyBufferSlot;
            GameEvents.ProgressionChanged += e =>
            {
                SetMilestoneProgress(ProgressionCurve.MilestoneProgress(e.Level));
                PopMilestone();
            };
            GameEvents.ScoreHarvested += FlyHarvest;
            GameEvents.PhaseChanged += OnPhaseChanged;
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

        /// <summary>
        /// One refill slot launched: fly a brass dot from a random point along the screen bottom (the
        /// score bar's on-screen home is off-screen below in phase A) up to the balls-left count over
        /// the feel file's flight time — the same time Zone A waits before landing the slot, so the
        /// dot arrives as the count pops. The dot is purely decorative: Zone A lands the slot by its
        /// own clock, and the count pop is driven by <c>BallBufferChanged</c>, not by the dot's arrival.
        /// </summary>
        void FlyBufferSlot(int index)
        {
            float margin = Screen.width * 0.06f;
            var start = new Vector2(Random.Range(margin, Screen.width - margin), Screen.safeArea.y + 5f);
            float bow = feel.bufferBowJitter * RefPxPerDesignPx;
            ScoreFlyer.LaunchDot(overlay, "BufferDot", Theme.BrassBright, DotSize, start, CountAnchor, feel.bufferFlightMs / 1000f, bow, null);
        }

        void SetMilestoneProgress(double fraction)
        {
            float width = (MilestoneW - 6f) * (float)fraction;
            Tween.UISizeDelta(milestoneFill, new Vector2(width, milestoneFill.sizeDelta.y), 0.25f, Ease.OutCubic);
        }

        /// <summary>A level landed: the milestone fill puffs vertically in place.</summary>
        void PopMilestone()
        {
            milestonePop.Stop();
            milestoneFill.localScale = Vector3.one;
            milestonePop = Tween.Scale(milestoneFill, new Vector3(1f, 1.5f, 1f), 0.13f, Ease.InOutSine, cycles: 2, CycleMode.Yoyo);
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

            // The landing beat: the chrome flashes brass and settles back to cream, with a light tap.
            barFlash.Stop();
            barImage.color = Theme.BrassBright;
            barFlash = Tween.Color(barImage, Theme.Cream, feel.harvestFlashMs / 1000f, Ease.OutCubic)
                .OnComplete(this, self => self.barImage.color = Theme.Cream); // land on the palette in force NOW (a cross-fade may have moved it)
            Haptics.Pulse(feel.harvestHapticMs, feel.harvestHapticAmp);
        }

        /// <summary>Pan starting (AToB / BToA): the ribbon lifts away. Pan landed (A / B): it drops back in with the new word.</summary>
        void OnPhaseChanged(GamePhase phase)
        {
            float seconds = feel.ribbonMs / 1000f;
            ribbonFade.Stop();
            ribbonSlide.Stop();
            switch (phase)
            {
                case GamePhase.A:
                case GamePhase.B:
                    ribbonLabel.text = phase == GamePhase.A ? "DROP" : "TAP THE DOOR";
                    ribbon.anchoredPosition = new Vector2(0f, ribbonBaseY + RibbonSlide);
                    ribbonFade = Tween.Alpha(ribbonGroup, 1f, seconds);
                    ribbonSlide = Tween.UIAnchoredPositionY(ribbon, ribbonBaseY, seconds, Ease.OutBack);
                    break;
                default:
                    ribbonFade = Tween.Alpha(ribbonGroup, 0f, seconds);
                    ribbonSlide = Tween.UIAnchoredPositionY(ribbon, ribbonBaseY + RibbonSlide, seconds, Ease.InSine);
                    break;
            }
        }
    }
}
