using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Two cropped plates fill the fixed arena. Only their sampled region moves.
    sealed class IndustrialArenaBackdrop : MonoBehaviour
    {
        const float VisibleWidthPixels = 966f;
        const float FarTravelPixels = 12f;
        const float NearTravelPixels = 64f;

        Transform player;
        Bounds arena;
        RawImage far, near;

        public static void Create(Transform attempt, Transform player, Bounds arena)
        {
            var farTexture = Resources.Load<Texture2D>("Arena/IndustrialFar");
            var nearTexture = Resources.Load<Texture2D>("Arena/IndustrialNear");
            if (!farTexture || !nearTexture)
            {
                Debug.LogError("Mission 1 industrial background textures could not be loaded.");
                return;
            }

            var root = new GameObject("Industrial Arena Background", typeof(RectTransform), typeof(Canvas));
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
            backdrop.far = backdrop.AddPlate("Distant Works", farTexture);
            backdrop.near = backdrop.AddPlate("Steel Columns", nearTexture);
            backdrop.UpdateParallax();
        }

        RawImage AddPlate(string label, Texture2D texture)
        {
            var plate = new GameObject(label, typeof(RawImage)).GetComponent<RawImage>();
            var rect = plate.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            plate.texture = texture;
            plate.raycastTarget = false;
            return plate;
        }

        void LateUpdate() => UpdateParallax();

        void UpdateParallax()
        {
            float position = Mathf.Clamp((player.position.x - arena.center.x) / arena.extents.x, -1f, 1f);
            SetCrop(far, position * FarTravelPixels);
            SetCrop(near, position * NearTravelPixels);
        }

        static void SetCrop(RawImage plate, float offsetPixels)
        {
            float width = plate.texture.width;
            float left = (width - VisibleWidthPixels) * .5f + offsetPixels;
            plate.uvRect = new Rect(left / width, 0f, VisibleWidthPixels / width, 1f);
        }
    }
}
