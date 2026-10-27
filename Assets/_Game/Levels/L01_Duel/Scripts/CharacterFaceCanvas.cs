using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Pointer capture belongs only to the drawing surface; text and buttons keep normal UI input.
    public sealed class CharacterFaceCanvas : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler, IPointerExitHandler
    {
        CharacterAppearance appearance;
        RectTransform area, brush;
        bool drawing, hasPrevious, cursorHidden, previousCursorVisible;
        Vector2 previous;
        Texture2D brushTexture;
        Sprite brushSprite;

        public void Initialize(CharacterAppearance value)
        {
            appearance = value;
            area = (RectTransform)transform;
            var cursor = new GameObject("Brush", typeof(RectTransform), typeof(Image));
            brush = cursor.GetComponent<RectTransform>();
            brush.SetParent(transform, false);
            brush.sizeDelta = Vector2.one * (CharacterAppearance.BrushRadius * 2 * area.rect.width / CharacterAppearance.Resolution + 4);
            brushTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            brushTexture.name = "Face Brush Cursor";
            var colors = new Color32[24 * 24];
            for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(11.5f, 11.5f));
                colors[y * 24 + x] = d >= 7 && d <= 10 ? (Color32)CharacterAppearance.Tint : d > 10 && d <= 11.5f ? (Color32)NeuralPalette.Primary : new Color32(0, 0, 0, 0);
            }
            brushTexture.SetPixels32(colors); brushTexture.Apply(false, true);
            brushSprite = Sprite.Create(brushTexture, new Rect(0, 0, 24, 24), Vector2.one * .5f);
            var image = cursor.GetComponent<Image>();
            image.sprite = brushSprite; image.raycastTarget = false;
            brush.gameObject.SetActive(false);
        }

        bool Point(Vector2 screen, Camera camera, out Vector2 uv)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, camera, out var local);
            uv = (local - area.rect.min) / area.rect.size;
            return CharacterAppearance.Contains(uv);
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !Application.isFocused) return;
            EventSystem.current?.SetSelectedGameObject(null);
            drawing = Point(data.position, data.pressEventCamera, out previous);
            hasPrevious = drawing;
            if (drawing) appearance.Draw(previous, previous);
        }
        public void OnDrag(PointerEventData data)
        {
            if (!drawing || data.button != PointerEventData.InputButton.Left) return;
            if (!Point(data.position, data.pressEventCamera, out var uv)) { hasPrevious = false; return; }
            appearance.Draw(hasPrevious ? previous : uv, uv);
            previous = uv; hasPrevious = true;
        }
        public void OnPointerUp(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) drawing = hasPrevious = false; }
        public void OnPointerExit(PointerEventData data) { hasPrevious = false; RestoreCursor(); }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused || !Point(mouse.position.ReadValue(), null, out var uv))
            { RestoreCursor(); return; }
            if (!mouse.leftButton.isPressed) drawing = hasPrevious = false;
            if (!cursorHidden) { previousCursorVisible = Cursor.visible; Cursor.visible = false; cursorHidden = true; }
            brush.gameObject.SetActive(true);
            brush.anchoredPosition = area.rect.min + uv * area.rect.size;
        }
        void RestoreCursor()
        {
            if (cursorHidden) Cursor.visible = previousCursorVisible;
            cursorHidden = false;
            if (brush != null) brush.gameObject.SetActive(false);
        }
        void OnApplicationFocus(bool focus) { if (!focus) { drawing = hasPrevious = false; RestoreCursor(); } }
        void OnDisable() { drawing = hasPrevious = false; RestoreCursor(); }
        void OnDestroy()
        {
            RestoreCursor();
            if (brushSprite != null) Destroy(brushSprite);
            if (brushTexture != null) Destroy(brushTexture);
        }
    }
}
