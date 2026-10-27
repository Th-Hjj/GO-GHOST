using UnityEngine;
using UnityEngine.Rendering;

namespace GoGhost.Duel
{
    /// <summary>Player-local cooldown feedback; PlayerDodge remains the only gameplay clock.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class DodgeCooldownView : MonoBehaviour
    {
        const int Segments = 32;
        const float ReadyPulseSeconds = .2f;
        readonly Vector3[] arc = new Vector3[Segments + 1];
        DuelGame game;
        PlayerDodge dodge;
        Camera viewCamera;
        LineRenderer track, fill;
        float acceptedDuration, cooldownEndsAt = float.NegativeInfinity;

        public void Initialize(DuelGame owner, Material material, int sortingLayer)
        {
            game = owner;
            dodge = owner.Dodge;
            viewCamera = Camera.main;
            for (int i = 0; i <= Segments; i++) arc[i] = Point(i / (float)Segments);
            track = CreateLine("DodgeCooldownTrack", material, sortingLayer, 29);
            fill = CreateLine("DodgeCooldownFill", material, sortingLayer, 30);
            track.positionCount = arc.Length;
            track.SetPositions(arc);
            track.startColor = track.endColor = new Color(.61f, .58f, .68f, .25f);
            dodge.DodgeStarted += OnDodgeStarted;
            SetVisible(false);
        }

        static Vector3 Point(float progress)
        {
            // Bottom to top on the screen-left side, independent of body and aim facing.
            float angle = Mathf.Lerp(240, 120, progress) * Mathf.Deg2Rad;
            return new Vector3(-.32f + Mathf.Cos(angle) * .34f, -.06f + Mathf.Sin(angle) * .34f, 0);
        }

        LineRenderer CreateLine(string label, Material material, int layer, int order)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.sortingLayerID = layer;
            line.sortingOrder = order;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        void OnDodgeStarted()
        {
            // Capture the accepted interval, so F8 edits cannot retime an existing display.
            acceptedDuration = dodge.CooldownRemaining;
            cooldownEndsAt = Time.time + acceptedDuration;
        }

        void LateUpdate()
        {
            bool visible = game != null && dodge != null && dodge.isActiveAndEnabled
                && game.Player != null && !game.Player.IsDead
                && (game.Phase == DuelPhase.Fight || game.Phase == DuelPhase.Training);
            SetVisible(visible);
            if (!visible) return;

            float remaining = dodge.CooldownRemaining;
            float progress = remaining > 0 ? 1 - Mathf.Clamp01(remaining / Mathf.Max(.0001f, acceptedDuration)) : 1;
            float sinceReady = Time.time - cooldownEndsAt;
            float pulse = remaining <= 0 && acceptedDuration > 0 && sinceReady >= 0
                ? Mathf.Clamp01(1 - sinceReady / ReadyPulseSeconds) : 0;
            // Keep a readable thin stroke at the normal arena camera scale.
            float width = viewCamera != null && viewCamera.orthographic
                ? Mathf.Max(.032f, 2f * viewCamera.orthographicSize * 2 / Mathf.Max(1, viewCamera.pixelHeight)) : .032f;
            track.startWidth = track.endWidth = width;
            fill.startWidth = fill.endWidth = width * (1 + .25f * pulse);
            Color color = Color.Lerp(NeuralPalette.Spine, NeuralPalette.Primary, pulse * .8f);
            color.a = remaining > 0 ? .95f : Mathf.Lerp(.5f, 1, pulse);
            fill.startColor = fill.endColor = color;
            fill.enabled = progress > 0;
            if (!fill.enabled) return;
            int last = Mathf.Clamp(Mathf.CeilToInt(progress * Segments), 1, Segments);
            fill.positionCount = last + 1;
            for (int i = 0; i < last; i++) fill.SetPosition(i, arc[i]);
            fill.SetPosition(last, Point(progress));
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
