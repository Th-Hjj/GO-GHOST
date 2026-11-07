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
        TMPro.TMP_Text ambientSpeaker, ambientDialogue, sceneDialogue;
        UnityEngine.UI.Image ambientPanel;
        const float FooterButtonHeight = 48;
        float dialogueFooterRightInset, dialogueFooterBottomInset;

        void UpdateDialogueOverlay()
        {
            if (sceneDialogue != null) sceneDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            bool show = game.PendingDialogue != null && !game.Paused && !game.DeveloperToolsOpen;
            if (pageControls != null) pageControls.interactable = game.StartupProgress < 0 && (!show || game.OnPreparationPage && !game.DialogueSpeaking);
            if (!show && dialogueOverlay == null) return;
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
                ambientPanel = Image(dialogueOverlay, Vector2.zero, Vector2.zero, NeuralPalette.Panel);
                ambientPanel.raycastTarget = true;
                var panel = ambientPanel.rectTransform;
                panel.name = "Happy dialogue";
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.right;
                ambientSpeaker = Text("", panel, Vector2.zero, Vector2.zero, 18, TMPro.TextAlignmentOptions.MidlineLeft, headingFont);
                ambientSpeaker.color = NeuralPalette.Secondary;
                var label = ambientSpeaker.rectTransform;
                label.anchorMin = Vector2.up;
                label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(20, -32);
                label.offsetMax = new Vector2(-20, -8);
                ambientDialogue = Text("", panel, Vector2.zero, Vector2.zero, 22, TMPro.TextAlignmentOptions.TopLeft, DialogueFont());
                var body = ambientDialogue.rectTransform;
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.right;
                body.offsetMin = new Vector2(20, 8);
                body.offsetMax = new Vector2(-20, 68);
            }
            if (show && !dialogueOverlay.gameObject.activeSelf)
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            dialogueOverlay.gameObject.SetActive(show);
            if (!show) return;
            dialogueOverlay.SetAsLastSibling();
            bool ambient = game.Phase != DuelPhase.FinalBoss && game.Phase != DuelPhase.BonusOffer && !game.FinalBossCutscene;
            ambientPanel.gameObject.SetActive(ambient);
            if (ambient)
            {
                float width = canvas.rect.width;
                float right = game.Phase == DuelPhase.Configure ? dialogueFooterRightInset :
                    game.Phase == DuelPhase.TargetProfile ? width*.5f+32 : game.Phase == DuelPhase.Training ? width*.42f : 32;
                float bottom = game.Phase == DuelPhase.Configure ? dialogueFooterBottomInset : 32;
                ambientPanel.rectTransform.offsetMin = new Vector2(32, bottom);
                ambientPanel.rectTransform.offsetMax = new Vector2(-right, bottom + 104);
                ambientDialogue.text = game.PendingDialogue;
                ambientSpeaker.text = game.HappySpeakerName;
                ambientDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            }
        }

        internal bool CaptureDialogueClick(Vector2 point, Camera eventCamera)
        {
            if (!game.OnPreparationPage || game.DialogueSpeaking) return true;
            // Between spoken lines, a fresh press on an explicit exit can leave preparation.
            foreach (var exit in dialogueExits)
                if (RectTransformUtility.RectangleContainsScreenPoint(exit, point, eventCamera)) return false;
            return true;
        }

        internal void ClickDialogue() => game.AcknowledgeDialogue();

        RectTransform BuildSceneCaption(RectTransform parent,Vector2 position,string speaker,string line)
        {
            const float width=760, height=134, padding=24;
            var box=Image(parent,position,new Vector2(width,height),NeuralPalette.Alpha(NeuralPalette.Panel,.96f)).rectTransform;
            box.name="Scene dialogue";
            if(!string.IsNullOrEmpty(speaker))
                Text(speaker,box,new Vector2(0,42),new Vector2(width-2*padding,26),18,TMPro.TextAlignmentOptions.MidlineLeft,headingFont).color=NeuralPalette.Secondary;
            sceneDialogue=Text(line,box,new Vector2(0,string.IsNullOrEmpty(speaker)?14:-12),new Vector2(width-2*padding,76),26,TMPro.TextAlignmentOptions.TopLeft,DialogueFont());
            sceneDialogue.name="CutsceneText";
            sceneDialogue.maxVisibleCharacters=game.FinalBossChoiceReady?int.MaxValue:game.DialogueVisibleCharacters;
            return box;
        }

        void BuildBonusOffer()
        {
            Text("MISSION 3 CLEARED",page,new Vector2(0,400),new Vector2(600,60),36,face:headingFont);
            BuildSceneCaption(page,new Vector2(0,110),null,game.FinalBossDialogue);
            if (game.FinalBossChoiceReady)
            {
                Button("Yes",page,new Vector2(-110,-35),new Vector2(190,60),()=>game.ChooseBonus(true)).name="BonusYes";
                Button("No",page,new Vector2(110,-35),new Vector2(190,60),()=>game.ChooseBonus(false)).name="BonusNo";
            }
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
