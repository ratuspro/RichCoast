using System;
using PrimeTween;
using RichCoast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RichCoast.Game
{
    /// <summary>
    /// Drag-to-aim input for Zone A (port of the Phaser <c>AimController</c>, on the new Input
    /// System). The current ball is a body-less ghost that tracks the pointer's X along the spawn
    /// row from ANYWHERE on screen; releasing drops a real physics ball through <see cref="Dropped"/>.
    /// Owns the <see cref="BallQueue"/> (current + next). Physics only begin on release.
    /// </summary>
    public sealed class AimController
    {
        readonly Camera camera;
        readonly BoardGeometry geometry;
        readonly BallFactory factory;
        readonly GameFeelSO feel;
        readonly Transform ghost;
        readonly SpriteRenderer ghostSprite;
        readonly LineRenderer guide;
        readonly Func<double> clockMs;

        public BallQueue Queue { get; }

        bool dragging;
        bool disabled;   // permanent (game over)
        bool frozen;     // reversible (milestone zoom / phase)
        bool dropLocked; // soft (buffer empty)
        float aimX;
        double dropReadyAt;

        /// <summary>(x, tier) — the player released the ball.</summary>
        public event Action<float, int> Dropped;
        /// <summary>Queue contents changed (current/next) — the HUD preview re-reads it.</summary>
        public event Action QueueChanged;

        public AimController(Transform parent, Camera camera, BoardGeometry geometry, BallFactory factory, GameFeelSO feel, BallQueue queue, Func<double> clockMs)
        {
            this.camera = camera;
            this.geometry = geometry;
            this.factory = factory;
            this.feel = feel;
            this.clockMs = clockMs;
            Queue = queue;

            var ghostGo = new GameObject("AimGhost");
            ghostGo.transform.SetParent(parent, false);
            ghost = ghostGo.transform;
            ghostSprite = ghostGo.AddComponent<SpriteRenderer>();
            ghostSprite.sortingOrder = 12;

            var guideGo = new GameObject("DropGuide");
            guideGo.transform.SetParent(parent, false);
            guide = guideGo.AddComponent<LineRenderer>();
            guide.useWorldSpace = true;
            guide.positionCount = 2;
            guide.textureMode = LineTextureMode.Tile;
            guide.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = DashTexture() };
            guide.startColor = guide.endColor = new Color(Theme.InkSoft.r, Theme.InkSoft.g, Theme.InkSoft.b, 0.55f);
            Themed.Bind(guide, ThemeKey.InkSoft);
            guide.sortingOrder = 9;
            guide.alignment = LineAlignment.TransformZ;

            aimX = 0f;
            SyncQueueVisuals();
        }

        /// <summary>Permanent freeze + hide (game over).</summary>
        public void Disable()
        {
            disabled = true;
            dragging = false;
            SetVisible(false);
        }

        /// <summary>Reversible freeze for milestone zooms / phase locks: blocks aiming AND dropping, hides the ghost.</summary>
        public void SetFrozen(bool on)
        {
            frozen = on;
            dragging = false;
            SetVisible(!on && !disabled);
        }

        /// <summary>Soft-lock: block drops without hiding the ghost or disabling aiming (buffer empty).</summary>
        public void SetDropLocked(bool locked) => dropLocked = locked;

        /// <summary>Re-read the queue's current/next into the ghost (after a re-roll or seed).</summary>
        public void RefreshQueue() => SyncQueueVisuals();

        public void Tick()
        {
            if (disabled || frozen) return;
            var pointer = Pointer.current;
            if (pointer == null) return;
            bool pressed = pointer.press.isPressed;
            if (pressed && !dragging)
            {
                if (clockMs() < dropReadyAt) return; // brief post-drop cooldown
                dragging = true;
                MoveAimTo(WorldX(pointer.position.ReadValue()));
            }
            else if (pressed && dragging)
            {
                MoveAimTo(WorldX(pointer.position.ReadValue()));
            }
            else if (!pressed && dragging)
            {
                dragging = false;
                if (dropLocked) return;
                int tier = Queue.Pop();
                Dropped?.Invoke(aimX, tier);
                SyncQueueVisuals();
                dropReadyAt = clockMs() + feel.dropCooldownMs;
            }
        }

        float WorldX(Vector2 screen) => camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f)).x;

        void MoveAimTo(float x)
        {
            float radius = factory.RadiusForTier(Queue.CurrentTier);
            aimX = (float)BallMath.ClampSpawnX(x, radius, geometry.MinX, geometry.MaxX);
            ghost.position = new Vector3(aimX, geometry.SpawnY, 0f);
            DrawGuide();
        }

        void DrawGuide()
        {
            float radius = factory.RadiusForTier(Queue.CurrentTier);
            float top = geometry.SpawnY - radius;
            float bottom = geometry.RampYAt(aimX);
            guide.SetPosition(0, new Vector3(aimX, top, 0f));
            guide.SetPosition(1, new Vector3(aimX, bottom, 0f));
            const float w = 0.08f;
            guide.startWidth = guide.endWidth = w;
            // Tile the dash texture along the length: one texture repeat per ~0.5 units.
            guide.material.mainTextureScale = new Vector2(1f / 0.5f * w, 1f);
        }

        void SyncQueueVisuals()
        {
            int tier = Queue.CurrentTier;
            ghostSprite.sprite = BallArt.SpriteForTier(tier);
            float d = factory.RadiusForTier(tier) * 2f;
            ghost.localScale = new Vector3(d, d, 1f);
            MoveAimTo(aimX);
            QueueChanged?.Invoke();
        }

        /// <summary>
        /// Fade the ghost + guide in or out over <c>aimFadeMs</c> instead of switching them off — a phase
        /// pan or milestone freeze reads as the aim receding, not vanishing. The renderers only disable
        /// once a fade-out lands; the freeze itself (input) is immediate regardless.
        /// </summary>
        void SetVisible(bool on)
        {
            visibleTarget = on;
            float seconds = feel.aimFadeMs / 1000f;
            ghostFade.Stop();
            guideFade.Stop();
            if (on)
            {
                ghostSprite.enabled = true;
                guide.enabled = true;
            }
            float ghostAlpha = on ? 1f : 0f;
            float guideAlpha = on ? GuideAlpha : 0f;
            ghostFade = Tween.Alpha(ghostSprite, ghostAlpha, seconds);
            var c = guide.startColor;
            guideFade = Tween.Custom(this, c.a, guideAlpha, seconds, (self, a) =>
            {
                var g = self.guide.startColor;
                g.a = a;
                self.guide.startColor = self.guide.endColor = g;
            }).OnComplete(this, self =>
            {
                if (self.visibleTarget) return;
                self.ghostSprite.enabled = false;
                self.guide.enabled = false;
            });
        }

        const float GuideAlpha = 0.55f;
        bool visibleTarget = true;
        Tween ghostFade, guideFade;

        static Texture2D dash;

        /// <summary>A 16×4 texture: 9 opaque texels then 7 clear — tiled along the guide for a dashed line.</summary>
        static Texture2D DashTexture()
        {
            if (dash != null) return dash;
            dash = new Texture2D(16, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 16; x++)
                dash.SetPixel(x, y, x < 9 ? Color.white : Color.clear);
            dash.Apply();
            return dash;
        }
    }
}
