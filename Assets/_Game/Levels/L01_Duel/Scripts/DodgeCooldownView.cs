using UnityEngine;
using UnityEngine.Rendering;

namespace GoGhost.Duel
{
    /// <summary>Player-local cooldown feedback; PlayerDodge remains the only gameplay clock.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class DodgeCooldownView : MonoBehaviour
    {
        const float ReadyHoldSeconds = .08f;
        const float ReadyFadeSeconds = .22f;
        DuelGame game;
        PlayerDodge dodge;
        Camera viewCamera;
        SpriteRenderer head;
        Collider2D body;
        LineRenderer track, fill;
        float acceptedDuration, cooldownEndsAt = float.NegativeInfinity;

        public void Initialize(DuelGame owner, Material material, int sortingLayer)
        {
            game = owner;
            dodge = owner.Dodge;
            viewCamera = Camera.main;
            head = owner.Player.GetComponent<OfficerVisual>()?.Part(LoadoutSlot.Brain);
            body = owner.Player.GetComponent<Collider2D>();
            track = CreateLine("DodgeCooldownTrack", material, sortingLayer, 29);
            fill = CreateLine("DodgeCooldownFill", material, sortingLayer, 30);
            dodge.DodgeStarted += OnDodgeStarted;
            SetVisible(false);
        }

        LineRenderer CreateLine(string label, Material material, int sortingLayer, int order)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sortingLayerID = sortingLayer;
            line.sortingOrder = order;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void OnDodgeStarted()
        {
            // Keep the accepted deadline: tuning edits must not move the ready accent.
            acceptedDuration = dodge.CooldownRemaining;
            cooldownEndsAt = Time.time + acceptedDuration;
        }

        void LateUpdate()
        {
            bool visible = game != null && dodge != null && dodge.isActiveAndEnabled && body != null
                && game.Player != null && !game.Player.IsDead
                && (game.Phase == DuelPhase.Fight || game.Phase == DuelPhase.Training);
            if (!visible) { SetVisible(false); return; }

            float remaining = dodge.CooldownRemaining;
            float sinceReady = Time.time - cooldownEndsAt;
            bool readyAccent = remaining <= 0 && sinceReady >= 0
                && sinceReady < ReadyHoldSeconds + ReadyFadeSeconds;
            SetVisible(remaining > 0 || readyAccent);
            if (!track.enabled) return;

            float opacity = readyAccent
                ? 1 - Mathf.SmoothStep(0, 1, (sinceReady - ReadyHoldSeconds) / ReadyFadeSeconds) : 1;
            float progress = remaining > 0
                ? 1 - Mathf.Clamp01(remaining / Mathf.Max(.0001f, acceptedDuration)) : 1;
            float worldPerPixel = viewCamera != null && viewCamera.orthographic
                ? 2 * viewCamera.orthographicSize / Mathf.Max(1, viewCamera.pixelHeight) : 0;
            float width = Mathf.Max(head != null ? head.bounds.size.x : .46f, 20 * worldPerPixel);
            float thickness = Mathf.Max(.025f, 2 * worldPerPixel);
            float gap = Mathf.Max(.05f, 3 * worldPerPixel);
            var bounds = body.bounds;
            // A stable foot-plane anchor follows jumps without bobbing with individual footsteps.
            var left = new Vector3(bounds.center.x - width * .5f, bounds.min.y - gap - thickness * .5f, transform.position.z);
            var right = left + Vector3.right * width;
            track.startWidth = track.endWidth = fill.startWidth = fill.endWidth = thickness;
            track.SetPosition(0, left); track.SetPosition(1, right);
            fill.SetPosition(0, left); fill.SetPosition(1, Vector3.Lerp(left, right, progress));
            track.startColor = track.endColor = new Color(.20f, .15f, .27f, .65f * opacity);
            Color color = readyAccent ? Color.Lerp(NeuralPalette.Spine, NeuralPalette.Primary, .2f) : NeuralPalette.Spine;
            color.a = .60f * opacity;
            fill.startColor = fill.endColor = color;
            fill.enabled = progress > 0;
        }

        void SetVisible(bool visible)
        {
            if (track != null) track.enabled = visible;
            if (fill != null) fill.enabled = visible;
        }

        void OnDisable() => SetVisible(false);
        void OnDestroy()
        {
            if (dodge != null) dodge.DodgeStarted -= OnDodgeStarted;
        }
    }
}
