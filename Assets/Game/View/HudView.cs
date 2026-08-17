using System;
using RichCoast.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RichCoast.View
{
    /// <summary>
    /// Placeholder HUD: score total, the queue row (next-ball preview + buffer count), the score
    /// bar, and the game-over overlay. Built entirely in code with legacy uGUI text so the
    /// migration carries no font assets or TMP setup — the art pass replaces this wholesale.
    ///
    /// Safe-area aware: the player draws under the notch, so the chrome insets itself rather than
    /// hiding behind it.
    /// </summary>
    public sealed class HudView : MonoBehaviour, IHudView
    {
        private Text _scoreText;
        private Text _bufferText;
        private Image _nextBallImage;
        private RectTransform _scoreBarFill;
        private GameObject _gameOverPanel;
        private Text _gameOverText;
        private Button _restartButton;
        private RectTransform _safeArea;

        private Rect _appliedSafeArea;

        /// <summary>Raised when the player asks for a new run from the game-over overlay.</summary>
        public event Action RestartRequested;

        public static HudView Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, worldPositionStays: false);
            var hud = go.AddComponent<HudView>();
            hud.Build();
            return hud;
        }

        public void SetScore(double total) => _scoreText.text = FormatScore(total);

        public void SetBuffer(int count) => _bufferText.text = count.ToString();

        public void SetNextBall(int tier, TierMaterial material) => _nextBallImage.color = material.Def.BaseColor;

        public void SetScoreBar(double filled, double target)
        {
            var progress = target > 0 ? Mathf.Clamp01((float)(filled / target)) : 0f;
            _scoreBarFill.anchorMax = new Vector2(progress, 1f);
        }

        public void ShowGameOver(double finalScore, int level)
        {
            _gameOverText.text = $"GAME OVER\n\n{FormatScore(finalScore)}\nlevel {level}";
            _gameOverPanel.SetActive(true);
        }

        public void HideGameOver() => _gameOverPanel.SetActive(false);

        private void Update()
        {
            // Cheap guard rather than a per-frame layout pass: the safe area only changes on a
            // rotation or a system-bar toggle.
            if (Screen.safeArea != _appliedSafeArea) ApplySafeArea();
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Layout.Width, Layout.DesignScreenHeight);
            scaler.matchWidthOrHeight = 1f; // portrait: match height, so the width never crops chrome
            canvasGo.AddComponent<GraphicRaycaster>();

            _safeArea = NewRect("Safe Area", canvasGo.transform, Vector2.zero, Vector2.one);
            ApplySafeArea();

            BuildTopBar();
            BuildScoreBar();
            BuildGameOver();
        }

        private void BuildTopBar()
        {
            // The HUD chrome band, matching the design's 42-unit reservation at the top of Zone A.
            var bar = NewRect("Top Bar", _safeArea, new Vector2(0f, 1f), Vector2.one);
            bar.sizeDelta = new Vector2(0f, PhaseGeometry.HudHeight);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;

            _scoreText = NewText("Score", bar, TextAnchor.MiddleLeft, 24);
            Stretch(_scoreText.rectTransform, new Vector2(12f, 0f), new Vector2(-140f, 0f));

            // Queue row: the next ball and how many drops are left, read together as one unit.
            var preview = new GameObject("Next Ball");
            preview.transform.SetParent(bar, worldPositionStays: false);
            _nextBallImage = preview.AddComponent<Image>();
            _nextBallImage.sprite = PlaceholderArt.Disc;
            var previewRect = _nextBallImage.rectTransform;
            previewRect.anchorMin = previewRect.anchorMax = new Vector2(1f, 0.5f);
            previewRect.pivot = new Vector2(1f, 0.5f);
            previewRect.sizeDelta = new Vector2(26f, 26f);
            previewRect.anchoredPosition = new Vector2(-64f, 0f);

            _bufferText = NewText("Buffer", bar, TextAnchor.MiddleRight, 22);
            var bufferRect = _bufferText.rectTransform;
            bufferRect.anchorMin = bufferRect.anchorMax = new Vector2(1f, 0.5f);
            bufferRect.pivot = new Vector2(1f, 0.5f);
            bufferRect.sizeDelta = new Vector2(56f, 30f);
            bufferRect.anchoredPosition = new Vector2(-12f, 0f);
        }

        private void BuildScoreBar()
        {
            var track = NewRect("Score Bar", _safeArea, new Vector2(0f, 0f), new Vector2(1f, 0f));
            track.pivot = new Vector2(0.5f, 0f);
            track.sizeDelta = new Vector2(0f, 10f);
            track.anchoredPosition = new Vector2(0f, 8f);
            var background = track.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.15f);

            var fill = NewRect("Fill", track, Vector2.zero, new Vector2(0f, 1f));
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.79f, 0.60f, 0.20f); // brass
            _scoreBarFill = fill;
        }

        private void BuildGameOver()
        {
            var panel = NewRect("Game Over", _safeArea, Vector2.zero, Vector2.one);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.05f, 0.04f, 0.82f);

            _gameOverText = NewText("Result", panel, TextAnchor.MiddleCenter, 30);
            _gameOverText.color = Color.white;
            Stretch(_gameOverText.rectTransform, new Vector2(20f, 120f), new Vector2(-20f, -80f));

            var buttonRect = NewRect("Restart", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(200f, 56f);
            buttonRect.anchoredPosition = new Vector2(0f, 140f);
            var buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(0.79f, 0.60f, 0.20f);
            _restartButton = buttonRect.gameObject.AddComponent<Button>();
            _restartButton.targetGraphic = buttonImage;
            _restartButton.onClick.AddListener(() => RestartRequested?.Invoke());

            var label = NewText("Label", buttonRect, TextAnchor.MiddleCenter, 24);
            label.text = "RESTART";
            label.color = new Color(0.16f, 0.11f, 0.06f);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);

            _gameOverPanel = panel.gameObject;
            _gameOverPanel.SetActive(false);
        }

        private void ApplySafeArea()
        {
            _appliedSafeArea = Screen.safeArea;
            if (_safeArea == null) return;

            var min = _appliedSafeArea.position;
            var max = min + _appliedSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Values reach the billions by level 40; raw digits stop being readable long before that.</summary>
        private static string FormatScore(double value)
        {
            if (value < 1_000d) return Mathf.RoundToInt((float)value).ToString();
            if (value < 1_000_000d) return (value / 1_000d).ToString("0.0") + "K";
            if (value < 1_000_000_000d) return (value / 1_000_000d).ToString("0.0") + "M";
            if (value < 1_000_000_000_000d) return (value / 1_000_000_000d).ToString("0.0") + "B";
            return (value / 1_000_000_000_000d).ToString("0.0") + "T";
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text NewText(string name, Transform parent, TextAnchor alignment, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.28f, 0.19f, 0.11f); // warm-brown ink
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
