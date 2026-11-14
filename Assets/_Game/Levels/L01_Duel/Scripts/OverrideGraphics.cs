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

        // A second colour entering from the left along the slab's own slant; a negative amount removes it.
        float sweep = -1;
        Color sweepColor;
        readonly Vector2[] swept = new Vector2[4];
        public void SetSweep(float amount, Color fill)
        {
            if (Mathf.Approximately(sweep, amount) && sweepColor == fill) return;
            sweep = amount; sweepColor = fill; SetVerticesDirty();
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
            if (sweep >= 0)
            {
                float reach = -cut + sweep * r.width;
                swept[0] = corners[0];
                swept[1] = corners[1];
                swept[2] = new Vector2(Mathf.Clamp(r.xMin + cut + reach, r.xMin + cut, r.xMax), r.yMax);
                swept[3] = new Vector2(Mathf.Clamp(r.xMin + reach, r.xMin, r.xMax - cut), r.yMin);
                if (swept[2].x - swept[1].x > .5f) OverrideMesh.Polygon(vh, swept, sweepColor, pixel);
            }
            if (frame > 0) OverrideMesh.Ring(vh, corners, frame, frameColor);
        }
    }

    // Override motion: shared timings and curves. Docs/Game/UIDesign.md lists the values.
    public static class OvMotion
    {
        public const float Stagger = .026f, EnterSeconds = .24f, LeaveSeconds = .12f, SharedSeconds = .32f;
        public const float WipeClose = .19f, WipeHold = .03f, WipeOpen = .26f;
        // Entrances created while a page is being built count from here, so they can wait for a wipe or an exit.
        public static double EntranceStart;
        // A page rebuilt while its entrance is under way continues that entrance instead of starting it again.
        public static bool Continue;
        public static double Born => Continue ? EntranceStart : System.Math.Max(Time.unscaledTimeAsDouble, EntranceStart);
        public static float Snap(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 4);
        public static float Glide(float t) { t = Mathf.Clamp01(t); return t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
    }

    // Slide-and-fade entrance for one page element, and its shorter exit. Also carries an element from an old place to a new one.
    public sealed class Arrive : MonoBehaviour
    {
        public Vector2 From;
        public float Seconds = OvMotion.EnterSeconds, Delay;
        public bool Fade = true, Glide;
        RectTransform rect;
        CanvasGroup group;
        Vector2 home, leaveTo;
        double born, leftAt = -1;
        bool done, leaveFade;

        // The first pose is applied in LateUpdate, after the builder has set From, Delay and Fade.
        void OnEnable() { rect = (RectTransform)transform; home = rect.anchoredPosition; born = OvMotion.Born; done = false; }
        void LateUpdate() => Apply();

        public void Leave() => LeaveTo(From.sqrMagnitude < 1 ? Vector2.zero : From.normalized * Mathf.Min(From.magnitude * .5f, 60), true);
        public void LeaveTo(Vector2 offset, bool fade)
        {
            if (leftAt >= 0) return;
            home = rect.anchoredPosition; leaveTo = offset; leaveFade = fade; leftAt = Time.unscaledTimeAsDouble;
        }

        void Apply()
        {
            double now = Time.unscaledTimeAsDouble;
            if (leftAt >= 0)
            {
                float out01 = Mathf.Clamp01((float)(now - leftAt) / OvMotion.LeaveSeconds);
                float gone = out01 * out01;
                rect.anchoredPosition = home + leaveTo * gone;
                if (leaveFade) Group().alpha = 1 - gone;
                return;
            }
            if (done) return;
            float t = Mathf.Clamp01((float)(now - born - Delay) / Mathf.Max(.01f, Seconds));
            float eased = Glide ? OvMotion.Glide(t) : OvMotion.Snap(t);
            rect.anchoredPosition = home + From * (1 - eased);
            if (Fade) Group().alpha = eased;
            done = t >= 1;
        }

        CanvasGroup Group()
        {
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            return group;
        }
    }

    // The page that is leaving: its elements step aside while the whole page fades, then it is destroyed.
    public sealed class PageLeave : MonoBehaviour
    {
        public float Seconds = .15f;
        // A wipe hides the swap itself, so the page under it stays as it was until it is removed.
        public bool Hold;
        CanvasGroup group;
        double born;

        void OnEnable() { born = Time.unscaledTimeAsDouble; TryGetComponent(out group); }
        void LateUpdate()
        {
            if (Hold) return;
            float t = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - born) / Seconds);
            if (group != null) group.alpha = 1 - t * t;
            if (t >= 1) Destroy(gameObject);
        }
    }

    // A slab that crosses the whole screen. The page changes while the screen is covered.
    public sealed class WipeOverlay : MonoBehaviour
    {
        public RectTransform Body;
        public GameObject[] Trim;
        public float Travel;
        // 1 moves left to right (forward), -1 right to left (back).
        public float Direction = 1;
        public System.Action Covered;
        CanvasGroup group;
        double born;
        bool fired;

        void OnEnable() => born = Time.unscaledTimeAsDouble;
        void LateUpdate() { transform.SetAsLastSibling(); if (Body != null) Apply(); }

        // Finish at once: used when another page change arrives before this one has ended.
        public void Cut() { Fire(); Destroy(gameObject); }
        void Fire() { if (fired) return; fired = true; var call = Covered; Covered = null; call?.Invoke(); }

        void Apply()
        {
            float t = (float)(Time.unscaledTimeAsDouble - born);
            float close = Mathf.Clamp01(t / OvMotion.WipeClose), open = Mathf.Clamp01((t - OvMotion.WipeClose - OvMotion.WipeHold) / OvMotion.WipeOpen);
            if (GameSettings.ReduceFlashing)
            {
                if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
                foreach (var part in Trim) if (part != null && part.activeSelf) part.SetActive(false);
                Body.anchoredPosition = Vector2.zero;
                group.alpha = close * (1 - open);
            }
            else
            {
                float x = open > 0 ? Travel * (1 - Mathf.Pow(1 - open, 3)) : -Travel * (1 - close * close);
                Body.anchoredPosition = new Vector2(x * Direction, 0);
            }
            if (close >= 1) Fire();
            if (open >= 1) Destroy(gameObject);
        }
    }

    // A four-cornered field that grows from one shape into another, such as a card opening into its page.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class QuadMorph : MaskableGraphic
    {
        public Vector2[] From, To;
        public float Seconds = .34f;
        public bool RemoveAtEnd;
        readonly Vector2[] corners = new Vector2[4];
        double born;
        bool settled;

        protected override void OnEnable() { base.OnEnable(); born = Time.unscaledTimeAsDouble; settled = false; }
        void Update()
        {
            if (settled) return;
            SetVerticesDirty();
            if (Time.unscaledTimeAsDouble - born < Seconds) return;
            settled = true;
            if (RemoveAtEnd) Destroy(gameObject);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (To == null || To.Length != 4) return;
            float eased = From == null ? 1 : OvMotion.Glide((float)(Time.unscaledTimeAsDouble - born) / Mathf.Max(.01f, Seconds));
            for (int i = 0; i < 4; i++) corners[i] = From == null ? To[i] : Vector2.LerpUnclamped(From[i], To[i], eased);
            float pixel = 1f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
            OverrideMesh.Polygon(vh, corners, color, pixel);
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
            float phase = SecondsPerPeriod > 0 && !GameSettings.ReduceFlashing ? (float)(Time.unscaledTimeAsDouble / SecondsPerPeriod % 1.0) : 0;
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
    public sealed class SlabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        internal DuelUI Owner;
        public Button Button;
        public RectTransform Face;
        public SlabGraphic Slab;
        public TMP_Text Label, Icon;
        public Color Fill, Ink;
        // Waiting colour for a control whose fill carries meaning, such as the target card.
        public Color? Dimmed;
        public float Shift = 12, EnterDelay = -1, EnterDistance = 90;
        // -1 enters from the left, 1 from the right.
        public float EnterSide = -1;
        // A parameter group that still needs a choice pulses a Signal inner frame.
        public bool Waiting;
        // A leaving page keeps its controls as they looked when the player left.
        public bool Frozen;
        // Hover arrives quickly and lets go slowly.
        const float HoverIn = .07f, HoverOut = .18f, PressSeconds = .05f, FlashSeconds = .18f, SweepSeconds = .17f;
        CanvasGroup group;
        bool hovered, focused, pressed, wasAvailable;
        float hover, press;
        double born, flashAt = -1, sweepAt = -1;
        Color sweepFrom, sweepInk;

        void OnEnable() { born = OvMotion.Born; hovered = focused = pressed = false; hover = press = 0; }
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = true;
            if (wasAvailable && !Frozen && Owner != null) Owner.PlayUiHover();
        }
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData data) => pressed = data.button == PointerEventData.InputButton.Left;
        public void OnPointerUp(PointerEventData data) => pressed = false;
        public void OnSelect(BaseEventData data) => focused = !(data is PointerEventData);
        public void OnDeselect(BaseEventData data) => focused = false;

        // The answer to a click: one bright step that falls back to the fill.
        public void Flash() => flashAt = Time.unscaledTimeAsDouble;

        // A newly chosen option: its colour enters from the left over the colour it had before.
        public void Sweep(Color from, Color ink) { sweepFrom = from; sweepInk = ink; sweepAt = Time.unscaledTimeAsDouble; }

        void LateUpdate()
        {
            if (Button == null || Face == null) return;
            if (group == null) group = Face.gameObject.AddComponent<CanvasGroup>();
            // A control the tutorial points at stays lit while the rest wait for the spoken line.
            bool available = Frozen ? wasAvailable : Owner != null && Owner.TutorialHighlightsControl(Button)
                ? Button.interactable : Button.IsInteractable() && !(Owner != null && Owner.DialogueVisible);
            wasAvailable = available;
            float target = available && !Frozen && (hovered || focused) ? 1 : 0;
            hover = Mathf.MoveTowards(hover, target, Time.unscaledDeltaTime / (target > hover ? HoverIn : HoverOut));
            press = Mathf.MoveTowards(press, available && !Frozen && pressed ? 1 : 0, Time.unscaledDeltaTime / PressSeconds);
            float eased = 1 - (1 - hover) * (1 - hover);
            float arrived = EnterDelay < 0 ? 1 : OvMotion.Snap((float)(Time.unscaledTimeAsDouble - born - EnterDelay) / OvMotion.EnterSeconds);
            Face.anchoredPosition = new Vector2(Shift * eased - 6 * press + EnterSide * (1 - arrived) * EnterDistance, 0);
            Face.localScale = Vector3.one * (1 - .015f * press);
            group.alpha = arrived;
            var fill = available ? Fill * (1 + .12f * eased) : Dimmed ?? OverridePalette.DisabledFill;
            float flash = flashAt < 0 || GameSettings.ReduceFlashing ? 0 : 1 - Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - flashAt) / FlashSeconds);
            if (available && flash > 0) fill = Color.Lerp(fill, Color.white, .7f * flash * flash);
            fill.a = 1;
            float swept = sweepAt < 0 ? 1 : OvMotion.Snap((float)(Time.unscaledTimeAsDouble - sweepAt) / SweepSeconds);
            if (available && swept < 1) { Slab.color = sweepFrom; Slab.SetSweep(swept, fill); }
            else { Slab.color = fill; Slab.SetSweep(-1, fill); }
            if (available && Waiting && !focused)
            {
                Color pulse = OverridePalette.Signal;
                pulse.a = .5f + .5f * Mathf.Cos((float)(Time.unscaledTimeAsDouble % 1.2 / 1.2) * Mathf.PI * 2);
                Slab.SetFrame(3, pulse);
            }
            else Slab.SetFrame(available && focused ? 4 : 0, Fill == OverridePalette.Text ? OverridePalette.Ink : OverridePalette.Text);
            var ink = !available ? OverridePalette.Disabled : swept < .5f ? sweepInk : Ink;
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
            if (GameSettings.ReduceFlashing)
            {
                if (First != null) First.anchoredPosition = Vector2.zero;
                if (Second != null) Second.anchoredPosition = Vector2.zero;
                if (Slice != null && Slice.gameObject.activeSelf) Slice.gameObject.SetActive(false);
                return;
            }
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

    // The Final Boss's mark: a flowing vertical rainbow on a slab-cut block.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RainbowBlock : MaskableGraphic
    {
        public float Cut = 22, Seconds = 4;
        void Update() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            const int steps = 12;
            float phase = (float)(Time.unscaledTimeAsDouble / Seconds % 1.0);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps, y = Mathf.Lerp(r.yMin, r.yMax, t), shift = Cut * t;
                Color32 tint = Color.HSVToRGB(Mathf.Repeat(phase + t, 1), .75f, 1);
                vh.AddVert(new Vector3(r.xMin + shift, y), tint, Vector2.zero);
                vh.AddVert(new Vector3(r.xMax - Cut + shift, y), tint, Vector2.zero);
                if (i == 0) continue;
                int n = i * 2;
                vh.AddTriangle(n - 2, n, n + 1);
                vh.AddTriangle(n - 2, n + 1, n - 1);
            }
        }
    }

    // The rule arrow keeps pushing toward the action once the condition is complete.
    public sealed class Drift : MonoBehaviour
    {
        public float Distance = 10, Period = .9f;
        RectTransform rect;
        Vector2 home;

        void OnEnable() { rect = (RectTransform)transform; home = rect.anchoredPosition; }
        void LateUpdate()
        {
            float phase = (float)(Time.unscaledTimeAsDouble % Period / Period);
            rect.anchoredPosition = home + Vector2.right * (Distance * Mathf.Sin(phase * Mathf.PI * 2));
        }
    }

    // Rule card hover: a small shift, a lighter slab and the reorder and remove tools.
    public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public SlabGraphic Slab;
        public CanvasGroup Tools;
        public RectTransform Move;
        public System.Func<bool> Busy;
        internal DuelUI Owner;
        bool inside;
        float amount;

        void OnEnable() { inside = false; amount = 0; Apply(); }
        public void OnPointerEnter(PointerEventData data) { inside = true; if (Owner != null) Owner.PlayUiHover(); }
        public void OnPointerExit(PointerEventData data) => inside = false;

        void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool focused = selected != null && Tools != null && selected.transform.IsChildOf(Tools.transform);
            float target = inside || focused ? 1 : 0;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / (target > amount ? .07f : .18f));
            Apply();
        }

        void Apply()
        {
            float eased = 1 - (1 - amount) * (1 - amount);
            if (Tools != null) { Tools.alpha = eased; Tools.blocksRaycasts = amount > .5f; }
            if (Slab != null) Slab.color = Color.Lerp(OverridePalette.Surface, new Color32(30, 30, 37, 255), eased);
            if (Move != null && (Busy == null || !Busy())) Move.anchoredPosition = new Vector2(12 * eased, 0);
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

    // The Upload scan: a bright line with a fading wake sweeps down and back up the head, then the head flashes once.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HeadScan : MaskableGraphic
    {
        public float Radius = 200, PassSeconds = 1.05f, Wake = 150, Line = 5;
        const float FlashSeconds = .3f;
        float clock, flashAt = -1;

        // Called when the upload is complete.
        public void Finish() { if (flashAt < 0) flashAt = clock; }

        void Update()
        {
            // A slow frame holds the line where it is instead of letting it jump.
            clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            SetVerticesDirty();
        }

        float Chord(float y) => Mathf.Sqrt(Mathf.Max(0, Radius * Radius - y * y));

        void Band(VertexHelper vh, float y0, float y1, Color32 c0, Color32 c1)
        {
            y0 = Mathf.Clamp(y0, -Radius, Radius); y1 = Mathf.Clamp(y1, -Radius, Radius);
            if (Mathf.Approximately(y0, y1)) return;
            float w0 = Chord(y0), w1 = Chord(y1);
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(-w0, y0), c0, Vector2.zero); vh.AddVert(new Vector3(w0, y0), c0, Vector2.zero);
            vh.AddVert(new Vector3(w1, y1), c1, Vector2.zero); vh.AddVert(new Vector3(-w1, y1), c1, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color tint = color;
            if (flashAt >= 0)
            {
                if (GameSettings.ReduceFlashing) return;
                float fade = 1 - Mathf.Clamp01((clock - flashAt) / FlashSeconds);
                tint.a = .55f * fade * fade;
                // Slices follow the angle so the top and bottom of the disc stay round.
                const int slices = 48;
                for (int s = 0; s < slices; s++)
                    Band(vh, -Radius * Mathf.Cos(Mathf.PI * s / slices), -Radius * Mathf.Cos(Mathf.PI * (s + 1) / slices), tint, tint);
                return;
            }
            float pass = clock / PassSeconds;
            bool down = Mathf.FloorToInt(pass) % 2 == 0;
            float p = Mathf.SmoothStep(0, 1, pass - Mathf.Floor(pass));
            float y = Mathf.Lerp(Radius, -Radius, down ? p : 1 - p);
            // The wake trails behind the line, strongest beside it.
            float behind = down ? 1 : -1;
            const int steps = 10;
            for (int s = 0; s < steps; s++)
            {
                float a0 = 1 - s / (float)steps, a1 = 1 - (s + 1) / (float)steps;
                Color c0 = tint, c1 = tint;
                c0.a = .34f * a0 * a0; c1.a = .34f * a1 * a1;
                Band(vh, y + behind * Wake * s / steps, y + behind * Wake * (s + 1) / steps, c0, c1);
            }
            Color core = Color.Lerp(tint, Color.white, .55f);
            Band(vh, y - Line / 2, y + Line / 2, core, core);
        }
    }

    // Shows where to draw: a ghost face is drawn stroke by stroke inside the head until the player makes a mark.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DrawGuide : MaskableGraphic
    {
        public CharacterAppearance Appearance;
        public float Thickness = 26;
        // The brush ring that travels with the strokes.
        public Color Brush = Color.white;
        const float Loop = 4.6f, FadeAt = 3.5f, FadeSeconds = .7f, BrushLeaves = 2.75f;
        static readonly float[] Begin = { .45f, 1.05f, 1.7f }, Seconds = { .32f, .32f, .8f };
        static readonly Vector2[][] Strokes = BuildStrokes();
        float clock;

        static Vector2[][] BuildStrokes()
        {
            var smile = new Vector2[17];
            for (int i = 0; i < smile.Length; i++)
            {
                float a = Mathf.Lerp(205, 335, i / (float)(smile.Length - 1)) * Mathf.Deg2Rad;
                smile[i] = new Vector2(Mathf.Cos(a) * 98, -14 + Mathf.Sin(a) * 98);
            }
            return new[]
            {
                new[] { new Vector2(-62, 70), new Vector2(-62, 18) },
                new[] { new Vector2(62, 70), new Vector2(62, 18) },
                smile
            };
        }

        protected override void OnEnable() { base.OnEnable(); clock = 0; }

        void Update()
        {
            clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            // After a Clear the demonstration starts again from its first stroke.
            if (Appearance != null && Appearance.HasInk) clock = 0;
            SetVerticesDirty();
        }

        static Vector2 Along(Vector2[] path, float part, System.Collections.Generic.List<Vector2> drawn)
        {
            float total = 0;
            for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);
            float left = total * Mathf.Clamp01(part);
            drawn?.Add(path[0]);
            for (int i = 1; i < path.Length; i++)
            {
                float length = Vector2.Distance(path[i - 1], path[i]);
                if (left < length)
                {
                    var tip = Vector2.Lerp(path[i - 1], path[i], length > 0 ? left / length : 0);
                    drawn?.Add(tip);
                    return tip;
                }
                left -= length;
                drawn?.Add(path[i]);
            }
            return path[path.Length - 1];
        }

        void Dot(VertexHelper vh, Vector2 at, float radius, Color32 c)
        {
            const int sides = 14;
            int centre = vh.currentVertCount;
            vh.AddVert(at, c, Vector2.zero);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                vh.AddVert(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, c, Vector2.zero);
                if (i > 0) vh.AddTriangle(centre, centre + i, centre + i + 1);
            }
        }

        void Segment(VertexHelper vh, Vector2 a, Vector2 b, float half, Color32 c)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < .0001f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * half;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        void Ring(VertexHelper vh, Vector2 at, float radius, float width, Color32 c)
        {
            const int sides = 28;
            int first = vh.currentVertCount;
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(at + dir * (radius - width), c, Vector2.zero);
                vh.AddVert(at + dir * radius, c, Vector2.zero);
                if (i == 0) continue;
                int v = first + i * 2;
                vh.AddTriangle(v - 2, v - 1, v + 1); vh.AddTriangle(v - 2, v + 1, v);
            }
        }

        readonly System.Collections.Generic.List<Vector2> drawn = new System.Collections.Generic.List<Vector2>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Appearance == null || Appearance.HasInk) return;
            float t = clock % Loop;
            // The head's interior is black, so fading to black is a fade-out without overlapping alpha.
            float fade = 1 - Mathf.Clamp01((t - FadeAt) / FadeSeconds);
            Color32 ink = Color.Lerp(Color.black, color, fade);
            float half = Thickness / 2;
            Vector2 brush = Strokes[0][0];
            for (int s = 0; s < Strokes.Length; s++)
            {
                float part = (t - Begin[s]) / Seconds[s];
                if (part <= 0)
                {
                    // Between strokes the brush travels to where the next one starts.
                    if (s > 0 && t > Begin[s - 1] + Seconds[s - 1])
                    {
                        float travel = Mathf.InverseLerp(Begin[s - 1] + Seconds[s - 1], Begin[s], t);
                        var from = Strokes[s - 1][Strokes[s - 1].Length - 1];
                        brush = Vector2.Lerp(from, Strokes[s][0], Mathf.SmoothStep(0, 1, travel));
                    }
                    break;
                }
                drawn.Clear();
                brush = Along(Strokes[s], part, drawn);
                for (int i = 0; i < drawn.Count; i++)
                {
                    Dot(vh, drawn[i], half, ink);
                    if (i > 0) Segment(vh, drawn[i - 1], drawn[i], half, ink);
                }
            }
            if (t < BrushLeaves)
            {
                Color ring = Brush;
                ring.a = Mathf.Clamp01(t / .25f) * Mathf.Clamp01((BrushLeaves - t) / .2f);
                Ring(vh, brush, half + 5, 3, ring);
            }
        }
    }

    // Scale-and-fade entrance for words and result titles.
    public sealed class SlamIn : MonoBehaviour
    {
        public float From = 1.5f, Seconds = .28f, Delay;
        CanvasGroup group;
        double born;

        void OnEnable() { born = OvMotion.Born; Apply(); }
        void LateUpdate() => Apply();

        // Restart the entrance so it begins this many seconds from now; a negative value is already under way.
        public void Trigger(float delay) { born = Time.unscaledTimeAsDouble; Delay = delay; Apply(); }

        void Apply()
        {
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            float t = Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - born - Delay) / Seconds);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            transform.localScale = Vector3.one * (GameSettings.ReduceFlashing ? 1 : Mathf.Lerp(From, 1, eased));
            group.alpha = eased;
        }
    }
}
