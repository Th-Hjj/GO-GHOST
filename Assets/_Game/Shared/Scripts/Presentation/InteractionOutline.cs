using System;
using UnityEngine;

/// <summary>Reusable greybox contour around currently relevant physical interaction shapes.</summary>
public sealed class InteractionOutline : MonoBehaviour
{
    private Collider2D[] shapes;
    private SpriteRenderer[] visuals;
    private LineRenderer[] contours;
    private Material material;
    private Func<bool> relevant;
    private PlayerHealth viewer;
    public bool Visible { get; private set; }

    public void Bind(PlayerHealth player, Func<bool> isRelevant, bool includeVisuals = false)
    {
        viewer = player; relevant = isRelevant;
        if (shapes != null) return;
        shapes = GetComponentsInChildren<Collider2D>(true);
        visuals = includeVisuals || shapes.Length == 0 ? GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
        contours = new LineRenderer[shapes.Length + visuals.Length];
        material = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < contours.Length; i++)
        {
            var go = new GameObject("InteractionOutline" + i); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.loop = true; line.positionCount = 4;
            line.widthMultiplier = .055f; line.sortingOrder = 20;
            line.startColor = line.endColor = new Color(.06f, .35f, 1f, 1);
            line.enabled = false; contours[i] = line;
        }
    }

    private void LateUpdate()
    {
        bool on = viewer != null && !viewer.IsDead && viewer.GetComponent<PlayerInputReader>().GameplayInputEnabled
            && viewer.GetComponent<OfficerIdentity>()?.Deployment?.BrainPermissionTier >= 2 && relevant?.Invoke() == true;
        Visible = false;
        if (contours == null) return;
        for (int i = 0; i < contours.Length; i++)
        {
            var shape = i < shapes.Length ? shapes[i] : null;
            var visual = i >= shapes.Length ? visuals[i - shapes.Length] : null;
            var line = contours[i];
            bool visibleShape = shape != null && shape.enabled && shape.gameObject.activeInHierarchy;
            bool visibleArt = visual != null && visual.enabled && visual.gameObject.activeInHierarchy && visual.color.a > 0
                && (visual.GetComponent<Collider2D>() == null || !visual.GetComponent<Collider2D>().enabled);
            line.enabled = on && (visibleShape || visibleArt);
            if (!line.enabled) continue;
            Visible = true;
            // Box contours follow the actual transformed geometry (including rotations), not a screen-space icon.
            if (shape is BoxCollider2D box)
            {
                Vector2 half = box.size * .5f;
                for (int n = 0; n < 4; n++) line.SetPosition(n, box.transform.TransformPoint(box.offset
                    + new Vector2(n < 2 ? -half.x : half.x, n == 0 || n == 3 ? -half.y : half.y)));
            }
            else
            {
                var b = visibleShape ? shape.bounds : visual.bounds;
                line.SetPosition(0, new Vector3(b.min.x,b.min.y,0)); line.SetPosition(1,new Vector3(b.min.x,b.max.y,0));
                line.SetPosition(2,new Vector3(b.max.x,b.max.y,0)); line.SetPosition(3,new Vector3(b.max.x,b.min.y,0));
            }
        }
    }
    private void OnDisable() { Visible = false; if (contours != null) foreach (var line in contours) if (line != null) line.enabled = false; }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
