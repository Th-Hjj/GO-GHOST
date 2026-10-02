using System;
using TMPro;
using UnityEngine;

public sealed partial class MissionUI
{
    [Header("S01 narrative")]
    [SerializeField] private TextAsset narrativeText;
    [SerializeField] private TMP_FontAsset narrativeFont;
    private NarrativeContent narrative;
    private GameObject dialogue;
    private TMP_Text dialogueText;
    private UnityEngine.UI.Button dialogueAdvance;
    private GameObject previousUISelection;
    private string[] dialogueLines;
    private int dialogueIndex;
    private Action dialogueCompleted;
    public bool DialogueVisible => dialogue != null && dialogue.activeSelf;

    private void BuildDialogue(RectTransform canvas)
    {
        // Instance load on every Play entry; no static cache with Domain Reload disabled.
        try { narrative = new NarrativeContent(narrativeText); }
        catch (Exception error) { Debug.LogError("Narrative: " + error.Message, this); }
        dialogue = Screen(canvas, "NarrativeDialogue");
        dialogue.GetComponent<UnityEngine.UI.Image>().color = Color.clear; // blocks clicks behind the dialogue
        var panel = Panel(dialogue.transform, "DialoguePanel", new Vector2(960, 280), new Vector2(1440, 180));
        panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        dialogueAdvance = panel.gameObject.AddComponent<UnityEngine.UI.Button>();
        dialogueAdvance.transition = UnityEngine.UI.Selectable.Transition.None;
        dialogueAdvance.targetGraphic = panel.GetComponent<UnityEngine.UI.Image>();
        dialogueAdvance.onClick.AddListener(AdvanceDialogue);
        dialogueText = Text(panel, "DialogueLine", "", new Vector2(720, 90), new Vector2(1340, 140), 38);
        if (narrativeFont != null) dialogueText.font = narrativeFont;
        dialogueText.richText = false;
        dialogueText.enableAutoSizing = false;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogue.SetActive(false);
    }

    public bool ShowNarrative(string sequenceId, Action completed)
    {
        if (DialogueVisible || narrative == null) return false;
        try { dialogueLines = narrative.ApprovedSequence(sequenceId); }
        catch (Exception error) { Debug.LogError("Narrative: " + error.Message, this); return false; }
        dialogueIndex = 0;
        dialogueCompleted = completed;
        dialogueText.text = dialogueLines[0];
        dialogue.transform.SetAsLastSibling();
        dialogue.SetActive(true);
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null)
        {
            previousUISelection = events.currentSelectedGameObject;
            events.SetSelectedGameObject(dialogueAdvance.gameObject);
        }
        return true;
    }

    private void AdvanceDialogue()
    {
        if (!DialogueVisible) return;
        if (++dialogueIndex < dialogueLines.Length) { dialogueText.text = dialogueLines[dialogueIndex]; return; }
        var completed = dialogueCompleted;
        HideDialogue();
        completed?.Invoke();
    }

    public void HideDialogue()
    {
        dialogueCompleted = null;
        dialogueLines = null;
        if (dialogue != null) dialogue.SetActive(false);
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null && dialogueAdvance != null && events.currentSelectedGameObject == dialogueAdvance.gameObject)
            events.SetSelectedGameObject(previousUISelection);
        previousUISelection = null;
    }
}
