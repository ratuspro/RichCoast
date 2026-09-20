using System;
using System.Collections.Generic;
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
        // Bottom-right, thumb-reachable, and clear of the top strip the aim drag owns.
        //
        // The vertical inset is NOT taste. In the A framing the bottom ~157 design px of the screen is
        // Zone B's barrier band and the golden chute that hangs above it, and phase A exists partly to
        // telegraph where that chute is — a button parked in the corner covers it whenever the mouth
        // rolls onto a right-hand column. 460 ref px (166 design px) puts the button in the empty entry
        // band between Zone C's door and the barrier instead, which is blank in every arena.
        const float TiltW = 188f, TiltH = 112f, TiltInset = 40f, TiltLift = 460f;
        const float PipSize = 22f, PipGap = 14f, PipRise = 26f;
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
        Button tiltButton;
        CanvasGroup tiltGroup;
        Image tiltFill;
        readonly List<Image> tiltPips = new List<Image>();
        Func<bool> canTilt;
        /// <summary>Raised on a live press; the composition root points it at the run's Zone A.</summary>
        Action OnTiltPressed;
        Tween tiltPop;
        double shownTotal;
        Tween countPop, scorePop, countUp, barFlash, milestonePop, ribbonFade, ribbonSlide, tiltFade;

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
            hud.BuildTilt(safe);
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
        /// <summary>
        /// The TILT button and its three pips. A panic button, so there is no confirm step — what keeps
        /// a mis-tap from being ruinous is the position (far from the aim strip) and the cooldown, not a
        /// dialog. Greys out when the charges are gone or the board cannot be shaken.
        /// </summary>
        void BuildTilt(RectTransform safe)
        {
            tiltButton = UiKit.Button(safe, "Tilt", "TILT", Theme.PineDark, Theme.BrassBright, Theme.Cream, 42f);
            tiltFill = tiltButton.GetComponent<Image>();
            UiKit.Themed(tiltFill, ThemeKey.PineDark);
            var rt = (RectTransform)tiltButton.transform;
            UiKit.Place(rt, new Vector2(1f, 0f), new Vector2(TiltW, TiltH), new Vector2(-TiltInset, TiltLift));
            tiltGroup = tiltButton.gameObject.AddComponent<CanvasGroup>();
            tiltButton.onClick.AddListener(() => { if (canTilt == null || canTilt()) OnTiltPressed?.Invoke(); });
        }

        /// <summary>Lay out the pips for a run's allowance and light them all.</summary>
        void BuildPips(RectTransform anchor, int charges)
        {
            foreach (var pip in tiltPips) if (pip != null) Destroy(pip.gameObject);
            tiltPips.Clear();
            float span = charges * PipSize + (charges - 1) * PipGap;
            for (int i = 0; i < charges; i++)
            {
                var pip = UiKit.Image(anchor, $"TiltPip{i}", Theme.BrassBright);
                UiKit.Themed(pip, ThemeKey.BrassBright);
                float x = -span / 2f + PipSize / 2f + i * (PipSize + PipGap);
                UiKit.Place((RectTransform)pip.transform, new Vector2(0.5f, 1f),
                    new Vector2(PipSize, PipSize), new Vector2(x, PipRise));
                tiltPips.Add(pip);
            }
        }

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
            GameEvents.TiltUsed += e => SetTiltsLeft(e.Remaining);
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
            var start = new Vector2(UnityEngine.Random.Range(margin, Screen.width - margin), Screen.safeArea.y + 5f);
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
        /// <summary>
        /// Wire the button to the live run. Called by the composition root on every run start, because
        /// the HUD outlives a run but <c>ZoneASystem</c> does not.
        /// </summary>
        public void BindTilt(Func<bool> can, Action onPressed, int charges, int remaining)
        {
            canTilt = can;
            OnTiltPressed = onPressed;
            BuildPips((RectTransform)tiltButton.transform, charges);
            SetTiltsLeft(remaining);
        }

        /// <summary>Spent pips go dark rather than vanishing, so the allowance still reads as three.</summary>
        void SetTiltsLeft(int remaining)
        {
            for (int i = 0; i < tiltPips.Count; i++)
            {
                if (tiltPips[i] == null) continue;
                bool lit = i < remaining;
                var themed = tiltPips[i].GetComponent<Game.Themed>();
                if (themed != null) themed.enabled = lit;
                tiltPips[i].color = lit ? Theme.BrassBright : Theme.PineShadow;
            }
            tiltPop.Stop();
            var rt = (RectTransform)tiltButton.transform;
            rt.localScale = Vector3.one;
            tiltPop = Tween.PunchScale(rt, Vector3.one * 0.12f, 0.22f, 4);
        }

        /// <summary>
        /// The tilt is a phase-A affordance: the board it shakes is off screen anywhere else, and in the
        /// B framing the button lands on top of Zone B's score bar. So it leaves with the ribbon.
        /// </summary>
        void FadeTilt(float alpha, float seconds)
        {
            if (tiltGroup == null) return;
            tiltFade.Stop();
            tiltFade = Tween.Alpha(tiltGroup, alpha, seconds);
        }

        void Update()
        {
            if (tiltButton == null) return;
            bool live = canTilt != null && canTilt();
            if (tiltButton.interactable != live) tiltButton.interactable = live;
        }

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
                    FadeTilt(phase == GamePhase.A ? 1f : 0f, seconds);
                    break;
                default:
                    ribbonFade = Tween.Alpha(ribbonGroup, 0f, seconds);
                    ribbonSlide = Tween.UIAnchoredPositionY(ribbon, ribbonBaseY + RibbonSlide, seconds, Ease.InSine);
                    FadeTilt(0f, seconds);
                    break;
            }
        }
    }
}
