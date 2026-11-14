using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Rim light on every landing edge, lift emitters under the platforms and tier marks on the walls.
    // Visual only: it reads the supports' bounds each frame and owns no collider.
    sealed class ArenaSilhouetteDressing : MonoBehaviour
    {
        const float RimHeight = .045f, Hairline = .022f;
        const float NubWidth = .54f, NubHeight = .09f, NubInset = .81f;
        const float LiftWidth = .68f, LiftHeight = .80f, LiftAlpha = .34f, LiftPeriod = 2.6f;

        sealed class PlatformPieces
        {
            public Collider2D Support;
            public SpriteRenderer Rim, LeftNub, RightNub, LeftLift, RightLift;
        }

        readonly List<PlatformPieces> platforms = new();
        readonly List<Object> owned = new();
        Material material;
        int sortingLayer;
        Sprite pixel, lift, ramp;
        Color liftColor;

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
            // Brightest at the top centre, gone by the sides and the bottom.
            lift = MakeSprite(32, 32, (u, v) =>
            {
                float dx = (u - .5f) * 2f, dy = 1f - v;
                float reach = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return reach * reach;
            });
            // The floor edge is brightest under the middle of the arena.
            ramp = MakeSprite(64, 4, (u, v) => Mathf.Pow(Mathf.Lerp(.3f, 1f, 1f - Mathf.Abs(2f * u - 1f)), 2.2f));
            liftColor = NeuralPalette.Alpha(look.Rim, LiftAlpha);

            float floor = arena.min.y, left = arena.min.x, right = arena.max.x, ceiling = arena.max.y;
            Place(Quad("Floor rim", ramp, look.Rim, 1), new Vector2(arena.center.x, floor - RimHeight * .5f),
                new Vector2(arena.size.x, RimHeight));
            Place(Quad("Ceiling rim", pixel, NeuralPalette.Alpha(look.Rim, .06f), 1),
                new Vector2(arena.center.x, ceiling + Hairline * .5f), new Vector2(arena.size.x, Hairline));
            Place(Quad("Left wall rim", pixel, NeuralPalette.Alpha(look.Rim, .08f), 1),
                new Vector2(left - Hairline * .5f, arena.center.y), new Vector2(Hairline, arena.size.y));
            Place(Quad("Right wall rim", pixel, NeuralPalette.Alpha(look.Rim, .08f), 1),
                new Vector2(right + Hairline * .5f, arena.center.y), new Vector2(Hairline, arena.size.y));

            // One mark per platform tier on each wall.
            for (int tier = 1; tier <= 3; tier++)
            {
                float top = floor + DuelGame.PlatformTierRise * tier;
                if (top > ceiling - .5f) break;
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side < 0 ? left + .08f : right - .08f;
                    Place(Quad("Tier mark", pixel, ArenaLook.Structure, 1), new Vector2(x, top - .135f), new Vector2(.16f, .27f));
                    Place(Quad("Tier mark rim", pixel, NeuralPalette.Alpha(look.Rim, .22f), 2),
                        new Vector2(x, top - Hairline * .5f), new Vector2(.16f, Hairline));
                }
            }

            for (int i = 1; i < supports.Length; i++)
            {
                if (supports[i] == null) continue;
                platforms.Add(new PlatformPieces
                {
                    Support = supports[i],
                    Rim = Quad("Platform rim", pixel, look.Rim, 1),
                    LeftNub = Quad("Lift emitter", pixel, ArenaLook.Structure, 1),
                    RightNub = Quad("Lift emitter", pixel, ArenaLook.Structure, 1),
                    LeftLift = Quad("Lift glow", lift, liftColor, -1),
                    RightLift = Quad("Lift glow", lift, liftColor, -1)
                });
            }
            Follow();
        }

        void LateUpdate() => Follow();

        // Final Boss phase two moves the platforms; every attached piece keeps to the collider.
        void Follow()
        {
            for (int i = 0; i < platforms.Count; i++)
            {
                var pieces = platforms[i];
                bool shown = pieces.Support != null && pieces.Support.gameObject.activeInHierarchy && pieces.Support.enabled;
                pieces.Rim.enabled = pieces.LeftNub.enabled = pieces.RightNub.enabled
                    = pieces.LeftLift.enabled = pieces.RightLift.enabled = shown;
                if (!shown) continue;
                Bounds bounds = pieces.Support.bounds;
                Place(pieces.Rim, new Vector2(bounds.center.x, bounds.max.y - RimHeight * .5f), new Vector2(bounds.size.x, RimHeight));
                float reach = Mathf.Max(0, bounds.extents.x - NubInset);
                float pulse = Mathf.Lerp(.7f, 1f, .5f + .5f * Mathf.Sin(Time.time * 2f * Mathf.PI / LiftPeriod + i * 1.7f));
                Color glow = NeuralPalette.Alpha(liftColor, liftColor.a * pulse);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = bounds.center.x + side * reach;
                    Place(side < 0 ? pieces.LeftNub : pieces.RightNub,
                        new Vector2(x, bounds.min.y - NubHeight * .5f), new Vector2(NubWidth, NubHeight));
                    var light = side < 0 ? pieces.LeftLift : pieces.RightLift;
                    Place(light, new Vector2(x, bounds.min.y - LiftHeight * .5f), new Vector2(LiftWidth, LiftHeight));
                    light.color = glow;
                }
            }
        }

        SpriteRenderer Quad(string label, Sprite sprite, Color color, int order)
        {
            var quad = new GameObject(label).AddComponent<SpriteRenderer>();
            quad.transform.SetParent(transform, false);
            quad.sprite = sprite;
            quad.sharedMaterial = material;
            quad.sortingLayerID = sortingLayer;
            quad.sortingOrder = order;
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
