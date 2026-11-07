using UnityEngine;
using UnityEngine.EventSystems;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform dialogueOverlay;
        TMPro.TMP_Text ambientDialogue, sceneDialogue;
        UnityEngine.UI.Image ambientPanel;
        const float FooterButtonHeight = 48;
        float dialogueFooterRightInset, dialogueFooterBottomInset;

        void UpdateDialogueOverlay()
        {
            if (sceneDialogue != null) sceneDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            bool show = game.PendingDialogue != null && !game.Paused && !game.DeveloperToolsOpen;
            if (!show && dialogueOverlay == null) return;
            if (dialogueOverlay == null)
            {
                var blocker = Image(canvas, Vector2.zero, Vector2.zero, Color.clear);
                blocker.raycastTarget = true;
                dialogueOverlay = blocker.rectTransform;
                dialogueOverlay.name = "Dialogue input";
                dialogueOverlay.anchorMin = Vector2.zero;
                dialogueOverlay.anchorMax = Vector2.one;
                blocker.gameObject.AddComponent<DialogueClickTarget>().Game = game;
                dialogueOverlay.gameObject.SetActive(false);
                ambientPanel = Image(dialogueOverlay, Vector2.zero, Vector2.zero, NeuralPalette.Panel);
                var panel = ambientPanel.rectTransform;
                panel.name = "Happy dialogue";
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = Vector2.right;
                var speaker = Text("Happy", panel, Vector2.zero, Vector2.zero, 18, TMPro.TextAlignmentOptions.MidlineLeft, headingFont);
                speaker.color = NeuralPalette.Secondary;
                var label = speaker.rectTransform;
                label.anchorMin = Vector2.up;
                label.anchorMax = Vector2.one;
                label.offsetMin = new Vector2(20, -32);
                label.offsetMax = new Vector2(-20, -8);
                ambientDialogue = Text("", panel, Vector2.zero, Vector2.zero, 22, TMPro.TextAlignmentOptions.MidlineLeft, DialogueFont());
                var body = ambientDialogue.rectTransform;
                body.anchorMin = Vector2.zero;
                body.anchorMax = Vector2.right;
                body.offsetMin = new Vector2(20, 0);
                body.offsetMax = new Vector2(-20, FooterButtonHeight);
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
                float right = game.Phase == DuelPhase.Configure ? dialogueFooterRightInset : 32;
                float bottom = game.Phase == DuelPhase.Configure ? dialogueFooterBottomInset : 32;
                ambientPanel.rectTransform.offsetMin = new Vector2(32, bottom);
                ambientPanel.rectTransform.offsetMax = new Vector2(-right, bottom + FooterButtonHeight + 36);
                ambientDialogue.text = game.PendingDialogue;
                ambientDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            }
        }

        void BuildFinalBoss()
        {
            bool offer = game.Phase == DuelPhase.BonusOffer;
            Text(offer ? "WAVE 3 CLEARED" : "WAVE 3",page,new Vector2(0,400),new Vector2(600,60),36,face:headingFont);
            Image(page,new Vector2(0,110),new Vector2(1040,190),NeuralPalette.Panel);
            sceneDialogue = Text(game.FinalBossDialogue,page,new Vector2(0,110),new Vector2(920,150),32,face:DialogueFont());
            sceneDialogue.maxVisibleCharacters = game.FinalBossChoiceReady ? int.MaxValue : game.DialogueVisibleCharacters;
            if (!offer)
            {
                Text(game.FinalBossDialogueSpeaker,page,new Vector2(0,177),new Vector2(600,42),24);
                return;
            }
            if (game.FinalBossChoiceReady)
            {
                Button("Yes",page,new Vector2(-110,-35),new Vector2(190,60),()=>game.ChooseBonus(true)).name="BonusYes";
                Button("No",page,new Vector2(110,-35),new Vector2(190,60),()=>game.ChooseBonus(false)).name="BonusNo";
            }
        }
    }

    public sealed class DialogueClickTarget : MonoBehaviour, IPointerDownHandler
    {
        internal DuelGame Game;

        public void OnPointerDown(PointerEventData pointer)
        {
            if (pointer.button == PointerEventData.InputButton.Left) Game.AcknowledgeDialogue();
        }
    }
}
