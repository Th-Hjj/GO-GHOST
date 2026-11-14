using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Two cropped plates fill the fixed arena. Only their sampled region moves.
    sealed class IndustrialArenaBackdrop : MonoBehaviour
    {
        const float VisibleWidthPixels = 966f;
        const float FarTravelPixels = 7f;
        const float NearTravelPixels = 36f;
        const float IndustrialOpacity = .60f;
        const float CasinoOpacity = .60f;
        const float MegacityOpacity = .60f;

        Transform player;
        Bounds arena;
        RawImage far, near;
        Material contourMaterial;

        public static void Create(Transform attempt, Transform player, Bounds arena, int wave)
        {
            string artwork, farLabel, nearLabel;
            float opacity;
            switch (wave)
            {
                case 1:
                    (artwork, farLabel, nearLabel, opacity) = ("Industrial", "Distant Works", "Steel Columns", IndustrialOpacity);
                    break;
                case 2:
                    (artwork, farLabel, nearLabel, opacity) = ("Casino", "Resort Hotels", "Casino Entrance", CasinoOpacity);
                    break;
                case DuelGame.FinalBossWave:
                    (artwork, farLabel, nearLabel, opacity) = ("Megacity", "Distant Skyline", "Corporate Towers", MegacityOpacity);
                    break;
                default:
                    return;
            }
            var farTexture = Resources.Load<Texture2D>($"Arena/{artwork}Far");
            var nearTexture = Resources.Load<Texture2D>($"Arena/{artwork}Near");
            if (!farTexture || !nearTexture)
            {
                Debug.LogError($"Mission {wave} {artwork} background textures could not be loaded.");
                return;
            }

            var root = new GameObject($"{artwork} Arena Background", typeof(RectTransform), typeof(Canvas));
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
            backdrop.far = backdrop.AddPlate(farLabel, farTexture, opacity);
            backdrop.near = backdrop.AddPlate(nearLabel, nearTexture, opacity);
            backdrop.UpdateParallax();
        }

        RawImage AddPlate(string label, Texture2D texture, float opacity)
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

        void LateUpdate() => UpdateParallax();

        internal void AddEmpContours(Material material)
        {
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
            float target = Mathf.Clamp((player.position.x - arena.center.x) / arena.extents.x, -1f, 1f);
            if (!parallaxPlaced) { shownPosition = target; parallaxPlaced = true; }
            else if (Time.deltaTime > 0) shownPosition = Mathf.SmoothDamp(shownPosition, target, ref parallaxVelocity, ParallaxSeconds);
            float position = shownPosition;
            SetCrop(far, position * FarTravelPixels);
            SetCrop(near, position * NearTravelPixels);
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
    }
}
