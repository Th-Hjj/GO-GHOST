using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class MissionUI
{
    [Header("Narrative content")]
    [SerializeField] private TextAsset narrativeText;
    [SerializeField] private TMP_FontAsset narrativeFont;
    // Explicit authorization for the supplied English review draft. Status stays draft in JSON.
    [SerializeField] private bool allowNarrativeDraft = true;
    private NarrativeContent narrative;
    private GameObject dialogue;
    private TMP_Text dialogueText, consequenceText, interactionKey;
    private RectTransform narrativeCanvas;
    private readonly TMP_Text[] choiceLabels = new TMP_Text[3];
    private string[] dialogueLines, choiceIds;
    private int dialogueIndex, selectedChoice, shownFrame, consumedFFrame = -1;
    private Action<DialogueResult, string> dialogueResult;
    private Func<string, string> choiceConsequence;
    private bool dialogueCanCancel;
    public enum DialogueResult { Completed, Cancelled, ChoiceCommitted }
    public bool DialogueVisible => dialogue != null && dialogue.activeSelf;
    public bool ChoicesVisible => DialogueVisible && choiceIds != null;
    public string CurrentNarrativeId { get; private set; }
    public string SelectedChoiceId => ChoicesVisible ? choiceIds[selectedChoice] : null;
    public string DisplayedNarrativeText => dialogueText != null ? dialogueText.text : null;
    public string DisplayedConsequence => consequenceText != null ? consequenceText.text : null;
    public bool InteractionKeyVisible => interactionKey != null && interactionKey.gameObject.activeSelf;

    private void BuildDialogue(RectTransform canvas)
    {
        narrativeCanvas = canvas;
        try { narrative = new NarrativeContent(narrativeText); }
        catch (Exception error) { Debug.LogError("Narrative: " + error.Message, this); }
        dialogue = Screen(canvas, "NarrativeDialogue");
        dialogue.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        var panel = Panel(dialogue.transform, "DialoguePanel", new Vector2(960, 260), new Vector2(1440, 290));
        panel.GetComponent<UnityEngine.UI.Image>().color = Color.black;
        dialogueText = Text(panel, "DialogueLine", "", new Vector2(720, 145), new Vector2(1340, 220), 38);
        // English uses the existing full Latin font; the old Chinese subset is not a Latin font source.
        dialogueText.color = Color.white;
        dialogueText.richText = false;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        for (int i = 0; i < choiceLabels.Length; i++)
        {
            choiceLabels[i] = Text(panel, "NarrativeChoice" + i, "", new Vector2(720, 210-i*65), new Vector2(1340, 62), 32);
            choiceLabels[i].richText = false;
        }
        consequenceText = Text(panel, "ChoiceConsequence", "", new Vector2(720, 20), new Vector2(1340, 35), 25);
        consequenceText.color = Color.white;
        interactionKey = Text(canvas, "InteractionKey", "", Vector2.zero, new Vector2(60, 60), 36);
        interactionKey.color = Color.white;
        interactionKey.raycastTarget = false;
        interactionKey.gameObject.SetActive(false);
        dialogue.SetActive(false);
    }

    public string NarrativeEntry(string id) => narrative.EntryText(id, allowNarrativeDraft);
    public string[] NarrativeSequence(string id) => narrative.SequenceText(id, allowNarrativeDraft);

    public bool ShowNarrative(string sequenceId, Action completed) => ShowNarrative(sequenceId,
        (result, _) => { if (result == DialogueResult.Completed) completed?.Invoke(); });

    public bool ShowNarrative(string sequenceId, Action<DialogueResult, string> result, bool cancellable = false)
    {
        if (DialogueVisible || narrative == null) return false;
        try { dialogueLines = NarrativeSequence(sequenceId); }
        catch (Exception error) { Debug.LogError("Narrative: " + error.Message, this); return false; }
        choiceIds = null;
        dialogueIndex = 0;
        dialogueText.text = dialogueLines[0];
        OpenDialogue(sequenceId, result, cancellable, true);
        return true;
    }

    public bool ShowChoices(string[] ids, Action<DialogueResult,string> result, bool replyOrAttack = false,
        Func<string,string> consequence = null)
    {
        if (DialogueVisible || ids == null || ids.Length == 0 || ids.Length > choiceLabels.Length || narrative == null) return false;
        try { foreach (string id in ids) NarrativeEntry(id); }
        catch (Exception error) { Debug.LogError("Narrative choice: " + error.Message, this); return false; }
        choiceIds = (string[])ids.Clone();
        choiceConsequence = consequence;
        selectedChoice = 0;
        dialogueText.text = "";
        OpenDialogue(ids[0], result, !replyOrAttack, !replyOrAttack);
        RefreshNarrativeChoices();
        return true;
    }

    private void OpenDialogue(string id, Action<DialogueResult,string> result, bool cancellable, bool blockPointer)
    {
        CurrentNarrativeId = id;
        dialogueResult = result;
        dialogueCanCancel = cancellable;
        shownFrame = Time.frameCount;
        dialogue.transform.SetAsLastSibling();
        dialogue.SetActive(true);
        foreach (var image in dialogue.GetComponentsInChildren<UnityEngine.UI.Image>(true)) image.raycastTarget = blockPointer;
        foreach (var label in choiceLabels) label.gameObject.SetActive(choiceIds != null);
        consequenceText.text = "";
        dialogueText.gameObject.SetActive(choiceIds == null);
        ShowInteractionKey(null);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    // The sole F consumer for both world focus and this presenter, independent of gameplay pause.
    public bool ConsumeInteractionPress()
    {
        if (consumedFFrame == Time.frameCount || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return false;
        consumedFFrame = Time.frameCount;
        return true;
    }

    private void TickDialogueInput()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DeveloperRouteJump>()?.MenuVisible == true) return;
#endif
        if (!DialogueVisible || Time.frameCount <= shownFrame || Keyboard.current == null) return;
        var keys = Keyboard.current;
        if (dialogueCanCancel && keys.escapeKey.wasPressedThisFrame) { HideDialogue(); return; }
        if (choiceIds != null)
        {
            int previousChoice = selectedChoice;
            if (keys.upArrowKey.wasPressedThisFrame) selectedChoice = (selectedChoice + choiceIds.Length - 1) % choiceIds.Length;
            if (keys.downArrowKey.wasPressedThisFrame) selectedChoice = (selectedChoice + 1) % choiceIds.Length;
            RefreshNarrativeChoices();
            if (previousChoice != selectedChoice) { shownFrame = Time.frameCount; ConsumeInteractionPress(); return; }
        }
        if (!ConsumeInteractionPress()) return;
        if (choiceIds != null) FinishDialogue(DialogueResult.ChoiceCommitted, SelectedChoiceId);
        else AdvanceDialogue();
    }

    private void RefreshNarrativeChoices()
    {
        for (int i = 0; i < choiceLabels.Length; i++)
        {
            choiceLabels[i].gameObject.SetActive(i < choiceIds.Length);
            if (i >= choiceIds.Length) continue;
            choiceLabels[i].text = NarrativeEntry(choiceIds[i]);
            choiceLabels[i].color = i == selectedChoice ? Color.white : new Color(.55f,.55f,.55f);
            choiceLabels[i].fontStyle = i == selectedChoice ? FontStyles.Underline : FontStyles.Normal;
        }
        string cue = choiceConsequence?.Invoke(SelectedChoiceId);
        consequenceText.text = cue == null ? "" : NarrativeEntry(cue);
    }

    private void AdvanceDialogue()
    {
        if (!DialogueVisible || choiceIds != null) return;
        if (++dialogueIndex < dialogueLines.Length) { dialogueText.text = dialogueLines[dialogueIndex]; return; }
        FinishDialogue(DialogueResult.Completed, CurrentNarrativeId);
    }
    private void FinishDialogue(DialogueResult result, string id)
    {
        var callback = dialogueResult;
        dialogueResult = null;
        dialogueLines = choiceIds = null;
        choiceConsequence = null;
        CurrentNarrativeId = null;
        if (dialogue != null) dialogue.SetActive(false);
        callback?.Invoke(result, id);
    }
    public void HideDialogue() { if (DialogueVisible) FinishDialogue(DialogueResult.Cancelled, CurrentNarrativeId); }

    public void ShowInteractionKey(Transform anchor)
    {
        if (interactionKey == null) return;
        bool visible = anchor != null && !DialogueVisible && Camera.main != null;
        interactionKey.gameObject.SetActive(visible);
        if (!visible) return;
        interactionKey.text = NarrativeEntry("ui.interact.key");
        Vector3 screen = Camera.main.WorldToScreenPoint(anchor.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(narrativeCanvas, screen, null, out var local);
        interactionKey.rectTransform.localPosition = local;
    }
}
