using UnityEngine;
using UnityEngine.EventSystems;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform dialogueOverlay;
        TMPro.TMP_Text ambientDialogue, clickHint;
        UnityEngine.UI.Image ambientPanel;

        void UpdateDialogueOverlay()
        {
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
                ambientPanel = Image(dialogueOverlay, new Vector2(0,185), new Vector2(950,180), NeuralPalette.Panel);
                ambientDialogue = Text("", ambientPanel.transform, Vector2.zero, new Vector2(900,160), 25);
                clickHint = Text("Click to continue", dialogueOverlay, new Vector2(0,-88), new Vector2(400,42), 22);
            }
            if (show && !dialogueOverlay.gameObject.activeSelf)
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            dialogueOverlay.gameObject.SetActive(show);
            if (!show) return;
            dialogueOverlay.SetAsLastSibling();
            bool ambient = game.Phase != DuelPhase.FinalBoss && game.Phase != DuelPhase.BonusOffer && !game.FinalBossCutscene;
            ambientPanel.gameObject.SetActive(ambient);
            if (ambient) ambientDialogue.text = game.PendingDialogue;
            clickHint.gameObject.SetActive(game.DialogueReady);
        }

        void BuildFinalBoss()
        {
            bool offer = game.Phase == DuelPhase.BonusOffer;
            Text(offer ? "WAVE 3 CLEARED" : "WAVE 3",page,new Vector2(0,400),new Vector2(600,60),36,face:headingFont);
            Image(page,new Vector2(0,110),new Vector2(1040,190),NeuralPalette.Panel);
            Text(game.FinalBossDialogue,page,new Vector2(0,110),new Vector2(920,150),32);
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
