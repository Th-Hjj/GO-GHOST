using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // One mission's backlit look. Colours are sRGB identities; strengths are Editor tuning.
    readonly struct ArenaLook
    {
        public static readonly Color Structure = new Color32(5, 5, 6, 255);
        public readonly string Artwork, FarLabel, NearLabel;
        public readonly Color Haze, Rim;
        // Near-plate opacity. The casino's entrance and palms need more than the shared value to read.
        public readonly float NearOpacity;
        // An opaque far plate would hide the sky haze, so that haze is added in front of it.
        public readonly bool OpaqueSky;

        ArenaLook(string artwork, string farLabel, string nearLabel, Color haze, Color rim, bool opaqueSky,
            float nearOpacity = PlateOpacity)
        {
            Artwork = artwork; FarLabel = farLabel; NearLabel = nearLabel; Haze = haze; Rim = rim;
            OpaqueSky = opaqueSky; NearOpacity = nearOpacity;
        }

        // Thomas, 2026-11-13: .60, .48, .40, then settled at .50.
        public const float PlateOpacity = .50f;

        // Mission 3 starts cold white; the EMP dyes its haze violet. The Test Room and the Bonus have no plates.
        public static ArenaLook For(int wave) => wave switch
        {
            1 => new ArenaLook("Industrial", "Distant Works", "Steel Columns", NeuralPalette.Gunner,
                new Color32(255, 233, 219, 255), false),
            2 => new ArenaLook("Casino", "Resort Hotels", "Casino Entrance", NeuralPalette.Swordsman,
                new Color32(255, 226, 230, 255), false, .70f),
            DuelGame.FinalBossWave => new ArenaLook("Megacity", "Distant Skyline", "Corporate Towers", NeuralPalette.Primary,
                NeuralPalette.Primary, true),
            _ => new ArenaLook(null, null, null, NeuralPalette.Secondary, NeuralPalette.Primary, false)
        };
    }

    // The two authored plates fill the fixed arena as authored; only their sampled region moves.
    // Three generated haze layers sit behind, between and in front of them. The art itself is never altered.
    sealed class IndustrialArenaBackdrop : MonoBehaviour
    {
        const float VisibleWidthPixels = 966f;
        const float FarTravelPixels = 7f;
        const float NearTravelPixels = 36f;
        // Thomas, 2026-11-13: lowered twice at his request (.75, then .55).
        const float HazeStrength = .38f;

        Transform player;
        Bounds arena;
        RawImage far, near, farLights;
        Material contourMaterial, hazeMaterial;
        readonly List<Texture2D> hazeTextures = new();
        // Sky, mid and floor haze in the mission colour, and the same three in EMP violet once the sweep exists.
        readonly RawImage[] haze = new RawImage[3];
        RawImage[] empHaze;

        public static void Create(Transform attempt, Transform player, Bounds arena, int wave)
        {
            var look = ArenaLook.For(wave);
            Texture2D farTexture = null, nearTexture = null;
            if (look.Artwork != null)
            {
                farTexture = Resources.Load<Texture2D>($"Arena/{look.Artwork}Far");
                nearTexture = Resources.Load<Texture2D>($"Arena/{look.Artwork}Near");
                if (!farTexture || !nearTexture)
                {
                    Debug.LogError($"Mission {wave} {look.Artwork} background textures could not be loaded.");
                    farTexture = nearTexture = null;
                }
            }
            var hazeShader = Resources.Load<Shader>("ArenaHaze");
            if (hazeShader == null)
            {
                Debug.LogError("Arena haze shader could not be loaded.");
                return;
            }

            var root = new GameObject($"{look.Artwork ?? "Neutral"} Arena Background", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)root.transform;
            rect.SetParent(attempt, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -100;
            rect.position = arena.center;
            rect.sizeDelta = arena.size;

            var backdrop = root.AddComponent<IndustrialArenaBackdrop>();
            backdrop.player = player;
            backdrop.arena = arena;
            backdrop.hazeMaterial = new Material(hazeShader) { name = "Arena haze", hideFlags = HideFlags.DontSave };
            bool plates = farTexture != null;
            if (!look.OpaqueSky) backdrop.haze[0] = backdrop.AddHaze("Sky haze", backdrop.SkyHaze(look.Haze));
            if (plates) backdrop.far = backdrop.AddPlate(look.FarLabel, farTexture, ArenaLook.PlateOpacity);
            if (look.OpaqueSky) backdrop.haze[0] = backdrop.AddHaze("Sky haze", backdrop.SkyHaze(look.Haze));
            // Only the factory has this layer: the two stack beacons.
            var lights = plates ? Resources.Load<Texture2D>($"Arena/{look.Artwork}FarLights") : null;
            if (lights != null) backdrop.farLights = backdrop.AddPlate("Beacons", lights, 1f);
            backdrop.haze[1] = backdrop.AddHaze("Mid haze", backdrop.MidHaze(look.Haze));
            if (plates) backdrop.near = backdrop.AddPlate(look.NearLabel, nearTexture, look.NearOpacity);
            backdrop.haze[2] = backdrop.AddHaze("Floor haze", backdrop.FloorHaze(look.Haze));
            backdrop.UpdateParallax();
        }

        RawImage AddPlate(string label, Texture texture, float opacity)
        {
            var plate = new GameObject(label, typeof(RawImage)).GetComponent<RawImage>();
            var rect = plate.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            plate.texture = texture;
            plate.color = new Color(1f, 1f, 1f, opacity);
            plate.raycastTarget = false;
            return plate;
        }

        RawImage AddHaze(string label, Texture2D texture)
        {
            var layer = AddPlate(label, texture, 1f);
            layer.material = hazeMaterial;
            return layer;
        }

        // Authored in display space, then stored linear and premultiplied.
        Texture2D HazeTexture(int width, int height, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            {
                name = "Arena haze", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    // u runs left to right; depth runs from the ceiling (0) to the floor (1).
                    pixels[y * width + x] = pixel((x + .5f) / width, 1f - (y + .5f) / height);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            hazeTextures.Add(texture);
            return texture;
        }

        static Color Linear(Color display, float occlusion)
            => new Color(Mathf.GammaToLinearSpace(display.r), Mathf.GammaToLinearSpace(display.g),
                Mathf.GammaToLinearSpace(display.b), occlusion);

        // Colour that gathers toward the floor, plus a glow rising from the horizon. Added as light.
        Texture2D SkyHaze(Color colour) => HazeTexture(256, 128, (u, depth) =>
        {
            float low = Mathf.Clamp01((depth - .36f) / .64f);
            Color sky = colour * (.21f * HazeStrength * Mathf.Pow(low, 1.5f));
            float dx = (u - .5f) / .58f, dy = (1f - depth) / .48f;
            float glow = .32f * HazeStrength * Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            return Linear(Color.Lerp(sky, colour, glow), 0);
        });

        // Thin haze between the far and near plates.
        Texture2D MidHaze(Color colour) => HazeTexture(4, 128, (u, depth) =>
        {
            float amount = .11f * HazeStrength * Mathf.Clamp01((depth - .42f) / .58f);
            return Linear(colour * amount, 1f - Mathf.Pow(1f - amount, 2.2f));
        });

        // Haze over the floor in front of the near plate.
        Texture2D FloorHaze(Color colour) => HazeTexture(4, 128, (u, depth) =>
        {
            float amount = .16f * HazeStrength * Mathf.Clamp01((depth - .724f) / .276f);
            return Linear(colour * amount, 1f - Mathf.Pow(1f - amount, 2.2f));
        });

        // Slow beacon cycle in seconds; the lights dim to a quarter and return.
        const float BeaconPeriod = 3.2f;

        void LateUpdate()
        {
            UpdateParallax();
            if (farLights != null)
            {
                float glow = .5f + .5f * Mathf.Sin(Time.time * 2f * Mathf.PI / BeaconPeriod);
                farLights.color = new Color(1f, 1f, 1f, Mathf.Lerp(.25f, 1f, glow));
            }
        }

        internal void AddEmpContours(Material material)
        {
            if (far == null || near == null) return;
            var mask = Resources.Load<Texture2D>("Arena/MegacityContours");
            if (mask == null)
            {
                Debug.LogError("Mission 3 architectural contour texture could not be loaded.");
                return;
            }
            var contours = AddPlate("EMP architectural contours", mask, 1f);
            contourMaterial = contours.material = material;
            contourMaterial.SetFloat("_CityContours", 1);
            // Violet copies of the haze sit directly above their cold-white layers, hidden until the sweep dyes them.
            empHaze = new[]
            {
                AddHaze("EMP sky haze", SkyHaze(NeuralPalette.Emp)),
                AddHaze("EMP mid haze", MidHaze(NeuralPalette.Emp)),
                AddHaze("EMP floor haze", FloorHaze(NeuralPalette.Emp))
            };
            for (int i = 0; i < empHaze.Length; i++)
                empHaze[i].transform.SetSiblingIndex(haze[i].transform.GetSiblingIndex() + 1);
            SetEmpHaze(0);
            UpdateParallax();
        }

        // 0 keeps the mission's own haze; 1 is fully EMP violet. Driven by the front that traces the skyline.
        internal void SetEmpHaze(float amount)
        {
            if (empHaze == null) return;
            amount = Mathf.Clamp01(amount);
            for (int i = 0; i < empHaze.Length; i++)
            {
                haze[i].color = new Color(1f, 1f, 1f, 1f - amount);
                empHaze[i].color = new Color(1f, 1f, 1f, amount);
            }
        }

        // The layers ease toward the player's side of the arena, so a Dodge or a hit does not jerk the scenery.
        const float ParallaxSeconds = .35f;
        float shownPosition, parallaxVelocity;
        bool parallaxPlaced;

        void UpdateParallax()
        {
            if (far == null || near == null) return;
            float target = Mathf.Clamp((player.position.x - arena.center.x) / arena.extents.x, -1f, 1f);
            if (!parallaxPlaced) { shownPosition = target; parallaxPlaced = true; }
            else if (Time.deltaTime > 0) shownPosition = Mathf.SmoothDamp(shownPosition, target, ref parallaxVelocity, ParallaxSeconds);
            float position = shownPosition;
            SetCrop(far, position * FarTravelPixels);
            SetCrop(near, position * NearTravelPixels);
            if (farLights != null) farLights.uvRect = far.uvRect;
            if (contourMaterial != null)
                contourMaterial.SetVector("_CityCrops", new Vector4(far.uvRect.x, far.uvRect.width,
                    near.uvRect.x, near.uvRect.width));
        }

        static void SetCrop(RawImage plate, float offsetPixels)
        {
            float width = plate.texture.width;
            float left = (width - VisibleWidthPixels) * .5f + offsetPixels;
            plate.uvRect = new Rect(left / width, 0f, VisibleWidthPixels / width, 1f);
        }

        void OnDestroy()
        {
            foreach (var texture in hazeTextures) if (texture != null) Destroy(texture);
            if (hazeMaterial != null) Destroy(hazeMaterial);
        }
    }
}
