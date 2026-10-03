using UnityEngine;

/// <summary>Brief, world-anchored T2+ warning. Attack code signals danger; this component only draws it.</summary>
public sealed class InnerAIDangerSense : MonoBehaviour
{
    private readonly LineRenderer[] streaks = new LineRenderer[9];
    private Material material;
    private PlayerHealth player;
    private PlayerInputReader input;
    private float remaining, clock, lastPulse = -10;
    public int PulseCount { get; private set; }
    public bool Visible => remaining > 0 && streaks[0] != null && streaks[0].enabled;

    public static void Signal(PlayerHealth target)
    {
        if (target == null || target.IsDead || (target.GetComponent<OfficerIdentity>()?.Deployment?.BrainPermissionTier ?? 0) < 2) return;
        var effect = target.GetComponent<InnerAIDangerSense>();
        if (effect == null) effect = target.gameObject.AddComponent<InnerAIDangerSense>();
        effect.Pulse();
    }

    private void Awake()
    {
        player = GetComponent<PlayerHealth>(); input = GetComponent<PlayerInputReader>();
        material = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < streaks.Length; i++)
        {
            var go = new GameObject("DangerStreak" + i);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 3;
            line.startWidth = .055f; line.endWidth = .018f; line.sortingOrder = 30;
            line.enabled = false; streaks[i] = line;
        }
    }

    private void Pulse()
    {
        if (Time.time - lastPulse < .4f) return;
        lastPulse = Time.time; remaining = .7f; clock = 0; PulseCount++;
    }

    private void LateUpdate()
    {
        bool live = player != null && !player.IsDead && input != null && input.GameplayInputEnabled;
        if (!live) remaining = 0;
        float dt = input != null && input.NarrativePaused ? 0 : Time.deltaTime;
        remaining = Mathf.Max(0, remaining - dt); clock += dt;
        for (int i = 0; i < streaks.Length; i++)
        {
            var line = streaks[i]; line.enabled = remaining > 0;
            if (!line.enabled) continue;
            float angle = (18 + i * 18 + Mathf.Sin(clock * 32 + i) * 5) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 head = (Vector2)transform.position + Vector2.up * .35f;
            float radius = 1 + .12f * Mathf.Sin(clock * 24 + i);
            line.SetPosition(0, head + direction * radius);
            line.SetPosition(1, head + direction * (radius + .23f) + perpendicular * .07f);
            line.SetPosition(2, head + direction * (radius + .5f + .13f * Mathf.Sin(i)));
            Color red = new Color(1, .04f, .04f, Mathf.Min(1, remaining * 4));
            line.startColor = line.endColor = red;
        }
    }
    private void OnDisable() { remaining = 0; foreach (var line in streaks) if (line != null) line.enabled = false; }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
