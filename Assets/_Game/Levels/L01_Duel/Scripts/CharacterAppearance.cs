using UnityEngine;

namespace GoGhost.Duel
{
    // One run-owned ink mask, with outline UI and cold-white combat renderings.
    // No static texture or saved progression: a new character starts with a clean face.
    public sealed class CharacterAppearance : System.IDisposable
    {
        public const int Resolution = 512;
        public const int NameLimit = 20;
        public const float BrushRadius = 14f;
        // Same neutral gray-blue as the configuration body's .78-opacity Secondary contour
        // over black. Bake the opacity into RGB so the face interior stays opaque black.
        public static Color Tint => Color.Lerp(Color.black, NeuralPalette.Secondary, .78f);
        public string Name { get; set; } = "";
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "YOU" : Name.Trim();
        public Texture2D Texture { get; private set; }
        public Sprite Sprite { get; private set; }
        public Texture2D CombatTexture { get; private set; }
        public Sprite CombatSprite { get; private set; }
        readonly Color32[] blank, pixels, combatPixels;
        readonly byte[] ink;
        const float DrawingRadius = .86f;

        public CharacterAppearance(Sprite source)
        {
            // The combat Knob's visible diameter is 36 of its 40-pixel sprite rectangle.
            // Rebuild that same ellipse smoothly at drawing resolution, keeping its pivot,
            // visible size and world bounds without magnifying the old low-resolution edge.
            blank = new Color32[Resolution * Resolution];
            pixels = new Color32[blank.Length];
            combatPixels = new Color32[blank.Length];
            ink = new byte[blank.Length];
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float u = (x + .5f) / Resolution, v = (y + .5f) / Resolution;
                float radius = new Vector2(u * 2 - 1, v * 2 - 1).magnitude;
                float alpha = Mathf.Clamp01((.9f - radius) * Resolution * .5f + .5f);
                float rim = Mathf.Clamp01((radius - (.9f - 28f / Resolution)) * Resolution * .5f + .5f);
                byte value = (byte)Mathf.RoundToInt(rim * 255);
                blank[y * Resolution + x] = new Color32(value, value, value, (byte)Mathf.RoundToInt(alpha * 255));
            }
            Texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            Texture.name = "Character Face";
            Texture.filterMode = FilterMode.Bilinear;
            Texture.wrapMode = TextureWrapMode.Clamp;
            CombatTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            CombatTexture.name = "Character Combat Face";
            CombatTexture.filterMode = FilterMode.Bilinear;
            CombatTexture.wrapMode = TextureWrapMode.Clamp;
            Clear();
            float pixelsPerUnit = source != null ? Resolution / source.bounds.size.x : Resolution;
            Vector2 pivot = source != null ? source.pivot / source.rect.size : Vector2.one * .5f;
            Sprite = Sprite.Create(Texture, new Rect(0, 0, Resolution, Resolution), pivot,
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            Sprite.name = "Character Face";
            CombatSprite = Sprite.Create(CombatTexture, new Rect(0, 0, Resolution, Resolution), pivot,
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            CombatSprite.name = "Character Combat Face";
        }

        public static bool Contains(Vector2 uv) => (uv * 2 - Vector2.one).sqrMagnitude <= DrawingRadius * DrawingRadius;

        public void Clear()
        {
            System.Array.Copy(blank, pixels, blank.Length);
            System.Array.Clear(ink, 0, ink.Length);
            for (int i = 0; i < combatPixels.Length; i++)
                combatPixels[i] = new Color32(255, 255, 255, blank[i].a);
            Upload();
        }

        public void Draw(Vector2 from, Vector2 to)
        {
            // Each event paints a continuous antialiased capsule, even on a fast pointer move.
            if (!Contains(from) || !Contains(to)) return;
            Vector2 a = from * Resolution - Vector2.one * .5f, b = to * Resolution - Vector2.one * .5f;
            Vector2 delta = b - a;
            float lengthSquared = delta.sqrMagnitude;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - BrushRadius - 1));
            int x1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + BrushRadius + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - BrushRadius - 1));
            int y1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + BrushRadius + 1));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!Contains(new Vector2((x + .5f) / Resolution, (y + .5f) / Resolution))) continue;
                Vector2 point = new Vector2(x, y);
                float t = lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / lengthSquared) : 0;
                float coverage = Mathf.Clamp01(BrushRadius + .5f - Vector2.Distance(point, a + delta * t));
                if (coverage <= 0) continue;
                int i = y * Resolution + x;
                ink[i] = (byte)Mathf.Max(ink[i], Mathf.RoundToInt(255 * coverage));
                byte value = (byte)Mathf.Max(blank[i].r, ink[i]);
                pixels[i] = new Color32(value, value, value, blank[i].a);
                byte inverse = (byte)(255 - ink[i]);
                combatPixels[i] = new Color32(inverse, inverse, inverse, blank[i].a);
            }
            Upload();
        }

        void Upload()
        {
            Texture.SetPixels32(pixels); Texture.Apply(false, false);
            CombatTexture.SetPixels32(combatPixels); CombatTexture.Apply(false, false);
        }

        public void Dispose()
        {
            if (Sprite != null) Object.Destroy(Sprite);
            if (Texture != null) Object.Destroy(Texture);
            if (CombatSprite != null) Object.Destroy(CombatSprite);
            if (CombatTexture != null) Object.Destroy(CombatTexture);
            Sprite = CombatSprite = null; Texture = CombatTexture = null;
        }
    }
}
