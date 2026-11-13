using UnityEngine;
using UnityEngine.Rendering;

namespace GoGhost.Duel
{
    /// <summary>Player-local cooldown feedback; PlayerDodge remains the only gameplay clock.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class DodgeCooldownView : MonoBehaviour
    {
        DuelGame game;
        PlayerDodge dodge;
        Camera viewCamera;
        SpriteRenderer head;
        Collider2D body;
        LineRenderer track, fill;
        float acceptedDuration;
        float readyAt = float.NegativeInfinity;
        const float ReadyFlashSeconds = .16f;

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
            // Preserve the accepted interval when live tuning changes the next Dodge.
            acceptedDuration = dodge.CooldownRemaining;
            readyAt = Time.time + acceptedDuration;
        }

        void LateUpdate()
        {
            bool visible = game != null && dodge != null && dodge.isActiveAndEnabled && body != null
                && game.Player != null && !game.Player.IsDead
                && (game.Phase == DuelPhase.Fight || game.Phase == DuelPhase.Training);
            if (!visible) { readyAt = float.NegativeInfinity; SetVisible(false); return; }

            float remaining = dodge.CooldownRemaining;
            // Only a real Dodge arms this flash. An early cooldown reset disarms it.
            if (remaining <= 0 && Time.time < readyAt - .0001f) readyAt = float.NegativeInfinity;
            float flash = remaining > 0 ? 0 : Mathf.Clamp01(1 - (Time.time - readyAt) / ReadyFlashSeconds);
            SetVisible(remaining > 0 || flash > 0);
            if (!track.enabled) return;

            float remainingFraction = flash > 0 ? 1 : Mathf.Clamp01(remaining / Mathf.Max(.0001f, acceptedDuration));
            float worldPerPixel = viewCamera != null && viewCamera.orthographic
                ? 2 * viewCamera.orthographicSize / Mathf.Max(1, viewCamera.pixelHeight) : 0;
            float width = Mathf.Max((head != null ? head.bounds.size.x : .46f) * 1.25f, 25 * worldPerPixel);
            float thickness = Mathf.Max(.06f, 5 * worldPerPixel);
            float gap = Mathf.Max(.09f, 5 * worldPerPixel);
            var bounds = body.bounds;
            // A stable foot-plane anchor follows jumps without bobbing with individual footsteps.
            var left = new Vector3(bounds.center.x - width * .5f, bounds.min.y - gap - thickness * .5f, transform.position.z);
            var right = left + Vector3.right * width;
            track.startWidth = track.endWidth = fill.startWidth = fill.endWidth = thickness;
            track.SetPosition(0, left); track.SetPosition(1, right);
            fill.SetPosition(0, left); fill.SetPosition(1, Vector3.Lerp(left, right, remainingFraction));
            track.startColor = track.endColor = new Color(.18f, .18f, .18f, .65f);
            Color color = NeuralPalette.Primary;
            if (flash > 0) color = Color.white;
            color.a = flash > 0 ? flash : .80f;
            fill.startColor = fill.endColor = color;
        }

        void SetVisible(bool visible)
        {
            if (track != null) track.enabled = visible;
            if (fill != null) fill.enabled = visible;
        }

        void OnDisable() { readyAt = float.NegativeInfinity; SetVisible(false); }
        void OnDestroy()
        {
            if (dodge != null) dodge.DodgeStarted -= OnDodgeStarted;
        }
    }
}
