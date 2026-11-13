using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI tokens. Docs/Game/UIDesign.md is the authority for every value here.
    public static class OverridePalette
    {
        public static readonly Color Ink = new Color32(8, 8, 10, 255);
        public static readonly Color Surface = new Color32(21, 21, 26, 255);
        public static readonly Color SurfaceHover = new Color32(34, 34, 42, 255);
        public static readonly Color Neutral = new Color32(42, 42, 49, 255);
        public static readonly Color Text = new Color32(244, 244, 240, 255);
        public static readonly Color TextDim = new Color32(180, 180, 188, 255);
        public static readonly Color Disabled = new Color32(111, 111, 120, 255);
        public static readonly Color DisabledFill = new Color32(26, 26, 32, 255);
        public static readonly Color Signal = new Color32(255, 46, 126, 255);
        public static readonly Color SignalText = new Color32(255, 92, 154, 255);
    }

    public enum OverrideButtonKind { Primary, Secondary, Tertiary }

    static class OverrideMesh
    {
        // Clockwise convex polygon with a feathered rim, so slanted edges stay smooth without MSAA.
        public static void Polygon(VertexHelper vh, IReadOnlyList<Vector2> points, Color32 color, float feather)
        {
            int start = vh.currentVertCount, count = points.Count;
            Color32 clear = color; clear.a = 0;
            for (int i = 0; i < count; i++) vh.AddVert(points[i], color, Vector2.zero);
            for (int i = 0; i < count; i++) vh.AddVert(points[i] + Outward(points, i) * feather, clear, Vector2.zero);
            for (int i = 1; i < count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(start + i, start + count + i, start + count + next);
                vh.AddTriangle(start + i, start + count + next, start + next);
            }
        }

        // A band of the given thickness just inside the polygon.
        public static void Ring(VertexHelper vh, IReadOnlyList<Vector2> points, float thickness, Color32 color)
        {
            int start = vh.currentVertCount, count = points.Count;
            for (int i = 0; i < count; i++) vh.AddVert(points[i], color, Vector2.zero);
            for (int i = 0; i < count; i++) vh.AddVert(points[i] - Outward(points, i) * thickness, color, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                vh.AddTriangle(start + i, start + count + next, start + count + i);
                vh.AddTriangle(start + i, start + next, start + count + next);
            }
        }

        static Vector2 Outward(IReadOnlyList<Vector2> points, int i)
        {
            int count = points.Count;
            Vector2 before = points[i] - points[(i + count - 1) % count], after = points[(i + 1) % count] - points[i];
            Vector2 a = new Vector2(-before.y, before.x).normalized, b = new Vector2(-after.y, after.x).normalized;
            Vector2 sum = a + b;
            // Mitre length keeps the rim an even width at sharp corners.
            float scale = 1f / Mathf.Max(.35f, Vector2.Dot(sum.normalized, a));
            return sum.normalized * scale;
        }
    }

    // Right-leaning parallelogram: the shape of every Override control and card.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlabGraphic : MaskableGraphic
    {
        public float Cut = -1;
        float frame;
        Color frameColor;
        readonly Vector2[] corners = new Vector2[4];

        public void SetFrame(float thickness, Color color)
        {
            if (Mathf.Approximately(frame, thickness) && frameColor == color) return;
            frame = thickness; frameColor = color; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float cut = Mathf.Min(Cut >= 0 ? Cut : Mathf.Round(r.height * .21f), r.width);
            corners[0] = new Vector2(r.xMin, r.yMin);
            corners[1] = new Vector2(r.xMin + cut, r.yMax);
            corners[2] = new Vector2(r.xMax, r.yMax);
            corners[3] = new Vector2(r.xMax - cut, r.yMin);
            float pixel = 1f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
            OverrideMesh.Polygon(vh, corners, color, pixel);
            if (frame > 0) OverrideMesh.Ring(vh, corners, frame, frameColor);
        }
    }

    // Scrolling hazard bands. The second colour may be transparent.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StripeGraphic : MaskableGraphic
    {
        public float Band = 17;
        public float Lean = .7f;
        public float SecondsPerPeriod = 1.4f;
        public Color Other = Color.clear;
        readonly List<Vector2> shape = new List<Vector2>(8), clipped = new List<Vector2>(8);

        void Update() { if (SecondsPerPeriod > 0) SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float period = Band * 2, slant = r.height * Lean;
            float phase = SecondsPerPeriod > 0 ? (float)(Time.unscaledTimeAsDouble / SecondsPerPeriod % 1.0) : 0;
            if (Other.a > 0) Quad(vh, r.xMin, r.xMax, r.xMin, r.xMax, r, Other);
            for (float x = r.xMin - slant - period + phase * period; x < r.xMax; x += period)
            {
                shape.Clear();
                shape.Add(new Vector2(x, r.yMin));
                shape.Add(new Vector2(x + slant, r.yMax));
                shape.Add(new Vector2(x + slant + Band, r.yMax));
                shape.Add(new Vector2(x + Band, r.yMin));
                Clip(shape, clipped, r.xMin, true);
                Clip(clipped, shape, r.xMax, false);
                if (shape.Count < 3) continue;
                int start = vh.currentVertCount;
                foreach (var point in shape) vh.AddVert(point, color, Vector2.zero);
                for (int i = 1; i < shape.Count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        static void Quad(VertexHelper vh, float bottomLeft, float bottomRight, float topLeft, float topRight, Rect r, Color32 fill)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector2(bottomLeft, r.yMin), fill, Vector2.zero);
            vh.AddVert(new Vector2(topLeft, r.yMax), fill, Vector2.zero);
            vh.AddVert(new Vector2(topRight, r.yMax), fill, Vector2.zero);
            vh.AddVert(new Vector2(bottomRight, r.yMin), fill, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

        // Keep the part of a polygon on one side of a vertical line.
        static void Clip(List<Vector2> source, List<Vector2> result, float edge, bool keepRight)
        {
            result.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 a = source[i], b = source[(i + 1) % source.Count];
                bool insideA = keepRight ? a.x >= edge : a.x <= edge, insideB = keepRight ? b.x >= edge : b.x <= edge;
                if (insideA) result.Add(a);
                if (insideA != insideB) result.Add(Vector2.Lerp(a, b, (edge - a.x) / (b.x - a.x)));
            }
        }
    }

    // Vertical two-colour fade.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FadeGraphic : MaskableGraphic
    {
        public Color Top = Color.black, Bottom = Color.clear;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            vh.AddVert(new Vector2(r.xMin, r.yMin), Bottom, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin, r.yMax), Top, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMax), Top, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax, r.yMin), Bottom, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }

    // Slab button behaviour: hover shift, focus frame, disabled colours and the slide-in.
    public sealed class SlabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public Button Button;
        public RectTransform Face;
        public SlabGraphic Slab;
        public TMP_Text Label, Icon;
        public Color Fill, Ink;
        public float Shift = 12, EnterDelay = -1;
        const float HoverSeconds = .1f, EnterSeconds = .22f, EnterDistance = 90;
        CanvasGroup group;
        bool hovered, focused;
        float hover;
        double born;

        void OnEnable() { born = Time.unscaledTimeAsDouble; hovered = focused = false; hover = 0; }
        public void OnPointerEnter(PointerEventData data) => hovered = true;
        public void OnPointerExit(PointerEventData data) => hovered = false;
        public void OnSelect(BaseEventData data) => focused = !(data is PointerEventData);
        public void OnDeselect(BaseEventData data) => focused = false;

        void LateUpdate()
        {
            if (Button == null || Face == null) return;
            if (group == null) group = Face.gameObject.AddComponent<CanvasGroup>();
            bool available = Button.IsInteractable();
            hover = Mathf.MoveTowards(hover, available && (hovered || focused) ? 1 : 0, Time.unscaledDeltaTime / HoverSeconds);
            float eased = 1 - (1 - hover) * (1 - hover);
            float arrived = EnterDelay < 0 ? 1 : Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - born - EnterDelay) / EnterSeconds);
            arrived = 1 - Mathf.Pow(1 - arrived, 3);
            Face.anchoredPosition = new Vector2(Shift * eased - (1 - arrived) * EnterDistance, 0);
            group.alpha = arrived;
            var fill = available ? Fill * (1 + .12f * eased) : OverridePalette.DisabledFill;
            fill.a = 1;
            Slab.color = fill;
            Slab.SetFrame(available && focused ? 4 : 0, Fill == OverridePalette.Text ? OverridePalette.Ink : OverridePalette.Text);
            var ink = available ? Ink : OverridePalette.Disabled;
            if (Label != null) Label.color = ink;
            if (Icon != null) Icon.color = ink;
        }
    }

    // Logo and result-word treatment: two offset colour copies and one displaced slice, in short bursts.
    public sealed class GlitchWord : MonoBehaviour
    {
        public RectTransform First, Second, Slice;
        public float Period = 3.4f, Spread = 7;

        void LateUpdate()
        {
            float p = (float)(Time.unscaledTimeAsDouble % Period / Period);
            bool burst = p >= .88f, late = p >= .92f;
            if (First != null) First.anchoredPosition = !burst ? new Vector2(-Spread, 0) : late ? new Vector2(Spread * .6f, 2) : new Vector2(-Spread * 3.1f, -3);
            if (Second != null) Second.anchoredPosition = !burst ? new Vector2(Spread, 0) : late ? new Vector2(-Spread * .7f, -2) : new Vector2(Spread * 3.4f, 3);
            if (Slice == null) return;
            bool visible = p >= .86f;
            if (Slice.gameObject.activeSelf != visible) Slice.gameObject.SetActive(visible);
            if (visible) Slice.anchoredPosition = new Vector2(p < .90f ? Spread * 5.4f : p < .94f ? -Spread * 3.7f : Spread * 1.7f, Slice.anchoredPosition.y);
        }
    }

    // Ring that fills clockwise from the top, with feathered edges.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RingGraphic : MaskableGraphic
    {
        public float Thickness = 16;
        float fill = 1;
        public float Fill
        {
            get => fill;
            set { value = Mathf.Clamp01(value); if (Mathf.Approximately(fill, value)) return; fill = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fill <= 0) return;
            var r = GetPixelAdjustedRect();
            float outer = Mathf.Min(r.width, r.height) / 2, inner = outer - Thickness;
            float pixel = 1f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
            Vector2 centre = r.center;
            Color32 solid = color, clear = color; clear.a = 0;
            const int steps = 120;
            int count = Mathf.CeilToInt(steps * fill);
            for (int i = 0; i <= count; i++)
            {
                float angle = Mathf.Min(i / (float)steps, fill) * Mathf.PI * 2;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(centre + direction * (outer + pixel), clear, Vector2.zero);
                vh.AddVert(centre + direction * outer, solid, Vector2.zero);
                vh.AddVert(centre + direction * inner, solid, Vector2.zero);
                vh.AddVert(centre + direction * (inner - pixel), clear, Vector2.zero);
                if (i == 0) continue;
                int k = i * 4;
                for (int band = 0; band < 3; band++)
                {
                    vh.AddTriangle(k - 4 + band, k + band, k + band + 1);
                    vh.AddTriangle(k - 4 + band, k + band + 1, k - 3 + band);
                }
            }
        }
    }

    // Leaning bars that follow a loudness value; they rest at a low height when silent.
    public sealed class VoiceBars : MonoBehaviour
    {
        public RectTransform[] Bars;
        public System.Func<float> Level;
        public float Idle = .22f, Gain = 7;
        float[] heights;

        void LateUpdate()
        {
            if (Bars == null) return;
            if (heights == null || heights.Length != Bars.Length)
            {
                heights = new float[Bars.Length];
                for (int i = 0; i < heights.Length; i++) heights[i] = Idle;
            }
            float level = Level != null ? Mathf.Clamp01(Level() * Gain) : 0;
            float time = (float)(Time.unscaledTimeAsDouble % 1000.0);
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 24);
            for (int i = 0; i < Bars.Length; i++)
            {
                float wobble = .55f + .45f * Mathf.Sin(time * (9 + i * 1.7f) + i * 2.1f);
                float target = level > .02f ? Mathf.Lerp(Idle, 1, Mathf.Clamp01(level * wobble * 1.5f)) : Idle;
                heights[i] = Mathf.Lerp(heights[i], target, blend);
                if (Bars[i] != null) Bars[i].localScale = new Vector3(1, heights[i], 1);
            }
        }
    }

    // A line sweeping down a circle, sized to the chord it crosses.
    public sealed class HeadScan : MonoBehaviour
    {
        public RectTransform Line;
        public float Radius = 200, Seconds = 1.5f;

        void LateUpdate()
        {
            if (Line == null) return;
            float p = (float)(Time.unscaledTimeAsDouble % Seconds / Seconds);
            float y = Mathf.Lerp(Radius, -Radius, p);
            Line.anchoredPosition = new Vector2(0, y);
            Line.sizeDelta = new Vector2(2 * Mathf.Sqrt(Mathf.Max(0, Radius * Radius - y * y)), 3);
        }
    }

    // Scale-and-fade entrance for words and result titles.
    public sealed class SlamIn : MonoBehaviour
    {
        public float From = 1.5f, Seconds = .28f, Delay;
        CanvasGroup group;
        double born;

        void OnEnable() { born = Time.unscaledTimeAsDouble; Apply(); }
        void LateUpdate() => Apply();

        // Restart the entrance so it begins this many seconds from now; a negative value is already under way.
        public void Trigger(float delay) { born = Time.unscaledTimeAsDouble; Delay = delay; Apply(); }

        void Apply()
        {
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            float t = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - born - Delay) / Seconds);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            transform.localScale = Vector3.one * Mathf.Lerp(From, 1, eased);
            group.alpha = eased;
        }
    }
}
