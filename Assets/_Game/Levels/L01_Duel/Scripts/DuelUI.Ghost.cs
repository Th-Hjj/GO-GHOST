using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // GHOST, the title's hidden Boss (Thomas, 2026-11-14). Stage 1: once the game has been finished he stands
    // on the T of the logo as a hologram, and the pointer takes the same look while it is over him.
    public sealed partial class DuelUI
    {
        void BuildTitleGhost(RectTransform frame, float delay)
        {
            if (!GameProgress.Cleared) return;
            // The middle of the T's bar on the 1600 x 900 frame, and the page's units per world unit.
            const float k = 56;
            var holder = Rect("GHOST", frame, new Vector2(165, 170), Vector2.zero);
            // The player's own body, pose and revolver, turned to face the menu.
            var figure = OvPlayerFigure(holder, new Vector2(0, .75f * k), k);
            figure.localScale = new Vector3(-1, 1, 1);
            holder.gameObject.AddComponent<GhostHologram>().Setup(figure, game.Appearance.CombatSprite, k, delay);
            var hit = Rect("Pointer area", holder, new Vector2(-.12f * k, .88f * k), new Vector2(1.15f * k, 1.9f * k)).gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            hit.gameObject.AddComponent<GhostPointer>().Clicked = game.EnterGhostArena;
        }
    }

    // Recolours a built player figure as GHOST and drives the hologram material.
    public sealed class GhostHologram : MonoBehaviour
    {
        public static readonly Color Body = new Color32(79, 134, 255, 255), Gun = new Color32(60, 104, 216, 255),
            Core = new Color32(228, 236, 255, 255), Light = new Color32(220, 232, 255, 255);
        // Trial tuning, in page units and seconds.
        const float RowPeriod = 2.5f, RowSpeed = 4.5f, BandSeconds = 3.4f, SlicePeriod = 2.9f, SliceShift = 5;

        Material body, slice;
        RectTransform figure, displaced;
        float k, from;

        public void Setup(RectTransform built, Sprite head, float unit, float delay)
        {
            figure = built; k = unit; from = Time.unscaledTime + delay;
            var shader = Resources.Load<Shader>("GhostHologram");
            body = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            slice = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            slice.SetFloat("_Mode", 1);
            foreach (var graphic in figure.GetComponentsInChildren<Graphic>(true))
            {
                Color colour = Body;
                if (graphic is Image image)
                {
                    if (image.sprite == head && head != null)
                    {
                        // GHOST has no face.
                        image.sprite = StickmanSprites.Disc;
                        image.rectTransform.sizeDelta /= StickmanSprites.DiscFill;
                    }
                    else if (image.sprite == RevolverPresentationGeometry.Body) colour = Gun;
                    else if (image.sprite == RevolverPresentationGeometry.Core) colour = Core;
                    else if (image.sprite == RevolverPresentationGeometry.Halo) colour = NeuralPalette.Alpha(Light, .35f);
                }
                graphic.color = colour; graphic.material = body; graphic.raycastTarget = false;
            }
            displaced = Instantiate(figure, figure.parent);
            displaced.name = "Displaced rows";
            displaced.anchoredPosition = figure.anchoredPosition + new Vector2(-SliceShift, 0);
            foreach (var graphic in displaced.GetComponentsInChildren<Graphic>(true)) graphic.material = slice;
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (body == null) return;
            float now = Time.unscaledTime, scale = Mathf.Max(1e-5f, transform.lossyScale.y);
            float fade = Mathf.Clamp01((now - from) / .3f);
            // A short run of rows across the chest jumps sideways for a moment.
            float beat = now % SlicePeriod / SlicePeriod;
            bool jump = !GameSettings.ReduceFlashing && fade >= 1 && beat > .91f && beat < .95f;
            var window = jump ? new Vector4(.80f * k, .98f * k, 0, 0) : new Vector4(1e6f, 1e6f, 0, 0);
            if (displaced.gameObject.activeSelf != jump) displaced.gameObject.SetActive(jump);
            float band = Mathf.Lerp(-.2f, 1.95f, now % BandSeconds / BandSeconds) * k;
            foreach (var material in new[] { body, slice })
            {
                material.SetFloat("_Feet", transform.position.y);
                material.SetFloat("_Scale", scale);
                material.SetFloat("_Clock", GameSettings.ReduceFlashing ? 0 : now * RowSpeed);
                material.SetFloat("_Period", RowPeriod);
                material.SetFloat("_Band", band);
                material.SetFloat("_BandHalf", .16f * k);
                material.SetFloat("_Fade", fade);
                material.SetVector("_Window", window);
                material.SetColor("_Light", Light);
            }
        }

        void OnDestroy()
        {
            if (body != null) Destroy(body);
            if (slice != null) Destroy(slice);
        }
    }

    // While the pointer is over GHOST it is drawn in his material: blue scan rows that rise, with a displaced run now and then.
    public sealed class GhostPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public System.Action Clicked;
        const int Size = 32, Frames = 6;
        static Texture2D[] frames;
        bool over;
        int shown = -1;

        public void OnPointerEnter(PointerEventData data) => over = true;
        public void OnPointerExit(PointerEventData data) => Release();
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            Release();
            Clicked?.Invoke();
        }
        void OnDisable() => Release();

        void Release()
        {
            if (!over && shown < 0) return;
            over = false; shown = -1;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void Update()
        {
            if (!over) return;
            int frame = GameSettings.ReduceFlashing ? 0 : (int)(Time.unscaledTime / .09f) % Frames;
            if (frame == shown) return;
            shown = frame;
            Cursor.SetCursor(Frame(frame), new Vector2(1, 1), CursorMode.Auto);
        }

        static Texture2D Frame(int index)
        {
            if (frames == null || frames[0] == null)
            {
                frames = new Texture2D[Frames];
                for (int i = 0; i < Frames; i++) frames[i] = Draw(i);
            }
            return frames[index];
        }

        // The ordinary arrow, in GHOST's blue. Rows are three texels: two lit, one faint; they climb one texel a frame.
        static Texture2D Draw(int frame)
        {
            Vector2[] arrow =
            {
                new Vector2(1, 1), new Vector2(1, 25), new Vector2(7.2f, 19.6f), new Vector2(11.3f, 29),
                new Vector2(15.4f, 27.3f), new Vector2(11.3f, 18.2f), new Vector2(18.8f, 18.2f),
            };
            var pixels = new Color32[Size * Size];
            Color lit = GhostHologram.Body, bright = GhostHologram.Light;
            for (int row = 0; row < Size; row++)
            {
                // One frame in six pushes a short run of rows sideways.
                int shift = frame == 4 && row >= 12 && row < 16 ? 2 : 0;
                bool faint = (row + frame) % 3 == 2;
                bool band = (row + frame * 5) % 30 < 3;
                for (int column = 0; column < Size; column++)
                {
                    float cover = 0;
                    for (int s = 0; s < 4; s++)
                        if (Inside(arrow, new Vector2(column + shift + .25f + .5f * (s & 1), row + .25f + .5f * (s >> 1)))) cover += .25f;
                    if (cover <= 0) continue;
                    Color colour = band ? bright : lit;
                    colour.a = cover * (faint ? .3f : 1);
                    // Texture rows run upward; the arrow is drawn from its tip down.
                    pixels[(Size - 1 - row) * Size + column] = colour;
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                { name = "GHOST pointer " + frame, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
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
