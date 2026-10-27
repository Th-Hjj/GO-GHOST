using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>Player-local cooldown feedback; PlayerDodge remains the only gameplay clock.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class DodgeCooldownView : MonoBehaviour
    {
        const float ReadyHoldSeconds = .06f;
        const float ReadyShrinkSeconds = .18f;
        const float MinimumDiameterPixels = 8;
        const float HeadGapPixels = 4;
        DuelGame game;
        PlayerDodge dodge;
        Camera viewCamera;
        SpriteRenderer head, dot;
        float cooldownEndsAt = float.NegativeInfinity;

        public void Initialize(DuelGame owner, Material material, int sortingLayer)
        {
            game = owner;
            dodge = owner.Dodge;
            viewCamera = Camera.main;
            head = owner.Player.GetComponent<OfficerVisual>()?.Part(LoadoutSlot.Brain);
            if (head == null || head.sprite == null) return;
            dot = new GameObject("DodgeCooldownDot").AddComponent<SpriteRenderer>();
            dot.transform.SetParent(transform, false);
            dot.sprite = head.sprite;
            dot.sharedMaterial = material;
            dot.sortingLayerID = sortingLayer;
            dot.sortingOrder = 30;
            dodge.DodgeStarted += OnDodgeStarted;
            SetVisible(false);
        }

        void OnDodgeStarted()
        {
            // Keep the accepted deadline: tuning edits must not move the ready accent.
            cooldownEndsAt = Time.time + dodge.CooldownRemaining;
        }

        void LateUpdate()
        {
            bool visible = game != null && dodge != null && dodge.isActiveAndEnabled && head != null && dot != null
                && game.Player != null && !game.Player.IsDead
                && (game.Phase == DuelPhase.Fight || game.Phase == DuelPhase.Training);
            SetVisible(visible);
            if (!visible) return;

            float remaining = dodge.CooldownRemaining;
            float sinceReady = Time.time - cooldownEndsAt;
            bool readyAccent = remaining <= 0 && sinceReady >= 0
                && sinceReady < ReadyHoldSeconds + ReadyShrinkSeconds;
            SetVisible(remaining > 0 || readyAccent);
            if (!dot.enabled) return;

            float shrink = readyAccent
                ? Mathf.SmoothStep(0, 1, (sinceReady - ReadyHoldSeconds) / ReadyShrinkSeconds) : 0;
            float worldPerPixel = viewCamera != null && viewCamera.orthographic
                ? 2 * viewCamera.orthographicSize / Mathf.Max(1, viewCamera.pixelHeight) : 0;
            var headBounds = head.bounds;
            float diameter = Mathf.Max(headBounds.size.x * .4f, MinimumDiameterPixels * worldPerPixel);
            float gap = Mathf.Max(headBounds.size.y * .22f, HeadGapPixels * worldPerPixel);
            // Use the posed head, not facing or aim: crouch, jump and recoil keep clear separation.
            dot.transform.position = new Vector3(headBounds.center.x, headBounds.max.y + gap + diameter * .5f, headBounds.center.z);
            dot.transform.localScale = Vector3.one * (diameter * (1 - shrink) / dot.sprite.bounds.size.x);
            Color color = readyAccent ? Color.Lerp(NeuralPalette.Spine, NeuralPalette.Primary, .8f) : NeuralPalette.Spine;
            color.a = readyAccent ? 1 - shrink : .95f;
            dot.color = color;
        }

        void SetVisible(bool visible)
        {
            if (dot != null) dot.enabled = visible;
        }

        void OnDisable() => SetVisible(false);
        void OnDestroy()
        {
            if (dodge != null) dodge.DodgeStarted -= OnDodgeStarted;
        }
    }
}
