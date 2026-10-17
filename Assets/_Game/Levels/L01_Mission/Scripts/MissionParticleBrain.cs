using UnityEngine;

/// <summary>A small, bounded 3D cloud projected into the configuration head's Overlay UI.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionParticleBrain : UnityEngine.UI.MaskableGraphic
{
    [Range(18, 30)] public int ParticleCount = 18;
    [Tooltip("Degrees per second around the cloud's vertical axis.")]
    [Range(0, 30)] public float RotationSpeed = 16;
    [Tooltip("Cloud width/height as fractions of the head. The outer feather stays inside its outline.")]
    public Vector2 ClusterSize = new Vector2(.90f, .66f);
    [Range(0, .4f)] public float GlowIntensity = .12f;

    const int Segments = 12;
    Vector3[] seeds, positions;
    int[] depthOrder;
    static readonly Vector2[] Circle = MakeCircle();

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
    }

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();
        color = new Color32(232, 245, 255, 255);
        raycastTarget = false;
    }
#endif

    void Update() => SetVerticesDirty();

    static Vector2[] MakeCircle()
    {
        var points = new Vector2[Segments];
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2 / Segments;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return points;
    }

    void EnsureParticles()
    {
        int count = Mathf.Clamp(ParticleCount, 18, 30);
        if (seeds != null && seeds.Length == count) return;
        seeds = new Vector3[count];
        positions = new Vector3[count];
        depthOrder = new int[count];
        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            // Stable, irregular volume sampling; never consume gameplay Random state or respawn dots.
            float y = 1 - 2 * (i + .5f) / count;
            float radial = Mathf.Sqrt(1 - y * y);
            float angle = i * 2.39996323f;
            float radius = .72f + .22f * Mathf.Repeat((i + 1) * .75487766f, 1);
            if (i % 5 == 0) radius *= .65f;
            seeds[i] = new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial) * radius;
            center += seeds[i];
        }
        center /= count;
        for (int i = 0; i < count; i++) seeds[i] -= center;
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        EnsureParticles();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0) return;
        // Absolute double-precision time preserves phase when the rule page is rebuilt.
        double time = Time.unscaledTimeAsDouble;
        float yaw = (float)(time * Mathf.Clamp(RotationSpeed, 0, 30) % 360);
        var rotation = Quaternion.Euler(17 + 6 * Wave(time, .083, 0), yaw, 6 * Wave(time, .061, 1));
        Vector3 center = Vector3.zero;
        for (int i = 0; i < seeds.Length; i++)
        {
            float phase = i * 2.39996323f;
            var drift = new Vector3(Wave(time, .19 + i * .0031, phase),
                Wave(time, .23 + i * .0023, phase * 1.7), Wave(time, .17 + i * .0041, phase * 2.3)) * .025f;
            positions[i] = rotation * Vector3.Scale(seeds[i] + drift, new Vector3(1, .84f, .88f));
            center += positions[i];
        }
        center /= seeds.Length;
        for (int i = 0; i < seeds.Length; i++)
        {
            positions[i] -= center;
            // Small allocation-free insertion sort: distant particles draw behind near particles.
            int j = i;
            while (j > 0 && positions[depthOrder[j - 1]].z > positions[i].z)
            { depthOrder[j] = depthOrder[j - 1]; j--; }
            depthOrder[j] = i;
        }
        Vector2 extent = new Vector2(rect.width * Mathf.Clamp(ClusterSize.x, .1f, .95f),
            rect.height * Mathf.Clamp(ClusterSize.y, .1f, .95f)) * .5f;
        // Minimum size is in physical pixels, but high-resolution renders retain proportional dots.
        float scale = Mathf.Max(.01f, Mathf.Abs(rectTransform.lossyScale.x));
        float unit = Mathf.Max(rect.width / 72f, .62f / scale);
        float feather = Mathf.Max(.45f * unit, .4f / scale);
        Vector2 safe = rect.size * .5f - Vector2.one * (1.7f / scale);
        if (safe.x <= 0 || safe.y <= 0) return;
        for (int n = 0; n < depthOrder.Length; n++)
        {
            int i = depthOrder[n];
            Vector3 p = positions[i];
            float depth = Mathf.Clamp01((p.z + 1) * .5f);
            float perspective = 4.5f / (4.5f - p.z);
            Vector2 point = new Vector2(p.x * extent.x, p.y * extent.y) * perspective;
            float variation = Mathf.Lerp(.9f, 1.1f, Mathf.Repeat(i * .61803399f, 1));
            float radius = Mathf.Lerp(.85f, 1.8f, depth) * variation * unit;
            float outer = radius * 1.4f + feather;
            // Fit even the transparent feather within an inset ellipse, without clipping or respawns.
            Vector2 limit = safe - Vector2.one * outer;
            if (limit.x <= 0 || limit.y <= 0) continue;
            float distance = new Vector2(point.x / limit.x, point.y / limit.y).magnitude;
            if (distance > 1) point /= distance;
            float brightness = .965f + .035f * Wave(time, .43 + i * .0097, i * 1.91);
            Color tint = color;
            tint.a *= Mathf.Lerp(.30f, 1, depth) * brightness;
            DrawParticle(mesh, rect.center + point, radius, outer, tint);
        }
    }

    static float Wave(double time, double speed, double phase) => (float)System.Math.Sin(time * speed + phase);

    void DrawParticle(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, float outer, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(center, tint, Vector2.zero);
        for (int ring = 0; ring < 3; ring++)
        {
            float r = ring == 0 ? radius * .6f : ring == 1 ? radius : outer;
            Color c = tint;
            c.a *= ring == 0 ? .95f : ring == 1 ? Mathf.Clamp(GlowIntensity, 0, .4f) : 0;
            for (int i = 0; i < Segments; i++) mesh.AddVert(center + Circle[i] * r, c, Vector2.zero);
        }
        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            mesh.AddTriangle(start, start + 1 + i, start + 1 + next);
            for (int ring = 0; ring < 2; ring++)
            {
                int a = start + 1 + ring * Segments + i, b = start + 1 + ring * Segments + next;
                mesh.AddTriangle(a, a + Segments, b + Segments);
                mesh.AddTriangle(a, b + Segments, b);
            }
        }
    }
}
