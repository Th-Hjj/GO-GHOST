using UnityEngine;

/// <summary>Shared screen-space bubble following a world speaker. No narrative or input ownership.</summary>
public sealed class WorldSpeechBubble : MonoBehaviour
{
    private RectTransform panel, canvas;
    private Transform speaker;
    private Collider2D speakerBody;
    private SpeechBubblePointer pointer;
    public Transform Speaker => speaker;

    public void Initialize(RectTransform root)
    {
        canvas = root;
        panel = (RectTransform)transform;
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.pivot = new Vector2(.5f, .5f);
        var tail = new GameObject("SpeechPointer", typeof(RectTransform), typeof(CanvasRenderer));
        var rect = (RectTransform)tail.transform;
        rect.SetParent(panel.parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        tail.transform.SetSiblingIndex(panel.GetSiblingIndex());
        pointer = tail.AddComponent<SpeechBubblePointer>();
        pointer.color = Color.white;
        pointer.raycastTarget = false;
    }

    public void SetSpeaker(Transform target)
    {
        speaker = target;
        speakerBody = speaker != null ? speaker.GetComponent<Collider2D>() : null;
    }

    private void LateUpdate() => PositionBubble();

    public void PositionBubble()
    {
        if (canvas == null || panel == null) return;
        var camera = Camera.main;
        bool anchored = speaker != null && speaker.gameObject.activeInHierarchy && camera != null;
        pointer.enabled = anchored;
        Vector2 target = Vector2.zero;
        if (anchored)
        {
            Vector3 head = speakerBody != null && speakerBody.enabled
                ? new Vector3(speakerBody.bounds.center.x, speakerBody.bounds.max.y + .2f, speaker.position.z)
                : speaker.position;
            Vector3 screen = camera.WorldToScreenPoint(head);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out target);
        }
        Rect bounds = canvas.rect;
        Vector2 half = panel.sizeDelta * .5f;
        Vector2 center = target + Vector2.up * (half.y + 42);
        if (!anchored) center = Vector2.zero;
        else if (center.y + half.y > bounds.yMax - 20) center.y = target.y - half.y - 42;
        center.x = Mathf.Clamp(center.x, bounds.xMin + half.x + 20, bounds.xMax - half.x - 20);
        center.y = Mathf.Clamp(center.y, bounds.yMin + half.y + 20, bounds.yMax - half.y - 20);
        panel.anchoredPosition = center;
        if (!anchored) return;
        target.x = Mathf.Clamp(target.x, bounds.xMin + 8, bounds.xMax - 8);
        target.y = Mathf.Clamp(target.y, bounds.yMin + 8, bounds.yMax - 8);
        // Attach to the nearest horizontal edge; edge clamping must not detach the speaker pointer.
        float edgeY = target.y >= center.y ? center.y + half.y : center.y - half.y;
        Vector2 edge = new Vector2(Mathf.Clamp(target.x, center.x - half.x + 20, center.x + half.x - 20), edgeY);
        pointer.SetPoints(edge + Vector2.left * 9, edge + Vector2.right * 9, target);
    }
}

public sealed class SpeechBubblePointer : UnityEngine.UI.MaskableGraphic
{
    private Vector2 a, b, tip;
    public void SetPoints(Vector2 left, Vector2 right, Vector2 target)
    { a = left; b = right; tip = target; SetVerticesDirty(); }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(tip, color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2);
    }
}
