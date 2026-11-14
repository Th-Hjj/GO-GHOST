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
        public readonly float FarTint, NearTint, FarLights, NearLights;
        // An opaque far plate would hide the sky haze, so that haze is added in front of it.
        public readonly bool OpaqueSky;

        ArenaLook(string artwork, string farLabel, string nearLabel, Color haze, Color rim,
            float farTint, float nearTint, float farLights, float nearLights, bool opaqueSky)
        {
            Artwork = artwork; FarLabel = farLabel; NearLabel = nearLabel; Haze = haze; Rim = rim;
            FarTint = farTint; NearTint = nearTint; FarLights = farLights; NearLights = nearLights;
            OpaqueSky = opaqueSky;
        }

        // The Test Room and the Bonus have no plates: neutral haze only.
        public static ArenaLook For(int wave) => wave switch
        {
            1 => new ArenaLook("Industrial", "Distant Works", "Steel Columns", NeuralPalette.Gunner,
                new Color32(255, 233, 219, 255), .85f, .38f, .50f, 0, false),
            2 => new ArenaLook("Casino", "Resort Hotels", "Casino Entrance", NeuralPalette.Swordsman,
                new Color32(255, 226, 230, 255), .50f, .34f, .70f, .80f, false),
            DuelGame.FinalBossWave => new ArenaLook("Megacity", "Distant Skyline", "Corporate Towers", NeuralPalette.Emp,
                new Color32(234, 224, 255, 255), .55f, .30f, .60f, .50f, true),
            _ => new ArenaLook(null, null, null, NeuralPalette.Secondary, NeuralPalette.Primary, 0, 0, 0, 0, false)
        };
    }

    // Plates, their light layers and three haze layers fill the fixed arena. Only the sampled region of the art moves.
    sealed class IndustrialArenaBackdrop : MonoBehaviour
    {
        const float VisibleWidthPixels = 966f;
        const float FarTravelPixels = 7f;
        const float NearTravelPixels = 36f;

        Transform player;
        Bounds arena;
        RawImage far, near, farLights, nearLights;
        Material contourMaterial, hazeMaterial;
        readonly List<Texture2D> hazeTextures = new();

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
            if (!look.OpaqueSky) backdrop.AddHaze("Sky haze", backdrop.SkyHaze(look.Haze));
            if (plates) backdrop.far = backdrop.AddPlate(look.FarLabel, farTexture, look.FarTint);
            if (look.OpaqueSky) backdrop.AddHaze("Sky haze", backdrop.SkyHaze(look.Haze));
            if (plates) backdrop.farLights = backdrop.AddLights(look.Artwork + "FarLights", look.FarLights);
            backdrop.AddHaze("Mid haze", backdrop.MidHaze(look.Haze));
            if (plates) backdrop.near = backdrop.AddPlate(look.NearLabel, nearTexture, look.NearTint);
            if (plates) backdrop.nearLights = backdrop.AddLights(look.Artwork + "NearLights", look.NearLights);
            backdrop.AddHaze("Floor haze", backdrop.FrontHaze(look.Haze));
            backdrop.UpdateParallax();
        }

        // Tint darkens the plate into a silhouette; it stays opaque so the haze sits between the layers.
        RawImage AddPlate(string label, Texture texture, float tint)
        {
            var plate = new GameObject(label, typeof(RawImage)).GetComponent<RawImage>();
            var rect = plate.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            plate.texture = texture;
            plate.color = new Color(tint, tint, tint, 1f);
            plate.raycastTarget = false;
            return plate;
        }

        // Flat lights share their plate's crop and keep full brightness above the darkened plate.
        RawImage AddLights(string resource, float opacity)
        {
            if (opacity <= 0) return null;
            var texture = Resources.Load<Texture2D>("Arena/" + resource);
            if (texture == null) return null;
            var lights = AddPlate(resource, texture, 1f);
            lights.color = new Color(1f, 1f, 1f, opacity);
            return lights;
        }

        void AddHaze(string label, Texture2D texture)
        {
            var haze = AddPlate(label, texture, 1f);
            haze.material = hazeMaterial;
        }

        // Authored like the approved mockup in display space, then stored linear and premultiplied.
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

        // Dark sky that warms toward the floor, plus a glow rising from the horizon. Added as light.
        Texture2D SkyHaze(Color haze) => HazeTexture(256, 128, (u, depth) =>
        {
            Color top = new Color32(7, 7, 10, 255), upper = new Color32(10, 11, 16, 255);
            float low = Mathf.Clamp01((depth - .36f) / .64f);
            Color sky = depth < .36f ? Color.Lerp(top, upper, depth / .36f)
                : Color.Lerp(upper, Color.black, low) + haze * (.21f * Mathf.Pow(low, 1.5f));
            float dx = (u - .5f) / .58f, dy = (1f - depth) / .48f;
            float glow = .32f * Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            return Linear(Color.Lerp(sky, haze, glow), 0);
        });

        // Thin haze between the far and near plates.
        Texture2D MidHaze(Color haze) => HazeTexture(4, 128, (u, depth) =>
        {
            float amount = .11f * Mathf.Clamp01((depth - .42f) / .58f);
            return Linear(haze * amount, 1f - Mathf.Pow(1f - amount, 2.2f));
        });

        // Haze over the floor in front of the near plate, and a vignette that keeps the corners quiet.
        Texture2D FrontHaze(Color haze) => HazeTexture(256, 128, (u, depth) =>
        {
            float floor = .16f * Mathf.Clamp01((depth - .724f) / .276f);
            float dx = (u - .5f) / .75f, dy = (depth - .62f) / .85f;
            float vignette = .42f * Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .55f) / .45f);
            float kept = Mathf.Pow(1f - vignette, 2.2f);
            Color light = Linear(haze * floor, 0) * kept;
            light.a = 1f - Mathf.Pow(1f - floor, 2.2f) * kept;
            return light;
        });

        void LateUpdate() => UpdateParallax();

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
            UpdateParallax();
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
            if (nearLights != null) nearLights.uvRect = near.uvRect;
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
