using UnityEngine;

/// <summary>Static uGUI edge glow. Overlay-safe; no camera, material, raycast or update loop.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionTerminalFrame : UnityEngine.UI.MaskableGraphic
{
    public bool Accent;

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var r = rectTransform.rect;
        Color blue = MissionUI.TerminalBlue;
        Ring(mesh, r, 7, Alpha(blue, Accent ? .08f : .025f));
        Ring(mesh, r, 3, Alpha(blue, Accent ? .17f : .065f));
        Ring(mesh, r, 1, Alpha(blue, Accent ? .90f : .32f));
        float length = Mathf.Min(22, r.height * .2f);
        Color corner = Alpha(blue, Accent ? 1 : .75f);
        Quad(mesh, r.xMin, r.yMax - 2, r.xMin + length, r.yMax, corner);
        Quad(mesh, r.xMin, r.yMax - length, r.xMin + 2, r.yMax, corner);
        Quad(mesh, r.xMax - length, r.yMin, r.xMax, r.yMin + 2, corner);
        Quad(mesh, r.xMax - 2, r.yMin, r.xMax, r.yMin + length, corner);
        if (Accent) Quad(mesh, r.xMin + 4, r.yMin + 9, r.xMin + 8, r.yMax - 9, corner);
    }

    private static Color Alpha(Color c, float alpha) { c.a = alpha; return c; }
    private static void Ring(UnityEngine.UI.VertexHelper m, Rect r, float width, Color c)
    {
        Quad(m, r.xMin, r.yMin, r.xMax, r.yMin + width, c);
        Quad(m, r.xMin, r.yMax - width, r.xMax, r.yMax, c);
        Quad(m, r.xMin, r.yMin + width, r.xMin + width, r.yMax - width, c);
        Quad(m, r.xMax - width, r.yMin + width, r.xMax, r.yMax - width, c);
    }

    private static void Quad(UnityEngine.UI.VertexHelper m, float x0, float y0, float x1, float y1, Color c)
    {
        int i = m.currentVertCount;
        m.AddVert(new Vector2(x0, y0), c, Vector2.zero);
        m.AddVert(new Vector2(x0, y1), c, Vector2.zero);
        m.AddVert(new Vector2(x1, y1), c, Vector2.zero);
        m.AddVert(new Vector2(x1, y0), c, Vector2.zero);
        m.AddTriangle(i, i + 1, i + 2);
        m.AddTriangle(i, i + 2, i + 3);
    }
}
