using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform playtestPrompt;
        CanvasGroup playtestBackground;
        GameObject playtestPreviousSelection;
        CharacterFaceCanvas playtestDrawing;

        public void ShowPlaytestPrompt()
        {
            playtestPreviousSelection = EventSystem.current?.currentSelectedGameObject;
            EventSystem.current?.SetSelectedGameObject(null);
            playtestBackground = page.GetComponent<CanvasGroup>();
            if (playtestBackground == null) playtestBackground = page.gameObject.AddComponent<CanvasGroup>();
            playtestBackground.interactable = playtestBackground.blocksRaycasts = false;
            playtestDrawing = page.GetComponentInChildren<CharacterFaceCanvas>();
            if (playtestDrawing != null) playtestDrawing.enabled = false;

            // A sibling overlay preserves the exact underlying page and incomplete rule draft.
            playtestPrompt = Rect("PlaytestPrompt", canvas, Vector2.zero, Vector2.zero);
            playtestPrompt.anchorMin = Vector2.zero;
            playtestPrompt.anchorMax = Vector2.one;
            var shade = playtestPrompt.gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, .88f);
            shade.raycastTarget = true;
            var panel = Image(playtestPrompt, Vector2.zero, new Vector2(820, 300), NeuralPalette.Panel).rectTransform;
            Text("The playtest is ending soon. Do you want to jump to wave 2?", panel,
                new Vector2(0, 45), new Vector2(740, 120), 30);
            var yes = Button("Yes", panel, new Vector2(-110, -90), new Vector2(180, 56), () => game.AnswerPlaytestPrompt(true));
            var no = Button("No", panel, new Vector2(110, -90), new Vector2(180, 56), () => game.AnswerPlaytestPrompt(false));
            yes.name = "PlaytestYes"; no.name = "PlaytestNo";
            // Keyboard navigation stays inside the modal; opening it never submits a choice.
            yes.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = no, selectOnRight = no, selectOnUp = no, selectOnDown = no };
            no.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yes, selectOnRight = yes, selectOnUp = yes, selectOnDown = yes };
            EventSystem.current?.SetSelectedGameObject(no.gameObject);
        }

        public void HidePlaytestPrompt(bool restoreSelection)
        {
            if (playtestPrompt != null)
            {
                playtestPrompt.gameObject.SetActive(false);
                Destroy(playtestPrompt.gameObject);
                playtestPrompt = null;
            }
            if (playtestBackground != null)
                playtestBackground.interactable = playtestBackground.blocksRaycasts = true;
            if (playtestDrawing != null) playtestDrawing.enabled = true;
            EventSystem.current?.SetSelectedGameObject(restoreSelection && playtestPreviousSelection != null
                && playtestPreviousSelection.activeInHierarchy ? playtestPreviousSelection : null);
            playtestPreviousSelection = null;
        }
    }
}
