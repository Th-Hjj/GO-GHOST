using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform tutorialFrame, tutorialPreview, tutorialSubmit, tutorialExample;
        RectTransform tutorialProfile, tutorialTest, tutorialStart;
        readonly List<Button> tutorialEditorButtons = new List<Button>();
        readonly Vector3[] tutorialCorners = new Vector3[4];

        TutorialFocus VisibleTutorialFocus => game.Phase == DuelPhase.Configure && !game.Paused
            && !game.DeveloperToolsOpen && !helpOpen ? game.ConfigurationVoice.Focus : TutorialFocus.None;
        bool TutorialChoiceOpen => VisibleTutorialFocus is TutorialFocus.Pick or TutorialFocus.Confirm;
        internal bool TutorialHighlightsControl(Button button) => VisibleTutorialFocus switch
        {
            TutorialFocus.None => false,
            TutorialFocus.TargetProfile => button.transform == tutorialProfile,
            TutorialFocus.TestLoadout => button.transform == tutorialTest,
            TutorialFocus.StartMission => button.transform == tutorialStart,
            _ => true
        };

        void ClearTutorialView()
        {
            tutorialFrame = tutorialPreview = tutorialSubmit = tutorialExample = null;
            tutorialProfile = tutorialTest = tutorialStart = null;
            tutorialEditorButtons.Clear();
        }

        void UpdateTutorialFocus()
        {
            var focus = VisibleTutorialFocus;
            RectTransform target = focus switch
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
            if (page == null || target == null || !target.IsChildOf(page))
            {
                if (tutorialFrame != null) tutorialFrame.gameObject.SetActive(false);
                return;
            }

            var bounds = TutorialBounds(target);
            if (focus is TutorialFocus.Condition or TutorialFocus.Action)
            {
                // Include that column's summary, categories, options and parameters.
                bounds.yMax = TutorialBounds(tutorialPreview).yMax;
            }
            if (tutorialFrame == null) BuildTutorialFrame();
            tutorialFrame.gameObject.SetActive(true);
            tutorialFrame.anchoredPosition = bounds.center;
            tutorialFrame.sizeDelta = bounds.size + Vector2.one * 8;
        }

        Rect TutorialBounds(RectTransform target)
        {
            target.GetWorldCorners(tutorialCorners);
            Vector2 min = page.InverseTransformPoint(tutorialCorners[0]);
            Vector2 max = page.InverseTransformPoint(tutorialCorners[2]);
            return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        void BuildTutorialFrame()
        {
            tutorialFrame = Rect("Tutorial focus", page, Vector2.zero, Vector2.zero);
            var shade = new Color(0, 0, 0, .48f);
            for (int edge = 0; edge < 4; edge++)
            {
                bool horizontal = edge < 2;
                float side = edge % 2;
                var min = horizontal ? new Vector2(0, side) : new Vector2(side, 0);
                var max = horizontal ? new Vector2(1, side) : new Vector2(side, 1);
                var outside = Image(tutorialFrame, Vector2.zero, Vector2.zero, shade).rectTransform;
                outside.anchorMin = min;
                outside.anchorMax = max;
                outside.pivot = horizontal ? new Vector2(.5f, 1-side) : new Vector2(1-side, .5f);
                outside.sizeDelta = horizontal ? new Vector2(page.rect.width * 2, page.rect.height)
                    : new Vector2(page.rect.width, 0);

                var line = Image(tutorialFrame, Vector2.zero, Vector2.zero, NeuralPalette.Primary).rectTransform;
                line.anchorMin = min;
                line.anchorMax = max;
                line.pivot = horizontal ? new Vector2(.5f, side) : new Vector2(side, .5f);
                line.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
            }
        }
    }
}
