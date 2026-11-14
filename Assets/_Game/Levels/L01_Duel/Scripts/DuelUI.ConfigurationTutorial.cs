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
        float spotShade, spotOpen;

        bool TutorialOnPage => game.Phase == DuelPhase.Configure && !game.Paused && !game.DeveloperToolsOpen && !helpOpen;
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
                // Include that column's summary, categories, options and parameters.
                if ((focus is TutorialFocus.Condition or TutorialFocus.Action) && tutorialPreview != null)
                    bounds.yMax = TutorialBounds(tutorialPreview).yMax;
                Vector2 size = bounds.size + Vector2.one * 16;
                if (spotOpen <= 0) { spotCenter = bounds.center; spotSize = size; }
                float follow = 1 - Mathf.Exp(-step * 14);
                spotCenter = Vector2.Lerp(spotCenter, bounds.center, follow);
                spotSize = Vector2.Lerp(spotSize, size, follow);
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
            tutorialSpot.Set(spotCenter, spotSize * OvMotion.Snap(spotOpen), spotShade, spotOpen);
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
