using UnityEngine;

namespace GoGhost.Duel
{
    // A slightly larger copy behind the opaque sprite only shows outside its silhouette.
    internal static class EmpOutline
    {
        public static SpriteRenderer Create(SpriteRenderer source, Material material, int order, float width)
        {
            var edge = new GameObject("EMP edge").AddComponent<SpriteRenderer>();
            edge.transform.SetParent(source.transform, false);
            edge.sprite = source.sprite;
            edge.drawMode = source.drawMode;
            if (edge.drawMode != SpriteDrawMode.Simple) edge.size = source.size;
            edge.sharedMaterial = material;
            edge.sortingLayerID = source.sortingLayerID;
            edge.sortingOrder = order;
            Vector3 size = Vector3.Scale(source.localBounds.size, source.transform.lossyScale);
            edge.transform.localScale = new Vector3(1 + 2 * width / Mathf.Max(.001f, Mathf.Abs(size.x)),
                1 + 2 * width / Mathf.Max(.001f, Mathf.Abs(size.y)), 1);
            edge.color = NeuralPalette.Alpha(NeuralPalette.Spine, 0);
            return edge;
        }
    }
}
