using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform tutorialPreview, tutorialSubmit, tutorialExample;
        RectTransform tutorialProfile, tutorialTest, tutorialStart;
        readonly List<Button> tutorialEditorButtons = new List<Button>();
        readonly Vector3[] tutorialCorners = new Vector3[4];
        // Happy's pointer: one Signal frame that opens on what she is talking about and travels to the next thing.
        SpotlightGraphic tutorialSpot;
        Vector2 spotCenter, spotSize;
        float spotShade, spotOpen, spotLean;
        // Left edge of the Condition card and of the Action card on the editor's frame.
        Vector2 editorGuideX;

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
                TutorialFocus.Condition => ruleEditor,
                TutorialFocus.Action => ruleList,
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
                var bounds = TutorialBounds(target);
                Vector2 centre = bounds.center, size = bounds.size + Vector2.one * 16;
                // An ordinary control gets a small lean, stored as zero here.
                float lean = 0;
                // A side of the rule editor is one slanted figure, from its sentence card down to the foot of its list:
                // the frame follows the two guide lines the card and the list share, not their upright bounding box.
                if ((focus is TutorialFocus.Condition or TutorialFocus.Action) && tutorialPreview != null)
                {
                    const float top = 160, cardBottom = 268, bottom = 700, column = 520;
                    var frame = (RectTransform)tutorialPreview.parent;
                    float scale = frame.lossyScale.x / Mathf.Max(.0001f, page.lossyScale.x);
                    float cardLeft = focus == TutorialFocus.Condition ? editorGuideX.x : editorGuideX.y;
                    float height = bottom - top, slant = height * PickSlant;
                    float width = column - (cardBottom - top) * PickSlant, footLeft = cardLeft - (bottom - cardBottom) * PickSlant;
                    var middle = new Vector2(footLeft + (width + slant) / 2 - 800, 450 - (top + bottom) / 2);
                    centre = page.InverseTransformPoint(frame.TransformPoint(middle));
                    size = new Vector2(width, height) * scale + Vector2.one * 16;
                    lean = size.y * PickSlant;
                }
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
            // Below one unit of lean the frame is an ordinary control's, with the small default lean.
            float opened = OvMotion.Snap(spotOpen);
            tutorialSpot.Set(spotCenter, spotSize * opened, spotShade, spotOpen, spotLean > 1 ? spotLean * opened : -1);
        }

        Rect TutorialBounds(RectTransform target)
        {
            target.GetWorldCorners(tutorialCorners);
            Vector2 min = page.InverseTransformPoint(tutorialCorners[0]);
            Vector2 max = page.InverseTransformPoint(tutorialCorners[2]);
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
