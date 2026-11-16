using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform tutorialPreview, tutorialSubmit, tutorialExample;
        RectTransform tutorialProfile, tutorialTest, tutorialStart;
        RectTransform tutorialCondition, tutorialAction;
        readonly List<Button> tutorialEditorButtons = new List<Button>();
        readonly Vector3[] tutorialCorners = new Vector3[4];
        // Happy's pointer: one Signal frame that opens on what she is talking about and travels to the next thing.
        SpotlightGraphic tutorialSpot;
        Vector2 spotCenter, spotSize;
        float spotShade, spotOpen, spotLean;

        bool TutorialOnPage => game.Phase == DuelPhase.Configure && !game.Paused && !game.DeveloperToolsOpen;
        TutorialFocus VisibleTutorialFocus => TutorialOnPage ? game.ConfigurationVoice.Focus : TutorialFocus.None;
        // Lines that wait for the player to choose something instead of a click.
        bool TutorialChoiceOpen => VisibleTutorialFocus is TutorialFocus.Pick or TutorialFocus.Confirm or TutorialFocus.Condition or TutorialFocus.Action;
        // During the tutorial the shade does the dimming, so controls keep their own colours.
        internal bool TutorialHighlightsControl(Button button) => TutorialOnPage && game.ConfigurationVoice.TutorialRunning;

        void ClearTutorialView()
        {
            tutorialSpot = null;
            tutorialPreview = tutorialSubmit = tutorialExample = null;
            tutorialProfile = tutorialTest = tutorialStart = null;
            tutorialCondition = tutorialAction = null;
            tutorialEditorButtons.Clear();
        }

        void UpdateTutorialFocus()
        {
            var focus = VisibleTutorialFocus;
            var voice = game.ConfigurationVoice;
            // Nothing is pointed at until the line is actually heard.
            bool speaking = TutorialOnPage && voice.TutorialRunning && !voice.WaitingForPageSpeech && !wipePending;
            RectTransform target = !speaking ? null : focus switch
            {
                TutorialFocus.Body => bodyPreview,
                TutorialFocus.Rules => ruleList,
                TutorialFocus.Example => tutorialExample,
                TutorialFocus.AddRule when OnPreparationOverview => tutorialSubmit,
                TutorialFocus.Confirm => tutorialSubmit,
                TutorialFocus.Condition => tutorialCondition,
                TutorialFocus.Action => tutorialAction,
                TutorialFocus.Relation => tutorialPreview,
                TutorialFocus.TargetProfile => tutorialProfile,
                TutorialFocus.TestLoadout => tutorialTest,
                TutorialFocus.StartMission => tutorialStart,
                _ => null
            };
            if (page == null || (target != null && !target.IsChildOf(page))) target = null;
            float step = Time.unscaledDeltaTime;
            if (target != null)
            {
                TutorialOpening(target, focus, out Vector2 centre, out Vector2 size, out float lean);
                if (spotOpen <= 0) { spotCenter = centre; spotSize = size; spotLean = lean; }
                float follow = 1 - Mathf.Exp(-step * 14);
                spotCenter = Vector2.Lerp(spotCenter, centre, follow);
                spotSize = Vector2.Lerp(spotSize, size, follow);
                spotLean = Mathf.Lerp(spotLean, lean, follow);
            }
            spotOpen = Mathf.MoveTowards(spotOpen, target != null ? 1 : 0, step / .18f);
            spotShade = Mathf.MoveTowards(spotShade, speaking && DialogueVisible ? .48f : 0, step * .48f / .18f);
            if (page == null || (spotOpen <= 0 && spotShade <= 0))
            {
                if (tutorialSpot != null) tutorialSpot.gameObject.SetActive(false);
                return;
            }
            if (tutorialSpot == null)
            {
                tutorialSpot = Rect("Tutorial focus", page, Vector2.zero, Vector2.zero).gameObject.AddComponent<SpotlightGraphic>();
                tutorialSpot.raycastTarget = false;
            }
            tutorialSpot.gameObject.SetActive(true);
            tutorialSpot.transform.SetAsLastSibling();
            float opened = OvMotion.Snap(spotOpen);
            tutorialSpot.Set(spotCenter, spotSize * opened, spotShade, spotOpen, spotLean * opened);
        }

        void TutorialOpening(RectTransform target, TutorialFocus focus, out Vector2 centre, out Vector2 size, out float lean)
        {
            var button = target.GetComponent<SlabButton>();
            var slab = button != null ? button.Slab : target.GetComponent<SlabGraphic>();
            // Groups have upright edges: leaning a tall group reaches into the neighbouring column.
            // Individual controls follow their own cut, including the shallower Target Profile card.
            float slope = slab != null ? TutorialCut(slab) / Mathf.Max(1, slab.rectTransform.rect.height)
                : focus == TutorialFocus.Relation ? PickSlant : 0;
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            if (focus == TutorialFocus.Relation)
            {
                // These cards are siblings of the sentence's layout marker, not its children.
                TutorialContent(tutorialCondition, slope, ref min, ref max);
                TutorialContent(tutorialAction, slope, ref min, ref max);
            }
            else TutorialContent(target, slope, ref min, ref max);
            if (float.IsInfinity(min.x)) TutorialRect(target, target.rect, 0, slope, ref min, ref max);

            // Work in sheared coordinates so every corner is enclosed, including rotated limbs.
            // Padding includes the inward-drawn 4-unit ring and scales with the page's design frame.
            float scale = target.lossyScale.x / Mathf.Max(.0001f, page.lossyScale.x);
            float room = (focus is TutorialFocus.Body or TutorialFocus.Rules ? 20 : 14) * scale;
            // The rule rows have only ten units between them; the first row is also under the heading.
            // The profile and Test Room are twelve units apart. Their frames must stay in those gaps.
            float verticalRoom = (focus is TutorialFocus.AddRule or TutorialFocus.Example ? 6
                : focus is TutorialFocus.TargetProfile or TutorialFocus.TestLoadout ? 8
                : room / scale) * scale;
            min -= new Vector2(room * Mathf.Sqrt(1 + slope * slope), verticalRoom);
            max += new Vector2(room * Mathf.Sqrt(1 + slope * slope), verticalRoom);
            size = max - min;
            centre = (min + max) * .5f;
            centre.x += slope * centre.y;
            lean = size.y * slope;
        }

        static float TutorialCut(SlabGraphic slab) => Mathf.Min(slab.Cut >= 0 ? slab.Cut : Mathf.Round(slab.rectTransform.rect.height * .21f), slab.rectTransform.rect.width);

        void TutorialContent(RectTransform root, float slope, ref Vector2 min, ref Vector2 max)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return;
            var button = root.GetComponent<SlabButton>();
            if (button != null && button.Slab != null)
            {
                var face = button.Slab;
                TutorialRect(face.rectTransform, face.rectTransform.rect, TutorialCut(face), slope, ref min, ref max);
                return;
            }
            var graphic = root.GetComponent<Graphic>();
            if (graphic != null && graphic.enabled && graphic.color.a > 0)
            {
                if (graphic is SlabGraphic slab)
                {
                    TutorialRect(root, root.rect, TutorialCut(slab), slope, ref min, ref max);
                    return; // The slab already encloses its labels and icons.
                }
                if (graphic is TMPro.TMP_Text text)
                {
                    if (text.havePropertiesChanged) text.ForceMeshUpdate();
                    // TMP's line ascender includes empty space above the visible capitals.
                    // Use the rendered glyph quads so the Rules frame stays below the header stripe.
                    for (int i = 0; i < text.textInfo.characterCount; i++)
                    {
                        var character = text.textInfo.characterInfo[i];
                        if (!character.isVisible) continue;
                        TutorialPoint(root, character.vertex_BL.position, slope, ref min, ref max);
                        TutorialPoint(root, character.vertex_TL.position, slope, ref min, ref max);
                        TutorialPoint(root, character.vertex_TR.position, slope, ref min, ref max);
                        TutorialPoint(root, character.vertex_BR.position, slope, ref min, ref max);
                    }
                }
                else TutorialRect(root, root.rect, 0, slope, ref min, ref max);
            }
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i) is RectTransform child) TutorialContent(child, slope, ref min, ref max);
        }

        void TutorialRect(RectTransform root, Rect rect, float cut, float slope, ref Vector2 min, ref Vector2 max)
        {
            tutorialCorners[0] = new Vector2(rect.xMin, rect.yMin);
            tutorialCorners[1] = new Vector2(rect.xMin + cut, rect.yMax);
            tutorialCorners[2] = new Vector2(rect.xMax, rect.yMax);
            tutorialCorners[3] = new Vector2(rect.xMax - cut, rect.yMin);
            for (int i = 0; i < 4; i++)
                TutorialPoint(root, tutorialCorners[i], slope, ref min, ref max);
        }

        void TutorialPoint(RectTransform root, Vector3 local, float slope, ref Vector2 min, ref Vector2 max)
        {
            Vector2 point = page.InverseTransformPoint(root.TransformPoint(local));
            point.x -= slope * point.y;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
    }
}
