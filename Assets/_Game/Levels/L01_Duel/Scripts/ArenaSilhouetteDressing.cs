using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Rim light on every landing edge and hairlines on the walls and ceiling of the black arena structure.
    // Visual only: it reads the supports' bounds each frame and owns no collider.
    sealed class ArenaSilhouetteDressing : MonoBehaviour
    {
        const float RimHeight = .045f, Hairline = .022f;

        readonly List<(Collider2D support, SpriteRenderer surface, SpriteRenderer rim)> platforms = new();
        readonly List<Object> owned = new();
        Material material;
        int sortingLayer;
        Sprite pixel, ramp;

        // supports[0] is the floor; the rest are this encounter's one-way platforms.
        public static void Create(Transform attempt, Bounds arena, Collider2D[] supports, ArenaLook look)
        {
            var surface = supports != null && supports.Length > 0 && supports[0] != null
                ? supports[0].GetComponent<SpriteRenderer>() : null;
            if (surface == null) return;
            var dressing = new GameObject("Arena Silhouette Dressing").AddComponent<ArenaSilhouetteDressing>();
            dressing.transform.SetParent(attempt, false);
            dressing.material = surface.sharedMaterial;
            dressing.sortingLayer = surface.sortingLayerID;
            dressing.Build(arena, supports, look);
        }

        void Build(Bounds arena, Collider2D[] supports, ArenaLook look)
        {
            pixel = MakeSprite(2, 2, (u, v) => 1f);
            // The floor edge is brightest under the middle of the arena.
            ramp = MakeSprite(64, 4, (u, v) => Mathf.Pow(Mathf.Lerp(.3f, 1f, 1f - Mathf.Abs(2f * u - 1f)), 2.2f));

            float floor = arena.min.y, left = arena.min.x, right = arena.max.x, ceiling = arena.max.y;
            Place(Quad("Floor rim", ramp, look.Rim), new Vector2(arena.center.x, floor - RimHeight * .5f),
                new Vector2(arena.size.x, RimHeight));
            Place(Quad("Ceiling rim", pixel, NeuralPalette.Alpha(look.Rim, .06f)),
                new Vector2(arena.center.x, ceiling + Hairline * .5f), new Vector2(arena.size.x, Hairline));
            Place(Quad("Left wall rim", pixel, NeuralPalette.Alpha(look.Rim, .08f)),
                new Vector2(left - Hairline * .5f, arena.center.y), new Vector2(Hairline, arena.size.y));
            Place(Quad("Right wall rim", pixel, NeuralPalette.Alpha(look.Rim, .08f)),
                new Vector2(right + Hairline * .5f, arena.center.y), new Vector2(Hairline, arena.size.y));

            for (int i = 1; i < supports.Length; i++)
                if (supports[i] != null)
                    platforms.Add((supports[i], supports[i].GetComponent<SpriteRenderer>(), Quad("Platform rim", pixel, look.Rim)));
            Follow();
        }

        void LateUpdate() => Follow();

        // Final Boss phase two moves the platforms. A collider's bounds only refresh on a physics step,
        // which does not run during the countdown, so each rim follows the platform's drawn sprite.
        void Follow()
        {
            foreach (var (support, surface, rim) in platforms)
            {
                bool shown = support != null && support.gameObject.activeInHierarchy && support.enabled;
                rim.enabled = shown;
                if (!shown) continue;
                Bounds bounds = surface != null ? surface.bounds : support.bounds;
                Place(rim, new Vector2(bounds.center.x, bounds.max.y - RimHeight * .5f), new Vector2(bounds.size.x, RimHeight));
            }
        }

        SpriteRenderer Quad(string label, Sprite sprite, Color color)
        {
            var quad = new GameObject(label).AddComponent<SpriteRenderer>();
            quad.transform.SetParent(transform, false);
            quad.sprite = sprite;
            quad.sharedMaterial = material;
            quad.sortingLayerID = sortingLayer;
            quad.sortingOrder = 1;
            quad.color = color;
            return quad;
        }

        static void Place(SpriteRenderer quad, Vector2 centre, Vector2 size)
        {
            Vector2 source = quad.sprite.bounds.size;
            quad.transform.position = new Vector3(centre.x, centre.y, 0);
            quad.transform.localScale = new Vector3(size.x / source.x, size.y / source.y, 1);
        }

        // White texture whose alpha comes from the given function; u and v run 0..1 from the lower left.
        Sprite MakeSprite(int width, int height, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Arena dressing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(alpha((x + .5f) / width, (y + .5f) / height))));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), width, 0, SpriteMeshType.FullRect);
            sprite.name = "Arena dressing";
            sprite.hideFlags = HideFlags.DontSave;
            owned.Add(texture);
            owned.Add(sprite);
            return sprite;
        }

        void OnDestroy()
        {
            foreach (var item in owned) if (item != null) Destroy(item);
        }
    }
}
