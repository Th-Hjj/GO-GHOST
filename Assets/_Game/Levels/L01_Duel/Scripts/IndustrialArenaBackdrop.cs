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
        // Plate opacities are Thomas's per-mission choices (2026-11-13/14).
        public readonly float FarOpacity, NearOpacity;

        ArenaLook(string artwork, string farLabel, string nearLabel, Color haze, Color rim, float farOpacity, float nearOpacity)
        {
            Artwork = artwork; FarLabel = farLabel; NearLabel = nearLabel; Haze = haze; Rim = rim;
            FarOpacity = farOpacity; NearOpacity = nearOpacity;
        }

        // Mission 3 starts cold white; the EMP dyes its haze violet. The Test Room and the Bonus have no plates.
        public static ArenaLook For(int wave) => wave switch
        {
            1 => new ArenaLook("Industrial", "Distant Works", "Steel Columns", NeuralPalette.Gunner,
                new Color32(255, 233, 219, 255), .65f, .65f),
            2 => new ArenaLook("Casino", "Resort Hotels", "Casino Entrance", NeuralPalette.Swordsman,
                new Color32(255, 226, 230, 255), .50f, .70f),
            DuelGame.FinalBossWave => new ArenaLook("Megacity", "Distant Skyline", "Corporate Towers", NeuralPalette.Primary,
                NeuralPalette.Primary, .50f, .50f),
            _ => new ArenaLook(null, null, null, NeuralPalette.Secondary, NeuralPalette.Primary, 0, 0)
        };
    }

    // The two authored plates fill the fixed arena as authored; only their sampled region moves.
    // One generated layer of haze lies along the floor between them. The art itself is never altered.
    sealed class IndustrialArenaBackdrop : MonoBehaviour
    {
        const float VisibleWidthPixels = 966f;
        const float FarTravelPixels = 7f;
        const float NearTravelPixels = 36f;
        // Thomas, 2026-11-14: only a layer that hugs the floor; the glow in mid-air looked like a stain.
        const float HazeHeight = .24f, HazeStrength = .20f;

        Transform player;
        Bounds arena;
        RawImage far, near, farLights;
        Material contourMaterial, hazeMaterial;
        readonly List<Texture2D> hazeTextures = new();
        // Floor haze in the mission colour, and a violet copy once the EMP sweep exists.
        RawImage haze, empHaze;

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
            if (plates) backdrop.far = backdrop.AddPlate(look.FarLabel, farTexture, look.FarOpacity);
            // Only the factory has this layer: the two stack beacons.
            var lights = plates ? Resources.Load<Texture2D>($"Arena/{look.Artwork}FarLights") : null;
            if (lights != null) backdrop.farLights = backdrop.AddPlate("Beacons", lights, 1f);
            backdrop.haze = backdrop.AddHaze("Floor haze", backdrop.FloorHaze(look.Haze));
            if (plates) backdrop.near = backdrop.AddPlate(look.NearLabel, nearTexture, look.NearOpacity);
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

        // Even across the arena, strongest at the floor and gone a short way up. Added as light.
        Texture2D FloorHaze(Color colour) => HazeTexture(4, 128, (u, depth) =>
        {
            float rise = Mathf.Clamp01((depth - (1f - HazeHeight)) / HazeHeight);
            return Linear(colour * (HazeStrength * rise * rise), 0);
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
            // A violet copy of the haze sits directly above the cold-white layer, hidden until the sweep dyes it.
            empHaze = AddHaze("EMP floor haze", FloorHaze(NeuralPalette.Emp));
            empHaze.transform.SetSiblingIndex(haze.transform.GetSiblingIndex() + 1);
            SetEmpHaze(0);
            UpdateParallax();
        }

        // 0 keeps the mission's own haze; 1 is fully EMP violet. Driven by the front that traces the skyline.
        internal void SetEmpHaze(float amount)
        {
            if (empHaze == null) return;
            amount = Mathf.Clamp01(amount);
            haze.color = new Color(1f, 1f, 1f, 1f - amount);
            empHaze.color = new Color(1f, 1f, 1f, amount);
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
