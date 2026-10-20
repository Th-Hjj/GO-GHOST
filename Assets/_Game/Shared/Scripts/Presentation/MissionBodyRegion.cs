using System.Collections.Generic;
using UnityEngine;

/// <summary>Six existing clickable regions, redrawn as a minimal geometric expanded pose.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionBodyRegion : UnityEngine.UI.MaskableGraphic, ICanvasRaycastFilter,
    UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
{
    public bool Round;
    public bool Selected;
    public bool Equipped;
    public bool Monochrome;
    public Vector2[] Contour;
    public bool NeuralStyle;
    public int NeuralRegion;
    public float NeuralSignal, NeuralFlow = -1;
    [Header("Contour pulse (local path units)")]
    [Min(0)] public float PulseSpeed = 100;
    [Min(8)] public float PulseLength = 90;
    [Range(0, 2)] public float PulseIntensity = 1;
    [Tooltip("Equally spaced pulses per closed contour. One keeps the six-region figure quiet.")]
    [Range(1, 3)] public int PulseFrequency = 1;
    readonly List<Vector2> pathPoints = new List<Vector2>();
    readonly List<Vector2> pathNormals = new List<Vector2>();
    readonly List<float> pathDistances = new List<float>();
    Vector2[] cachedContour;
    Rect cachedRect;
    bool cachedRound, hovered, focused;
    float pathLength, activity, pulseVisibility;
    float NeuralStrokeScale => Mathf.Max(1, canvas != null ? canvas.scaleFactor : 1);

    void Update()
    {
        if (!NeuralStyle || NeuralRegion == 0) return;
        // UI runs while configuration freezes combat time. Absolute phase survives page rebuilds.
        float target = Mathf.Max(NeuralSignal, Selected || hovered || focused ? 1 : Equipped ? .38f : 0);
        if (NeuralFlow >= 0 && NeuralFlow < 1)
            target = Mathf.Max(target, Mathf.Sin(NeuralFlow * Mathf.PI));
        float previousVisibility = pulseVisibility;
        activity = Mathf.Lerp(activity, Mathf.Clamp01(target), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
        pulseVisibility = Mathf.Lerp(pulseVisibility, target > 0 ? 1 : 0, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
        if (pulseVisibility < .001f) pulseVisibility = 0;
        if (pulseVisibility > 0 || previousVisibility > 0) SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        hovered = focused = false;
        activity = pulseVisibility = 0;
        base.OnDisable();
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data) => hovered = true;
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data) => hovered = false;
    public void OnSelect(UnityEngine.EventSystems.BaseEventData data) => focused = true;
    public void OnDeselect(UnityEngine.EventSystems.BaseEventData data) => focused = false;

    private int PointCount => Round ? 32 : Contour != null ? Contour.Length : 4;
    private Vector2 Point(int index)
    {
        var r = rectTransform.rect;
        Vector2 p;
        if (Round)
        {
            float angle = index * Mathf.PI * 2 / PointCount;
            p = new Vector2(Mathf.Cos(angle) * .5f, Mathf.Sin(angle) * .5f);
        }
        else if (Contour != null) p = Contour[index];
        else p = new Vector2(index == 0 || index == 3 ? -.5f : .5f, index < 2 ? -.5f : .5f);
        return new Vector2(p.x * r.width, p.y * r.height) + r.center;
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        if (NeuralStyle) { DrawContourPulse(mesh); return; }
        int count = PointCount;
        var center = rectTransform.rect.center;
        Color outline = Selected ? new Color(.52f, .85f, 1f)
            : Equipped ? new Color(.30f, .68f, 1f) : new Color(.86f, .91f, .94f);
        Color fill = Selected ? new Color(.045f, .19f, .29f)
            : Equipped ? new Color(.025f, .065f, .095f) : new Color(.012f, .018f, .024f);
        if (Monochrome)
        {
            outline = Selected ? Color.white : new Color(.55f, .55f, .55f);
            fill = Selected ? new Color(.12f, .12f, .12f) : Color.black;
        }
        mesh.AddVert(center, fill, Vector2.zero);
        for (int i = 0; i < count; i++) mesh.AddVert(Point(i), fill, Vector2.zero);
        for (int i = 0; i < count; i++) mesh.AddTriangle(0, i + 1, (i + 1) % count + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 a = Point(i), b = Point((i + 1) % count);
            if (Selected || Equipped)
            {
                Color glow = outline; glow.a = Selected ? .10f : .05f;
                Segment(mesh, a, b, 9, glow);
            }
            float width = Monochrome ? 1.5f * (NeuralStyle ? NeuralStrokeScale : 1) / Mathf.Max(.05f, rectTransform.lossyScale.x) : Selected ? 3 : 2;
            Segment(mesh, a, b, width, outline);
            if (Equipped) Segment(mesh, Vector2.Lerp(center, a, .88f), Vector2.Lerp(center, b, .88f), 1,
                new Color(outline.r, outline.g, outline.b, .40f));
        }
    }

    void CacheContourPath()
    {
        var rect = rectTransform.rect;
        if (pathPoints.Count > 0 && cachedRect == rect && cachedContour == Contour && cachedRound == Round) return;
        cachedRect = rect; cachedContour = Contour; cachedRound = Round;
        pathPoints.Clear(); pathNormals.Clear(); pathDistances.Clear(); pathLength = 0;
        // Subdivide the original edges, never cut across a corner or bridge the anatomical gaps.
        for (int edge = 0; edge < PointCount; edge++)
        {
            Vector2 a = Point(edge), b = Point((edge + 1) % PointCount);
            float length = Vector2.Distance(a, b);
            if (length < .001f) continue;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2.5f));
            for (int step = 0; step < steps; step++)
            {
                float t = (float)step / steps;
                pathPoints.Add(Vector2.Lerp(a, b, t));
                pathDistances.Add(pathLength + t * length);
            }
            pathLength += length;
        }
        for (int i = 0; i < pathPoints.Count; i++)
        {
            Vector2 incoming = (pathPoints[i] - pathPoints[(i + pathPoints.Count - 1) % pathPoints.Count]).normalized;
            Vector2 outgoing = (pathPoints[(i + 1) % pathPoints.Count] - pathPoints[i]).normalized;
            Vector2 normal = new Vector2(-outgoing.y, outgoing.x);
            Vector2 join = (normal + new Vector2(-incoming.y, incoming.x)).normalized;
            // Shared, bounded miter vertices avoid cracks and overlapping bright corner quads.
            pathNormals.Add(join / Mathf.Max(.4f, Vector2.Dot(join, normal)));
        }
    }

    void DrawContourPulse(UnityEngine.UI.VertexHelper mesh)
    {
        CacheContourPath();
        if (pathLength < .001f) return;
        float pixel = NeuralStrokeScale / Mathf.Max(.05f, rectTransform.lossyScale.x);
        Color outline = NeuralPalette.Alpha(NeuralPalette.Secondary, .62f);
        Color identity = NeuralPalette.Region(NeuralRegion);
        float spacing = pathLength / Mathf.Clamp(PulseFrequency, 1, 3);
        float length = Mathf.Clamp(PulseLength, 8, spacing * .7f);
        // Double precision avoids long-session phase jitter. Both ends of the trail reach zero.
        // Paired contours already mirror one another. Shared phase preserves mirrored motion.
        int phaseRegion = NeuralRegion == 3 ? 2 : NeuralRegion == 5 ? 4 : NeuralRegion;
        float head = (float)((Time.unscaledTimeAsDouble * Mathf.Max(0, PulseSpeed) + phaseRegion * 97.0) % spacing);
        float intensity = Mathf.Clamp01(PulseIntensity * Mathf.Lerp(.62f, 1, activity)) * pulseVisibility;
        DrawContourBand(mesh, pixel, outline, identity, spacing, length, head, intensity, false);
        // Head is reserved for its own future effect, including in the legacy Upgrade screen.
        if (NeuralRegion != 0 && pulseVisibility > .001f)
            DrawContourBand(mesh, pixel, outline, identity, spacing, length, head, intensity, true);
    }

    // Feathered strips use the default UI material: Overlay-compatible, no texture or Bloom needed.
    static readonly float[] OutlineOffsets = { -1.25f, -.55f, .55f, 1.25f };
    static readonly float[] PulseOffsets = { -4.5f, -2.2f, -1.05f, -.35f, .35f, 1.05f, 2.2f, 4.5f };
    void DrawContourBand(UnityEngine.UI.VertexHelper mesh, float pixel, Color outline, Color identity,
        float spacing, float length, float head, float intensity, bool pulse)
    {
        float[] offsets = pulse ? PulseOffsets : OutlineOffsets;
        int start = mesh.currentVertCount, columns = offsets.Length, count = pathPoints.Count;
        for (int i = 0; i < count; i++)
        {
            float behind = Mathf.Repeat(head - pathDistances[i], spacing) / length;
            float strength = behind < 1 ? Mathf.SmoothStep(0, 1, behind / .12f) * (1 - behind) * (1 - behind) * intensity : 0;
            for (int j = 0; j < columns; j++)
            {
                bool edge = j == 0 || j == columns - 1;
                Color c = outline;
                if (pulse)
                {
                    c = j == 3 || j == 4 ? Color.white : identity;
                    c.a = edge ? 0 : (j == 1 || j == 6 ? .18f : 1) * strength;
                }
                else if (edge) c.a = 0;
                mesh.AddVert(pathPoints[i] + pathNormals[i] * (offsets[j] * pixel), c, Vector2.zero);
            }
        }
        for (int i = 0; i < count; i++)
            for (int j = 0; j < columns - 1; j++)
            {
                int a = start + i * columns + j, b = start + ((i + 1) % count) * columns + j;
                mesh.AddTriangle(a, a + 1, b + 1); mesh.AddTriangle(a, b + 1, b);
            }
    }

    private static void Segment(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width, Color c)
    {
        var delta = b - a;
        if (delta.sqrMagnitude < .0001f) return;
        var normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(a - normal, c, Vector2.zero); mesh.AddVert(a + normal, c, Vector2.zero);
        mesh.AddVert(b + normal, c, Vector2.zero); mesh.AddVert(b - normal, c, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // Do not let an expanded limb's empty bounding-box corners steal adjacent region clicks.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var p)) return false;
        bool inside = false;
        int count = PointCount;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = Point(i), b = Point(j), segment = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, segment) / Mathf.Max(.0001f, segment.sqrMagnitude));
            if ((p - a - segment * t).sqrMagnitude <= 64) return true;
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
