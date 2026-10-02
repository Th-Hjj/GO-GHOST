using UnityEngine;

/// <summary>One static mesh behind the body: circle, datum square and sparse scan ticks.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionAugmentationGuide : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var r = rectTransform.rect;
        float radius = Mathf.Min(r.width, r.height) * .5f;
        Color guide = new Color(.24f, .61f, 1f, .16f);
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
            Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 1, guide);
        }
        Color square = guide; square.a = .07f;
        Line(mesh, new Vector2(-radius, -radius), new Vector2(radius, -radius), 1, square);
        Line(mesh, new Vector2(-radius, radius), new Vector2(radius, radius), 1, square);
        Line(mesh, new Vector2(-radius, -radius), new Vector2(-radius, radius), 1, square);
        Line(mesh, new Vector2(radius, -radius), new Vector2(radius, radius), 1, square);
        for (int i = -3; i <= 3; i++)
        {
            Line(mesh, new Vector2(-radius, i * 70), new Vector2(-radius + 14, i * 70), 1, guide);
            Line(mesh, new Vector2(radius - 14, i * 70), new Vector2(radius, i * 70), 1, guide);
        }
        Line(mesh, new Vector2(-radius, 0), new Vector2(radius, 0), 1, square);
        Line(mesh, new Vector2(0, -radius), new Vector2(0, radius), 1, square);
        // Neck join between the existing head and torso region; no hit target.
        Line(mesh, new Vector2(0, 186), new Vector2(0, 207), 2, new Color(.86f, .91f, .94f));
    }

    private static void Line(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized * width * .5f;
        int i = mesh.currentVertCount;
        mesh.AddVert(a - n, color, Vector2.zero); mesh.AddVert(a + n, color, Vector2.zero);
        mesh.AddVert(b + n, color, Vector2.zero); mesh.AddVert(b - n, color, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
    }
}
