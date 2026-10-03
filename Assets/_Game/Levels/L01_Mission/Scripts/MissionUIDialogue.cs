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
    private TMP_Text dialogueText, speakerText, consequenceText, interactionKey;
    private RectTransform narrativeCanvas, dialoguePanel;
    private readonly UnityEngine.UI.Image[] choiceRows = new UnityEngine.UI.Image[3];
    private PlayerInputReader dialogueInput;
    private bool dialogueBlocksWorld, mouseReady;
    private Vector2 previousDialoguePointer;
    private int hoveredChoice = -1;
    private const float DialogueFontSize = 38;
    private readonly TMP_Text[] choiceLabels = new TMP_Text[3];
    private NarrativeContent.Line[] dialogueLines;
    private string[] choiceIds;
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
    public string DisplayedSpeaker => speakerText != null ? speakerText.text : null;
    public string DisplayedConsequence => consequenceText != null ? consequenceText.text : null;
    public bool InteractionKeyVisible => interactionKey != null && interactionKey.gameObject.activeSelf;

    private void BuildDialogue(RectTransform canvas)
    {
        narrativeCanvas = canvas;
        try { narrative = new NarrativeContent(narrativeText); }
        catch (Exception error) { Debug.LogError("Narrative: " + error.Message, this); }
        dialogue = Screen(canvas, "NarrativeDialogue");
        dialogue.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        dialoguePanel = Panel(dialogue.transform, "DialoguePanel", new Vector2(960, 260), new Vector2(1440, 350));
        dialoguePanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .6f);
        speakerText = DialogueLabel(dialoguePanel, "SpeakerName", new Vector2(720, 310), new Vector2(1340, 46));
        dialogueText = DialogueLabel(dialoguePanel, "DialogueLine", new Vector2(720, 175), new Vector2(1340, 210));
        dialogueText.alignment = TextAlignmentOptions.Center;
        for (int i = 0; i < choiceLabels.Length; i++)
        {
            var row = Rect(dialoguePanel, "NarrativeChoiceRow" + i, new Vector2(720, 242-i*65), new Vector2(1360, 62));
            choiceRows[i] = row.gameObject.AddComponent<UnityEngine.UI.Image>();
            choiceRows[i].color = Color.clear;
            choiceLabels[i] = DialogueLabel(row, "NarrativeChoice" + i, new Vector2(680, 31), new Vector2(1340, 62));
        }
        consequenceText = DialogueLabel(dialoguePanel, "ChoiceConsequence", new Vector2(720, 40), new Vector2(1340, 46));
        interactionKey = Text(canvas, "InteractionKey", "", Vector2.zero, new Vector2(60, 60), 36);
        interactionKey.color = Color.white;
        interactionKey.raycastTarget = false;
        interactionKey.gameObject.SetActive(false);
        dialogue.SetActive(false);
    }

    private TMP_Text DialogueLabel(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var label = Text(parent, name, "", position, size, DialogueFontSize, TextAlignmentOptions.Left);
        // Keep the existing Latin font; no implicit markup, size changes or emphasis.
        label.fontStyle = FontStyles.Normal;
        label.enableAutoSizing = false;
        label.richText = false;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private void PresentLine()
    {
        dialogueText.text = dialogueLines[dialogueIndex].Text;
        // Reuse the baked CJK subset for authored Chinese lines; keep the normal Latin font elsewhere.
        dialogueText.font = !font.HasCharacters(dialogueText.text) && narrativeFont != null && narrativeFont.HasCharacters(dialogueText.text)
            ? narrativeFont : font;
        speakerText.text = dialogueLines[dialogueIndex].SpeakerName;
    }

    public string NarrativeEntry(string id) => narrative.EntryText(id, allowNarrativeDraft);
    public string[] NarrativeSequence(string id) => narrative.SequenceText(id, allowNarrativeDraft);

    public bool ShowNarrative(string sequenceId, Action completed) => ShowNarrative(sequenceId,
        (result, _) => { if (result == DialogueResult.Completed) completed?.Invoke(); });

    public bool ShowNarrative(string sequenceId, Action<DialogueResult, string> result, bool cancellable = false)
    {
        if (DialogueVisible || narrative == null) return false;
        try { dialogueLines = narrative.SequenceLines(sequenceId, allowNarrativeDraft); }
        catch (Exception error) { Debug.LogError("Narrative: " + error.Message, this); return false; }
        choiceIds = null;
        dialogueIndex = 0;
        PresentLine();
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
        dialogueBlocksWorld = blockPointer;
        mouseReady = Mouse.current == null || !Mouse.current.leftButton.isPressed;
        previousDialoguePointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        hoveredChoice = -1;
        dialogueInput = flow.ActiveFight != null ? flow.ActiveFight.Player.GetComponent<PlayerInputReader>() : null;
        if (dialogueInput != null) dialogueInput.UiBlocksManualFire = DialogueOwnsPointer;
        dialogue.transform.SetAsLastSibling();
        dialogue.SetActive(true);
        foreach (var image in dialogue.GetComponentsInChildren<UnityEngine.UI.Image>(true)) image.raycastTarget = blockPointer;
        dialoguePanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        foreach (var row in choiceRows) { row.raycastTarget = true; row.gameObject.SetActive(choiceIds != null); }
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

    private bool DialogueOwnsPointer()
    {
        if (!DialogueVisible) return false;
        return dialogueBlocksWorld || (Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(
            dialoguePanel, Mouse.current.position.ReadValue(), null));
    }

    private void TickDialogueInput()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DeveloperRouteJump>()?.MenuVisible == true) return;
#endif
        if (!DialogueVisible || Time.frameCount <= shownFrame) return;
        var keys = Keyboard.current;
        var mouse = Mouse.current;
        if (mouse != null && !mouse.leftButton.isPressed) mouseReady = true;
        bool click = mouse != null && mouseReady && mouse.leftButton.wasPressedThisFrame;
        if (dialogueCanCancel && keys != null && keys.escapeKey.wasPressedThisFrame) { HideDialogue(); return; }
        if (choiceIds != null)
        {
            int hit = -1;
            if (mouse != null)
            {
                Vector2 pointer = mouse.position.ReadValue();
                for (int i = 0; i < choiceIds.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)choiceRows[i].transform, pointer, null)) { hit = i; break; }
                // Stationary hover must not undo W/S navigation. A click always uses its actual row.
                if (hit >= 0 && (hit != hoveredChoice || pointer != previousDialoguePointer || click)) selectedChoice = hit;
                hoveredChoice = hit;
                previousDialoguePointer = pointer;
            }
            int previousChoice = selectedChoice;
            if (keys != null && keys.wKey.wasPressedThisFrame) selectedChoice = (selectedChoice + choiceIds.Length - 1) % choiceIds.Length;
            if (keys != null && keys.sKey.wasPressedThisFrame) selectedChoice = (selectedChoice + 1) % choiceIds.Length;
            RefreshNarrativeChoices();
            if (click && hit >= 0)
            {
                selectedChoice = hit;
                RefreshNarrativeChoices();
                ConsumeInteractionPress();
                FinishDialogue(DialogueResult.ChoiceCommitted, SelectedChoiceId);
                return;
            }
            if (previousChoice != selectedChoice) { ConsumeInteractionPress(); return; }
            if (ConsumeInteractionPress()) FinishDialogue(DialogueResult.ChoiceCommitted, SelectedChoiceId);
        }
        else if (ConsumeInteractionPress() || click)
        {
            mouseReady = false;
            shownFrame = Time.frameCount;
            AdvanceDialogue();
        }
    }

    private void RefreshNarrativeChoices()
    {
        for (int i = 0; i < choiceLabels.Length; i++)
        {
            choiceRows[i].gameObject.SetActive(i < choiceIds.Length);
            if (i >= choiceIds.Length) continue;
            choiceLabels[i].text = NarrativeEntry(choiceIds[i]);
            choiceRows[i].color = i == selectedChoice ? new Color(1, 1, 1, .18f) : Color.clear;
        }
        speakerText.text = narrative.EntryLine(SelectedChoiceId, allowNarrativeDraft).SpeakerName;
        string cue = choiceConsequence?.Invoke(SelectedChoiceId);
        consequenceText.text = cue == null ? "" : NarrativeEntry(cue);
    }

    private void AdvanceDialogue()
    {
        if (!DialogueVisible || choiceIds != null) return;
        if (++dialogueIndex < dialogueLines.Length) { PresentLine(); return; }
        FinishDialogue(DialogueResult.Completed, CurrentNarrativeId);
    }
    private void FinishDialogue(DialogueResult result, string id)
    {
        var callback = dialogueResult;
        dialogueResult = null;
        // A click held while the panel closes must never become a gameplay shot.
        if (dialogueInput != null)
        {
            if (DialogueOwnsPointer() && Mouse.current != null && Mouse.current.leftButton.isPressed)
                dialogueInput.SuppressUiFireUntilRelease();
            dialogueInput.UiBlocksManualFire = null;
        }
        dialogueInput = null;
        dialogueLines = null;
        choiceIds = null;
        choiceConsequence = null;
        CurrentNarrativeId = null;
        if (dialogue != null) dialogue.SetActive(false);
        callback?.Invoke(result, id);
    }
    public void HideDialogue() { if (DialogueVisible) FinishDialogue(DialogueResult.Cancelled, CurrentNarrativeId); }

    public void ShowInteractionKey(Transform anchor, Vector3 offset = default)
    {
        if (interactionKey == null) return;
        bool visible = anchor != null && !DialogueVisible && Camera.main != null;
        interactionKey.gameObject.SetActive(visible);
        if (!visible) return;
        interactionKey.text = NarrativeEntry("ui.interact.key");
        Vector3 screen = Camera.main.WorldToScreenPoint(anchor.position + offset);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(narrativeCanvas, screen, null, out var local);
        interactionKey.rectTransform.localPosition = local;
    }
}
