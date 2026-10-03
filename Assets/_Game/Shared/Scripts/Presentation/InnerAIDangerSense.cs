using System.Collections.Generic;
using UnityEngine;

/// <summary>Attack preparation supplies source and timing. This T2+ display never predicts or controls attacks.</summary>
public sealed class InnerAIDangerSense : MonoBehaviour
{
    [SerializeField, Min(.05f)] private float warningLeadSeconds = .6f;
    [SerializeField, Min(.05f)] private float arcDuration = .55f;
    [SerializeField, Range(10, 100)] private float arcDegrees = 55;
    [SerializeField, Min(.3f)] private float arcRadius = 1.25f;
    private sealed class Warning
    {
        public Transform source;
        public Vector2? fixedOrigin;
        public float delay, elapsed;
        public bool active, shown;
        public LineRenderer line;
    }
    private readonly List<Warning> warnings = new List<Warning>();
    private Material material;
    private PlayerHealth player;
    private PlayerInputReader input;
    private PlayerTimeSlow hostileClock;
    public int PulseCount { get; private set; }
    public bool Visible { get; private set; }
    public Vector2 LastDirection { get; private set; }
    public static float LeadTimeFor(PlayerHealth target) => target != null && target.TryGetComponent<InnerAIDangerSense>(out var effect)
        ? effect.warningLeadSeconds : .6f;

    public static void Signal(PlayerHealth target, Transform source, float secondsUntilAttack, Vector2? fixedOrigin = null)
    {
        if (target == null || target.IsDead || source == null || (target.GetComponent<OfficerIdentity>()?.Deployment?.BrainPermissionTier ?? 0) < 2) return;
        var effect = target.GetComponent<InnerAIDangerSense>();
        if (effect == null) effect = target.gameObject.AddComponent<InnerAIDangerSense>();
        effect.Prepare(source, secondsUntilAttack, fixedOrigin);
    }

    public static void Cancel(PlayerHealth target, Transform source)
    {
        if (target == null || !target.TryGetComponent<InnerAIDangerSense>(out var effect)) return;
        foreach (var warning in effect.warnings)
            if (warning.source == source) { warning.active = false; warning.line.enabled = false; }
    }

    private void Awake()
    {
        player = GetComponent<PlayerHealth>(); input = GetComponent<PlayerInputReader>();
        hostileClock = GetComponent<PlayerTimeSlow>();
        material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Prepare(Transform source, float lead, Vector2? origin)
    {
        var warning = warnings.Find(w => !w.active);
        if (warning == null)
        {
            var go = new GameObject("DangerArc"); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 17;
            line.widthMultiplier = .07f; line.sortingOrder = 30; line.enabled = false;
            warning = new Warning { line = line }; warnings.Add(warning);
        }
        warning.source = source; warning.fixedOrigin = origin;
        warning.delay = Mathf.Max(0, lead - warningLeadSeconds);
        warning.elapsed = 0; warning.shown = false; warning.active = true;
    }

    private void LateUpdate()
    {
        bool live = player != null && !player.IsDead && input != null && input.GameplayInputEnabled;
        float dt = input != null && input.NarrativePaused ? 0 : Time.deltaTime * (hostileClock != null ? hostileClock.WorldRate : 1);
        Visible = false;
        foreach (var warning in warnings)
        {
            if (!live || warning.source == null || !warning.source.gameObject.activeInHierarchy) warning.active = false;
            warning.line.enabled = false;
            if (!warning.active) continue;
            if (warning.delay > 0) { warning.delay -= dt; continue; }
            if (!warning.shown) { warning.shown = true; PulseCount++; }
            warning.elapsed += dt;
            if (warning.elapsed >= arcDuration) { warning.active = false; continue; }
            Vector2 centre = (Vector2)transform.position + Vector2.up * .25f;
            Vector2 direction = ((warning.fixedOrigin ?? (Vector2)warning.source.position) - centre).normalized;
            LastDirection = direction; Visible = warning.line.enabled = true;
            float angle = Mathf.Atan2(direction.y, direction.x);
            float radius = arcRadius + .06f * Mathf.Sin(warning.elapsed * 24);
            for (int i = 0; i < 17; i++)
            {
                float a = angle + (i / 16f - .5f) * arcDegrees * Mathf.Deg2Rad;
                warning.line.SetPosition(i, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            warning.line.startColor = warning.line.endColor = new Color(1, .04f, .04f,
                Mathf.Min(1, (arcDuration - warning.elapsed) * 8));
        }
    }
    private void OnDisable()
    {
        Visible = false;
        foreach (var warning in warnings) { warning.active = false; if (warning.line != null) warning.line.enabled = false; }
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
