using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Flat generated silhouettes for the Gunner's rail rifle and the Swordsman's blade.
    // Structure is neutral grey-blue with a small accent in the owner's colour. Visual only:
    // the muzzle, the blade length and every hit shape stay where the executors put them.
    static class BossWeaponSprites
    {
        const float PixelsPerUnit = 500f;
        static readonly Color32 Body = new Color32(111, 129, 145, 255), Dark = new Color32(86, 100, 111, 255),
            Grip = new Color32(43, 49, 59, 255), Steel = new Color32(138, 154, 166, 255), CellOff = new Color32(70, 60, 58, 255);

        // The rifle's pivot is the centre of the old .54 bar, so the muzzle is still .27 in front of it.
        public const float RifleMuzzle = .27f;
        public const int ChargeCells = 5;
        const float RifleLeft = -.46f, RifleRight = RifleMuzzle, RifleBottom = -.17f, RifleTop = .12f;
        const float CellLeft = .005f, CellPitch = .051f, CellWidth = .039f, CellHalf = .019f;
        public static readonly Vector2 CellsCentre = new Vector2(CellLeft + (CellPitch * (ChargeCells - 1) + CellWidth) * .5f, 0);

        // The blade's pivot is its middle; the hand holds the root end, as with the old bar.
        public static readonly Vector2 BladeSize = new Vector2(SwordLength, .20f);
        const float SwordLength = 1.5f;

        static Sprite rifle, blade;
        static readonly Sprite[] cells = new Sprite[ChargeCells + 1];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Release(ref rifle); Release(ref blade);
            for (int i = 0; i < cells.Length; i++) Release(ref cells[i]);
        }

        static void Release(ref Sprite sprite)
        {
            if (sprite != null) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
            sprite = null;
        }

        public static Sprite Rifle
        {
            get
            {
                if (rifle != null) return rifle;
                Color32 accent = NeuralPalette.Gunner;
                var shapes = new List<(Vector2[] outline, Color32 colour)>
                {
                    (new[] { V(-.46f, .05f), V(-.30f, .07f), V(-.30f, -.05f), V(-.43f, -.07f), V(-.46f, -.03f) }, Dark),   // stock
                    (Box(-.30f, -.07f, -.02f, .07f), Body),                                                                // receiver
                    (new[] { V(-.14f, -.07f), V(-.06f, -.07f), V(-.09f, -.16f), V(-.16f, -.15f) }, Dark),                  // grip
                    (Box(-.24f, .08f, -.085f, .11f), Dark),                                                                // scope
                    (Box(-.085f, .08f, -.07f, .11f), accent),                                                              // lens
                    (Box(-.21f, .07f, -.19f, .08f), Dark), (Box(-.12f, .07f, -.10f, .08f), Dark),                          // scope mounts
                    (new[] { V(-.02f, .07f), V(RifleMuzzle, .07f), V(RifleMuzzle - .012f, .035f), V(-.02f, .035f) }, Body), // upper rail
                    (new[] { V(-.02f, -.035f), V(RifleMuzzle - .012f, -.035f), V(RifleMuzzle, -.07f), V(-.02f, -.07f) }, Body) // lower rail
                };
                for (int i = 0; i < ChargeCells; i++)
                    shapes.Add((Box(CellLeft + CellPitch * i, -CellHalf, CellLeft + CellPitch * i + CellWidth, CellHalf), CellOff));
                return rifle = Draw("Gunner rail rifle", RifleLeft, RifleBottom, RifleRight, RifleTop, Vector2.zero, shapes);
            }
        }

        // The lit charge cells alone, white so the renderer's own colour tints them.
        public static Sprite Cells(int lit)
        {
            lit = Mathf.Clamp(lit, 0, ChargeCells);
            if (lit == 0) return null;
            if (cells[lit] != null) return cells[lit];
            var shapes = new List<(Vector2[] outline, Color32 colour)>();
            for (int i = 0; i < lit; i++)
                shapes.Add((Box(CellLeft + CellPitch * i, -CellHalf, CellLeft + CellPitch * i + CellWidth, CellHalf), new Color32(255, 255, 255, 255)));
            float right = CellLeft + CellPitch * (ChargeCells - 1) + CellWidth;
            return cells[lit] = Draw("Rifle charge " + lit, CellLeft, -CellHalf, right, CellHalf, CellsCentre, shapes);
        }

        public static Sprite Blade
        {
            get
            {
                if (blade != null) return blade;
                Color32 accent = NeuralPalette.Swordsman;
                const float half = .0375f, tip = SwordLength, guard = .15f, root = .18f;
                var shapes = new List<(Vector2[] outline, Color32 colour)>
                {
                    (Box(0, -.03f, guard, .03f), Grip),                                                     // handle
                    (Box(0, -.04f, .022f, .04f), accent),                                                   // pommel
                    (Box(guard, -.09f, root, .09f), Dark),                                                  // guard
                    (new[] { V(root, half), V(tip - .09f, half), V(tip, -half), V(root, -half) }, Steel),   // blade with a slanted tip
                    (new[] { V(root, -half + .016f), V(tip - .019f, -half + .016f), V(tip, -half), V(root, -half) }, accent) // edge
                };
                return blade = Draw("Swordsman blade", 0, -.10f, SwordLength, .10f, new Vector2(SwordLength * .5f, 0), shapes);
            }
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);
        static Vector2[] Box(float left, float bottom, float right, float top)
            => new[] { V(left, bottom), V(right, bottom), V(right, top), V(left, top) };

        // Later shapes paint over earlier ones. Four samples per texel soften every edge.
        static Sprite Draw(string name, float left, float bottom, float right, float top, Vector2 pivot,
            List<(Vector2[] outline, Color32 colour)> shapes)
        {
            int width = Mathf.CeilToInt((right - left) * PixelsPerUnit), height = Mathf.CeilToInt((top - bottom) * PixelsPerUnit);
            var pixels = new Color32[width * height];
            var offsets = new[] { V(.25f, .25f), V(.75f, .25f), V(.25f, .75f), V(.75f, .75f) };
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    foreach (var offset in offsets)
                    {
                        var point = new Vector2(left + (x + offset.x) / PixelsPerUnit, bottom + (y + offset.y) / PixelsPerUnit);
                        for (int i = shapes.Count - 1; i >= 0; i--)
                        {
                            if (!Inside(shapes[i].outline, point)) continue;
                            r += shapes[i].colour.r; g += shapes[i].colour.g; b += shapes[i].colour.b; a += 255;
                            break;
                        }
                    }
                    if (a <= 0) continue;
                    float covered = a / 255f;
                    pixels[y * width + x] = new Color32((byte)(r / covered), (byte)(g / covered), (byte)(b / covered), (byte)(a / 4f));
                }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2((pivot.x - left) / (right - left), (pivot.y - bottom) / (top - bottom)), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static bool Inside(Vector2[] path, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
            {
                Vector2 a = path[i], b = path[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
