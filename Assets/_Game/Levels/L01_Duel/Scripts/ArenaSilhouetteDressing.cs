using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // A thin outline round the black arena structure: full strength on every landing edge, dim on the other sides.
    // Visual only: it reads the supports' bounds each frame and owns no collider.
    sealed class ArenaSilhouetteDressing : MonoBehaviour
    {
        const float RimHeight = .045f;
        // Sides that nobody lands on carry the same colour at this strength (Thomas, 2026-11-13).
        const float SideStrength = .14f;

        sealed class PlatformOutline
        {
            public Collider2D Support;
            public SpriteRenderer Surface, Top, Bottom, Left, Right;
        }

        readonly List<PlatformOutline> platforms = new();
        readonly List<Object> owned = new();
        Material material;
        int sortingLayer;
        Sprite pixel, ramp;
        readonly List<SpriteRenderer> frameHairlines = new();

        // supports[0] is the floor; the rest are this encounter's one-way platforms.
        // steady: the camera moves and zooms here. A hard-edged line a few pixels thick changes thickness as it
        // travels, and snapping it to the pixel grid makes neighbouring lines step at different moments; both flicker.
        // Here each line is drawn by ArenaLine, which spreads it over the pixels it actually covers.
        public static void Create(Transform attempt, Bounds arena, Collider2D[] supports, ArenaLook look, bool steady = false)
        {
            var surface = supports != null && supports.Length > 0 && supports[0] != null
                ? supports[0].GetComponent<SpriteRenderer>() : null;
            if (surface == null) return;
            var dressing = new GameObject("Arena Silhouette Dressing").AddComponent<ArenaSilhouetteDressing>();
            dressing.transform.SetParent(attempt, false);
            dressing.material = surface.sharedMaterial;
            dressing.sortingLayer = surface.sortingLayerID;
            dressing.steady = steady;
            if (steady)
            {
                dressing.material = new Material(Resources.Load<Shader>("ArenaLine")) { hideFlags = HideFlags.HideAndDontSave };
                dressing.owned.Add(dressing.material);
            }
            dressing.Build(arena, supports, look);
        }

        void Build(Bounds arena, Collider2D[] supports, ArenaLook look)
        {
            pixel = MakeSprite(2, 2, (u, v) => 1f);
            // The floor edge is brightest under the middle of the arena.
            ramp = MakeSprite(64, 4, (u, v) => Mathf.Pow(Mathf.Lerp(.3f, 1f, 1f - Mathf.Abs(2f * u - 1f)), 2.2f));

            frame = arena;
            floorRim = Quad("Floor rim", ramp, look.Rim);
            Color side = NeuralPalette.Alpha(look.Rim, SideStrength);
            frameHairlines.Add(Quad("Ceiling edge", pixel, side));
            frameHairlines.Add(Quad("Left wall edge", pixel, side));
            frameHairlines.Add(Quad("Right wall edge", pixel, side));
            PlaceFrame();

            for (int i = 1; i < supports.Length; i++)
            {
                if (supports[i] == null) continue;
                var outline = new PlatformOutline
                {
                    Support = supports[i], Surface = supports[i].GetComponent<SpriteRenderer>(),
                    Top = Quad("Platform rim", pixel, look.Rim), Bottom = Quad("Platform underside", pixel, side),
                    Left = Quad("Platform end", pixel, side), Right = Quad("Platform end", pixel, side)
                };
                frameHairlines.Add(outline.Bottom); frameHairlines.Add(outline.Left); frameHairlines.Add(outline.Right);
                platforms.Add(outline);
            }
            Follow();
        }

        // The EMP draws its own violet outline round each platform; a second line inside it reads as a mistake.
        // The platforms' dim sides give way to it. The landing edges and the arena frame stay as in every mission.
        bool sidesHidden;
        internal void HidePlatformHairlines()
        {
            sidesHidden = true;
            // The first three are the ceiling and the two walls.
            for (int i = 3; i < frameHairlines.Count; i++) if (frameHairlines[i] != null) frameHairlines[i].enabled = false;
        }

        void LateUpdate() { if (steady) PlaceFrame(); Follow(); }

        bool steady;
        Bounds frame;
        SpriteRenderer floorRim;
        Camera view;
        // One screen pixel in world units; ArenaLine needs each quad that much larger on every side. Zero elsewhere.
        const float rim = RimHeight;
        float grid;

        void ReadGrid()
        {
            grid = 0;
            if (!steady) return;
            if (view == null) view = Camera.main;
            if (view != null) grid = 2 * view.orthographicSize / Mathf.Max(1, view.pixelHeight);
        }

        static float SnapX(float x) => x;
        static float SnapY(float y) => y;

        void PlaceFrame()
        {
            ReadGrid();
            float floor = SnapY(frame.min.y), left = SnapX(frame.min.x), right = SnapX(frame.max.x), ceiling = SnapY(frame.max.y);
            Place(floorRim, new Vector2(frame.center.x, floor - rim * .5f), new Vector2(frame.size.x, rim));
            Place(frameHairlines[0], new Vector2(frame.center.x, ceiling + rim * .5f), new Vector2(frame.size.x, rim));
            Place(frameHairlines[1], new Vector2(left - rim * .5f, frame.center.y), new Vector2(rim, frame.size.y));
            Place(frameHairlines[2], new Vector2(right + rim * .5f, frame.center.y), new Vector2(rim, frame.size.y));
        }

        // Final Boss phase two moves the platforms. A collider's bounds only refresh on a physics step,
        // which does not run during the countdown, so each rim follows the platform's drawn sprite.
        void Follow()
        {
            foreach (var outline in platforms)
            {
                var support = outline.Support;
                bool shown = support != null && support.gameObject.activeInHierarchy && support.enabled;
                outline.Top.enabled = shown;
                outline.Bottom.enabled = outline.Left.enabled = outline.Right.enabled = shown && !sidesHidden;
                if (!shown) continue;
                Bounds bounds = outline.Surface != null ? outline.Surface.bounds : support.bounds;
                float top = SnapY(bounds.max.y), bottom = SnapY(bounds.min.y), west = SnapX(bounds.min.x), east = SnapX(bounds.max.x);
                float sideHeight = Mathf.Max(0, top - bottom - rim), width = east - west, middle = (west + east) * .5f;
                Place(outline.Top, new Vector2(middle, top - rim * .5f), new Vector2(width, rim));
                Place(outline.Bottom, new Vector2(middle, bottom + rim * .5f), new Vector2(width, rim));
                Place(outline.Left, new Vector2(west + rim * .5f, bottom + sideHeight * .5f), new Vector2(rim, sideHeight));
                Place(outline.Right, new Vector2(east - rim * .5f, bottom + sideHeight * .5f), new Vector2(rim, sideHeight));
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
            if (steady)
            {
                var block = new MaterialPropertyBlock();
                quad.GetPropertyBlock(block);
                block.SetColor("_Tint", color);
                quad.SetPropertyBlock(block);
            }
            return quad;
        }

        void Place(SpriteRenderer quad, Vector2 centre, Vector2 size)
        {
            size += Vector2.one * (grid * 2);
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
