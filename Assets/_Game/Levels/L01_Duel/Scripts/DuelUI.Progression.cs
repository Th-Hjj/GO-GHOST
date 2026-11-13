using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform dialogueOverlay;
        readonly List<RectTransform> dialogueExits = new List<RectTransform>(3);
        CanvasGroup pageControls;
        internal bool DialogueVisible { get; private set; }
        TMPro.TMP_Text ambientSpeaker, ambientDialogue, sceneDialogue;
        UnityEngine.UI.Image ambientPanel;
        const float FooterButtonHeight = 48;
        float dialogueFooterRightInset, dialogueFooterBottomInset;
        (DuelPhase phase, bool editor, bool help, bool pause, bool tools)? lastDialoguePage;

        void NoteDialoguePage()
        {
            bool configure = game.Phase == DuelPhase.Configure;
            var current = (game.Phase, configure && selectorRow >= 0, configure && helpOpen,
                game.Paused, game.DeveloperToolsOpen);
            if (lastDialoguePage.HasValue && lastDialoguePage.Value == current) return;
            lastDialoguePage = current;
            game.CurrentVoice.PageEntered();
        }

        void UpdateDialogueOverlay()
        {
            if (sceneDialogue != null) sceneDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            bool show = game.PendingDialogue != null && !game.Paused && !game.DeveloperToolsOpen
                && !(game.Phase == DuelPhase.Configure && helpOpen);
            bool wasShowingStory = DialogueVisible;
            DialogueVisible = show;
            UpdateTutorialFocus();
            bool explainingEditor = VisibleTutorialFocus is TutorialFocus.Condition or TutorialFocus.Action or TutorialFocus.Relation;
            if (pageControls != null) pageControls.interactable = game.StartupProgress < 0
                && (!show || TutorialChoiceOpen || (!explainingEditor && game.DialogueAllowsPreparationNavigation));
            bool ruleEditorOpen = game.Phase == DuelPhase.Configure && selectorRow >= 0 && !helpOpen
                && !game.Paused && !game.DeveloperToolsOpen;
            if (!show && !ruleEditorOpen && dialogueOverlay == null) return;
            if (dialogueOverlay == null)
            {
                var blocker = Image(canvas, Vector2.zero, Vector2.zero, Color.clear);
                blocker.raycastTarget = true;
                dialogueOverlay = blocker.rectTransform;
                dialogueOverlay.name = "Dialogue input";
                dialogueOverlay.anchorMin = Vector2.zero;
                dialogueOverlay.anchorMax = Vector2.one;
                blocker.gameObject.AddComponent<DialogueClickTarget>().Owner = this;
                dialogueOverlay.gameObject.SetActive(false);
                BuildOverrideAmbientPanel();
            }
            if (show && !wasShowingStory)
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            dialogueOverlay.gameObject.SetActive(show || ruleEditorOpen);
            if (!show && !ruleEditorOpen) return;
            dialogueOverlay.SetAsLastSibling();
            bool ambient = !game.FinalBossCutscene && !game.InvasionHeld;
            ambientPanel.gameObject.SetActive(ambient && !game.CurrentVoice.WaitingForPageSpeech);
            if (ambient)
            {
                float width = canvas.rect.width;
                float right = game.Phase == DuelPhase.Configure ? dialogueFooterRightInset :
                    game.Phase == DuelPhase.TargetProfile ? width*.5f+24 : game.Phase == DuelPhase.Training ? width*.42f : 32;
                float bottom = game.Phase == DuelPhase.Configure ? dialogueFooterBottomInset : game.Phase == DuelPhase.TargetProfile ? 48 : 32;
                float left = game.Phase == DuelPhase.Configure ? dialogueFooterLeftInset : game.Phase == DuelPhase.TargetProfile ? 56 : 32;
                ambientPanel.rectTransform.offsetMin = new Vector2(left, bottom);
                ambientPanel.rectTransform.offsetMax = new Vector2(-right, bottom + 108);
                string description = ruleEditorOpen && hoveredRuleOption != null && hoveredRuleOption.isActiveAndEnabled
                    ? hoveredRuleOption.Description : null;
                bool showDescription = !show || (TutorialChoiceOpen && description != null);
                ambientDialogue.text = showDescription ? description ?? "" : game.PendingDialogue;
                ambientSpeaker.text = showDescription ? (description == null ? "" : game.HappySpeakerName) : game.HappySpeakerName;
                ambientDialogue.maxVisibleCharacters = showDescription ? int.MaxValue : game.DialogueVisibleCharacters;
            }
        }

        internal bool CaptureDialogueClick(Vector2 point, Camera eventCamera)
        {
            if (!DialogueVisible) return false;
            if (TutorialChoiceOpen)
                foreach (var button in tutorialEditorButtons)
                    if (button != null && button.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)button.transform, point, eventCamera)) return false;
            if (!game.DialogueAllowsPreparationNavigation) return true;
            if (game.Phase == DuelPhase.Configure && OnPreparationOverview && game.ConfigurationVoice.Focus == TutorialFocus.AddRule
                && tutorialSubmit != null && RectTransformUtility.RectangleContainsScreenPoint(tutorialSubmit, point, eventCamera)) return false;
            // Between spoken lines, a fresh press on an explicit exit can leave preparation.
            foreach (var exit in dialogueExits)
                if (RectTransformUtility.RectangleContainsScreenPoint(exit, point, eventCamera)) return false;
            return true;
        }

        internal void ClickDialogue() => game.AcknowledgeDialogue();

        RectTransform BuildSceneCaption(RectTransform parent,Vector2 position,string speaker,string line)
        {
            return OvSceneBox(parent,860,speaker,line,speaker==game.HappySpeakerName,"CutsceneText");
        }
    }

    public sealed class DialogueClickTarget : MonoBehaviour, IPointerDownHandler, ICanvasRaycastFilter
    {
        internal DuelUI Owner;
        public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera) => Owner.CaptureDialogueClick(point, eventCamera);

        public void OnPointerDown(PointerEventData pointer)
        {
            if (pointer.button != PointerEventData.InputButton.Left) return;
            pointer.Use();
            Owner.ClickDialogue();
        }
    }
}
