using PrimeTween;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// The ball's juice, on a child of the physics body so its scale animations never touch the
    /// collider. Owns landing squash & stretch (along the contact normal), the merge-birth pop,
    /// the blast punch, and a subtle idle breathing wobble — all driven by <see cref="GameFeelSO"/>.
    /// <para>
    /// This transform is the squash pivot: it turns to put its local y on the contact normal and
    /// scales non-uniformly there. The sprite lives on a "Face" child that is counter-rotated by
    /// the same angle, so the face's world rotation is always exactly the body's — the visible
    /// roll comes from physics alone, and the squash reaches the face as a shear rather than a
    /// snap to a new orientation.
    /// </para>
    /// </summary>
    public sealed class BallView : MonoBehaviour
    {
        SpriteRenderer sprite;
        Transform face;
        GameFeelSO feel;
        float diameter = 1f;
        Vector2 squash = Vector2.one;   // multiplicative squash/stretch, tweened back to 1
        float punch = 1f;               // uniform pop multiplier, tweened back to 1
        float wobblePhase;
        Tween squashTween, punchTween;
        Vector2 squashAxis = Vector2.up;

        public SpriteRenderer Sprite => sprite;

        public void Init(GameFeelSO feelSo)
        {
            feel = feelSo;
            FindFace();
            wobblePhase = Random.value * Mathf.PI * 2f;
        }

        void FindFace()
        {
            if (sprite != null) return;
            sprite = GetComponentInChildren<SpriteRenderer>();
            face = sprite != null ? sprite.transform : null;
        }

        public void SetTier(int tier, float radius)
        {
            FindFace();
            sprite.sprite = BallArt.SpriteForTier(tier);
            diameter = radius * 2f;
            squash = Vector2.one;
            punch = 1f;
            squashTween.Stop();
            punchTween.Stop();
            Apply();
        }

        /// <summary>Landing/collision squash: compress along the contact normal, stretch across it, spring back.</summary>
        public void OnImpact(float impactSpeed, Vector2 normal)
        {
            if (feel == null || impactSpeed < feel.landSquashMinSpeed) return;
            float k = Mathf.Clamp01((impactSpeed - feel.landSquashMinSpeed) / Mathf.Max(0.01f, feel.landSquashFullSpeed - feel.landSquashMinSpeed));
            float amount = feel.landSquash * k;
            squashAxis = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector2.up;
            squashTween.Stop();
            squash = new Vector2(1f + amount, 1f / (1f + amount)); // x = across the normal, y = along it
            squashTween = Tween.Custom(this, 0f, 1f, feel.landSquashMs / 1000f, (self, t) =>
            {
                float a = self.feel.landSquash * k * (1f - t);
                self.squash = new Vector2(1f + a, 1f / (1f + a));
                self.Apply();
            }, Ease.OutBack);
        }

        /// <summary>Merge birth: pop up from a small scale with a back-ease overshoot.</summary>
        public void PlayBirthPop()
        {
            if (feel == null) return;
            punchTween.Stop();
            punch = feel.birthScaleFrom;
            punchTween = Tween.Custom(this, feel.birthScaleFrom, 1f, feel.birthPopMs / 1000f, (self, v) =>
            {
                self.punch = v;
                self.Apply();
            }, Ease.OutBack);
        }

        /// <summary>Neighbour punch when a merge blast reaches this ball (strength 0..1).</summary>
        public void PlayBlastPunch(float strength)
        {
            if (feel == null) return;
            punchTween.Stop();
            float peak = 1f + feel.blastPunch * strength;
            punchTween = Tween.Custom(this, peak, 1f, 0.18f, (self, v) =>
            {
                self.punch = v;
                self.Apply();
            }, Ease.OutQuad);
        }

        void Update()
        {
            if (feel == null || feel.idleWobble <= 0f) return;
            wobblePhase += Time.deltaTime * feel.idleWobbleHz * Mathf.PI * 2f;
            Apply();
        }

        void Apply()
        {
            float wobble = feel != null ? 1f + Mathf.Sin(wobblePhase) * feel.idleWobble : 1f;
            float wobbleY = feel != null ? 1f - Mathf.Sin(wobblePhase) * feel.idleWobble : 1f;
            // Squash is expressed in the contact frame: turn this pivot so its local y aligns with
            // the (world-space) normal, then scale y by the "along" factor and x by the "across"
            // factor. The parent body rolls, so express the normal in the parent's frame — and
            // counter-rotate the face by the same angle so the sprite itself never turns except
            // with the body.
            float angle = Mathf.Atan2(squashAxis.y, squashAxis.x) * Mathf.Rad2Deg - 90f;
            if (transform.parent != null) angle -= transform.parent.eulerAngles.z;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (face != null) face.localRotation = Quaternion.Euler(0f, 0f, -angle);
            transform.localScale = new Vector3(diameter * squash.x * punch * wobble, diameter * squash.y * punch * wobbleY, 1f);
        }
    }
}
