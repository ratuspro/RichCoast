using PrimeTween;
using RichCoast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RichCoast.Game
{
    /// <summary>
    /// Zone C — the trap-door (port of <c>ZoneCSystem.ts</c>). Owns the cooldown lock (driven by Zone
    /// B's busy/empty events), the milestone-zoom lock, the phase lock (armed only in phase B) and a
    /// row of nine evenly-spaced brass markers across the band; while armed, the lit one steps
    /// edge→edge and back. A tap ANYWHERE on screen freezes on the lit position: that column becomes
    /// the Zone B entry. <c>ZoneBBusy</c> fires up front (so Zone A's stalemate check stays blocked
    /// while the ball is mid-transit), then a suck→pop cosmetic runs and <c>BallDropped</c> is emitted
    /// at the frozen column when it lands — WHERE a ball enters Zone B is a timing skill.
    /// </summary>
    public sealed class ZoneCSystem
    {
        const float MarkerRadiusPx = 6f;
        const int MarkerOrder = 50;

        readonly Board board;
        readonly BoardGeometry geometry;
        readonly GameFeelSO feel;
        readonly Transform root;
        readonly SpriteRenderer[] dots = new SpriteRenderer[DesignSpace.SweepPositions];
        readonly SpriteRenderer[] rings = new SpriteRenderer[DesignSpace.SweepPositions];
        readonly double[] positionsDesignX = new double[DesignSpace.SweepPositions];

        bool locked;          // Zone B has balls in flight (or a suck is in transit)
        bool zoomLocked;      // Zone A's milestone zoom-out is animating
        bool modalLocked;     // a modal dialog is up (its scrim only blocks uGUI; the tap reads the pointer)
        bool phaseLocked = true; // armed only in phase B; the run boots in A
        bool over;
        float sweepMs;
        int activeIndex;
        int styledIndex = -1;

        /// <summary>True when a tap would fire the door.</summary>
        public bool IsArmed => !(locked || zoomLocked || phaseLocked || modalLocked || over);
        public int ActiveIndex => activeIndex;

        public ZoneCSystem(Transform parent, Board board, BoardGeometry geometry, GameFeelSO feel)
        {
            this.board = board;
            this.geometry = geometry;
            this.feel = feel;
            root = new GameObject("ZoneC").transform;
            root.SetParent(parent, false);

            BuildBand();
            BuildMarkers();

            GameEvents.ZoneBBusy += () => SetLocked(true);
            GameEvents.ZoneBEmpty += () => SetLocked(false);
            GameEvents.ArenaZoom += active =>
            {
                zoomLocked = active;
                if (!active) sweepMs = 0f; // restart the sweep from the edge when the zoom lands
            };
            GameEvents.PhaseChanged += phase =>
            {
                phaseLocked = phase != GamePhase.B;
                if (!phaseLocked) sweepMs = 0f;
            };
            GameEvents.GameOver += _ => over = true;
            GameEvents.ModalOpen += open =>
            {
                modalLocked = open;
                if (!open) sweepMs = 0f; // restart the sweep from the edge when the dialog closes
            };
        }

        /// <summary>
        /// The door band reads as a seamless continuation of Zone A's wooden funnel: same pine fill,
        /// no top border. Only the bottom edge carries a divider, marking the separation from Zone B.
        /// </summary>
        void BuildBand()
        {
            float x0 = geometry.ZoneBMinX - 0.5f, x1 = geometry.ZoneBMaxX + 0.5f;
            WorldArt.Rect(root, "Band", x0, x1, geometry.ZoneCBottomY, 0f, ThemeKey.Pine, -9);
            float t = BoardGeometry.Units(2);
            WorldArt.Rect(root, "Divider", x0, x1, geometry.ZoneCBottomY - t / 2f, geometry.ZoneCBottomY + t / 2f, ThemeKey.PineShadow, -5);
        }

        /// <summary>Nine markers along the band, inset one ball radius from each Zone B edge so a ball can never spawn into a side wall.</summary>
        void BuildMarkers()
        {
            double minX = DesignSpace.SweepMargin, maxX = DesignSpace.Width - DesignSpace.SweepMargin;
            float d = BoardGeometry.Units(MarkerRadiusPx * 2f);
            for (int i = 0; i < DesignSpace.SweepPositions; i++)
            {
                positionsDesignX[i] = DoorMath.SweepPositionX(i, DesignSpace.SweepPositions, minX, maxX);
                var pos = new Vector3(geometry.DesignXToWorld(positionsDesignX[i]), geometry.DoorMouthY, 0f);

                var ring = new GameObject($"Ring{i}");
                ring.transform.SetParent(root, false);
                ring.transform.localPosition = pos;
                ring.transform.localScale = new Vector3(d * 1.3f + BoardGeometry.Units(4f), d * 1.3f + BoardGeometry.Units(4f), 1f);
                rings[i] = ring.AddComponent<SpriteRenderer>();
                rings[i].sprite = BallArt.Disc;
                rings[i].color = Theme.Cream;
                Themed.Bind(rings[i], ThemeKey.Cream);
                rings[i].sortingOrder = MarkerOrder - 1;

                var dot = new GameObject($"Marker{i}");
                dot.transform.SetParent(root, false);
                dot.transform.localPosition = pos;
                dot.transform.localScale = new Vector3(d, d, 1f);
                dots[i] = dot.AddComponent<SpriteRenderer>();
                dots[i].sprite = BallArt.Disc;
                dots[i].sortingOrder = MarkerOrder;
                StyleDot(i, false);
            }
            SetMarkersVisible(false);
        }

        /// <summary>Polished-brass glow when active, dim brass stud when not.</summary>
        void StyleDot(int i, bool active)
        {
            float d = BoardGeometry.Units(MarkerRadiusPx * 2f);
            if (active)
            {
                dots[i].color = Theme.BrassBright;
                Themed.Bind(dots[i], ThemeKey.BrassBright);
                dots[i].transform.localScale = new Vector3(d * 1.3f, d * 1.3f, 1f);
                rings[i].enabled = true;
            }
            else
            {
                var c = Theme.Brass;
                dots[i].color = new Color(c.r, c.g, c.b, 0.45f);
                Themed.Bind(dots[i], ThemeKey.Brass);
                dots[i].transform.localScale = new Vector3(d, d, 1f);
                rings[i].enabled = false;
            }
        }

        void SetMarkersVisible(bool on)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].enabled = on;
                rings[i].enabled = on && i == activeIndex;
            }
        }

        /// <summary>
        /// The lit position is driven straight off the lock state every frame — nothing to get stuck.
        /// Armed: the lit index steps edge→edge and back at (sweep / 8) per step. Locked: all markers
        /// hidden. Because this reads the current state each tick, the sweep reappears the instant
        /// Zone B clears, regardless of how the busy/empty events interleave.
        /// </summary>
        public void Tick(float deltaMs)
        {
            if (!IsArmed)
            {
                if (styledIndex != -1) SetMarkersVisible(false);
                styledIndex = -1;
                return;
            }
            sweepMs += deltaMs;
            float stepMs = feel.sweepMs / (DesignSpace.SweepPositions - 1);
            activeIndex = DoorMath.SweepIndex(sweepMs, stepMs, DesignSpace.SweepPositions);
            if (activeIndex != styledIndex)
            {
                if (styledIndex == -1) SetMarkersVisible(true);
                for (int i = 0; i < dots.Length; i++) StyleDot(i, i == activeIndex);
                styledIndex = activeIndex;
            }

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) OnTap();
        }

        /// <summary>Test/debug hook: fire the door exactly as a tap would.</summary>
        public void DebugTap() => OnTap();

        void OnTap()
        {
            if (!IsArmed) return;
            var ball = DoorTarget.Find(board);

            // Freeze the sweep the instant the player commits — the lit position's column is where
            // the ball will enter Zone B. Capture it before SetLocked() hides the markers.
            double spawnX = positionsDesignX[activeIndex];

            // Announce the tap BEFORE the early-out, so a tap that grabbed nothing is measured too:
            // BallDropped only ever reports the taps that worked, which is exactly the half that
            // cannot say whether the door's timing reads.
            GameEvents.RaiseDoorTapped(new DoorTapEvent(ball != null, activeIndex, spawnX));
            if (ball == null) return; // nothing to suck yet

            // Signal busy up front so Zone A's stalemate check can't read a stalemate mid-transit.
            SetLocked(true);
            GameEvents.RaiseZoneBBusy();

            int tier = ball.Tier;
            Vector2 start = ball.Position;
            float diameter = ball.Radius * 2f;
            board.Extract(ball);

            Sfx.Instance?.Transition();
            Haptics.Pulse(feel.dropHapticMs, feel.dropHapticAmp);
            PlaySuck(start, diameter, tier, spawnX);
        }

        /// <summary>
        /// Cosmetic suck → spawn pop → handoff. A throwaway sprite slides from the ball's last Zone A
        /// position to the frozen column at the door mouth (suck), then pops up at the top of Zone B,
        /// and only when the pop lands do we emit <c>BallDropped</c> so Zone B's real ball appears
        /// exactly there — deferring the emit avoids any double-ball flicker.
        /// </summary>
        void PlaySuck(Vector2 start, float diameter, int tier, double spawnX)
        {
            var go = new GameObject("Suck");
            go.transform.SetParent(root, false);
            go.transform.position = start;
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallArt.SpriteForTier(tier);
            sr.sortingOrder = 800;

            float zbDiameter = geometry.ZoneBBallRadius * 2f;
            var mouth = new Vector2(geometry.DesignXToWorld(spawnX), geometry.DoorMouthY);
            var entry = new Vector2(mouth.x, geometry.ZoneBTopY - geometry.ZoneBBallRadius);
            var t = go.transform;

            Tween.Custom(t, 0f, 1f, feel.suckMs / 1000f, (tr, u) =>
            {
                tr.position = Vector2.Lerp(start, mouth, u);
                float d = Mathf.Lerp(diameter, zbDiameter * 0.5f, u);
                tr.localScale = new Vector3(d, d, 1f);
            }, Ease.InCubic).OnComplete(t, tr =>
            {
                tr.position = entry;
                Tween.Scale(tr, zbDiameter, feel.popMs / 1000f, Ease.OutBack).OnComplete(tr, tr2 =>
                {
                    GameEvents.RaiseBallDropped(new BallDroppedEvent(new BallSpec(tier), spawnX));
                    Object.Destroy(tr2.gameObject);
                }, warnIfTargetDestroyed: false);
            }, warnIfTargetDestroyed: false);
        }

        void SetLocked(bool on)
        {
            bool wasLocked = locked;
            locked = on;
            // On re-arm restart the step sequence from the left edge.
            if (wasLocked && !on) sweepMs = 0f;
        }
    }
}
