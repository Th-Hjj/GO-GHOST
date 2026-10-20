using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UPGRADE SCREEN (controller). Body abilities come straight from YOUR catalog
/// (LoadoutRules.PrototypeCatalog filtered by LoadoutRules.IsOffered); AI abilities come from AiAbilityCatalog.
///
/// RULES IT ENFORCES
///  - Level only goes UP. Abilities are only ADDED, never removed or swapped.
///  - A body slot that already holds a commitment can never be changed (LoadoutBridge.CanCommit).
///  - Open slots = granted by level - committed (see AssistRules). Never saved, always calculated.
///  - Autosave right after every commit, so dying and returning keeps everything.
///
/// FLOW
///  Start():           wires level buttons, refreshes header, shows AI list.
///  ChooseLevel():     confirm, raise aiLevel, save.
///  ShowAiAbilities(): "AI Abilities" tab button.
///  SelectPart():      called by BodyPartButton, shows that part's offered abilities.
///  RefreshList():     rebuilds the option list for the current view.
///  CommitBody/Ai():   confirm, add to the save, autosave.
///  RefreshBlueprint():glows the selected region and regions that hold a committed ability.
///  ReturnToBattle():  "Back to Battle" button.
/// </summary>
public class UpgradeScreen : MonoBehaviour
{
    [Header("Blueprint")]
    [SerializeField] BodyPartButton[] bodyButtons;

    [Header("Level UI")]
    [SerializeField] Button[] levelButtons;          // index 0 = Level 1
    [SerializeField] TMP_Text slotsText;

    [Header("Ability list UI")]
    [SerializeField] TMP_Text listTitleText;
    [SerializeField] Transform optionContainer;
    [SerializeField] AbilityOptionButton optionPrefab;

    [Header("Confirm popup")]
    [SerializeField] ConfirmPopup confirmPopup;

    SaveData Data => GameManager.Instance.Data;
    LoadoutSlot? selectedPart;
    bool showingAi;

    int OpenAiSlots   => AssistRules.TotalAiSlots(Data.aiLevel)   - Data.aiAbilityIds.Count;
    int OpenBodySlots => AssistRules.TotalBodySlots(Data.aiLevel) - Data.bodyCommits.Count;

    void Start()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int level = i + 1;
            levelButtons[i].onClick.AddListener(() => ChooseLevel(level));
        }
        RefreshHeader();
        ShowAiAbilities();
    }

    // ---------- Levels ----------

    void ChooseLevel(int level)
    {
        if (level <= Data.aiLevel) return;
        confirmPopup.Ask($"Raise AI assistance to Level {level}? This can't be undone.", () =>
        {
            Data.aiLevel = level;
            GameManager.Instance.Autosave();
            RefreshHeader();
            RefreshList();
        });
    }

    void RefreshHeader()
    {
        for (int i = 0; i < levelButtons.Length; i++)
            levelButtons[i].interactable = (i + 1) > Data.aiLevel;

        slotsText.text = $"AI slots open: {OpenAiSlots}   |   Body slots open: {OpenBodySlots}";
    }

    // ---------- Browsing ----------

    public void ShowAiAbilities()
    {
        showingAi = true;
        selectedPart = null;
        listTitleText.text = "AI Abilities";
        RefreshList();
    }

    public void SelectPart(LoadoutSlot slot)
    {
        if (slot == LoadoutSlot.Weapon) return;   // the blueprint has no weapon region
        showingAi = false;
        selectedPart = slot;
        listTitleText.text = PartLabel(slot);
        RefreshList();
    }

    static string PartLabel(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.Brain => "Head: Brain Abilities",
        LoadoutSlot.Torso => "Chest: Core Abilities",
        _ => LoadoutRules.SlotLabel(slot) + " Abilities"
    };

    void RefreshList()
    {
        foreach (Transform child in optionContainer) Destroy(child.gameObject);

        if (showingAi) BuildAiList();
        else if (selectedPart.HasValue) BuildBodyList(selectedPart.Value);

        RefreshBlueprint();
    }

    void BuildAiList()
    {
        foreach (var info in AiAbilityCatalog.All)
        {
            var captured = info;
            bool owned = Data.aiAbilityIds.Contains(info.Id);
            var btn = Instantiate(optionPrefab, optionContainer);
            btn.Setup(info.Label, info.Description, owned, canChoose: !owned && OpenAiSlots > 0, onClick: () => CommitAi(captured));
        }
    }

    void BuildBodyList(LoadoutSlot slot)
    {
        var draft = LoadoutBridge.BuildDraft(Data);
        string equippedHere = draft.GetSlot(slot).ItemId;
        var seen = new HashSet<string>();

        foreach (var def in LoadoutRules.PrototypeCatalog)
        {
            if (!def.Occupies(slot) || !LoadoutRules.IsOffered(slot, def.ItemId)) continue;
            if (!seen.Add(def.ItemId)) continue;

            string id = def.ItemId;
            bool owned = equippedHere == id;
            bool canChoose = !owned && OpenBodySlots > 0 && LoadoutBridge.CanCommit(Data, slot, id);

            var btn = Instantiate(optionPrefab, optionContainer);
            btn.Setup(LoadoutRules.ItemLabel(id), BodyAbilityText.Description(id), owned, canChoose, onClick: () => CommitBody(slot, id));
        }
    }

    void RefreshBlueprint()
    {
        var draft = LoadoutBridge.BuildDraft(Data);
        foreach (var b in bodyButtons)
        {
            bool hasCommitted = !string.IsNullOrEmpty(draft.GetSlot(b.slot).ItemId);
            b.SetState(selectedPart.HasValue && b.slot == selectedPart.Value, hasCommitted);
        }
    }

    // ---------- Committing ----------

    void CommitBody(LoadoutSlot slot, string itemId)
    {
        if (OpenBodySlots <= 0 || !LoadoutBridge.CanCommit(Data, slot, itemId)) return;   // safety net; UI already blocks
        confirmPopup.Ask($"Commit to \"{LoadoutRules.ItemLabel(itemId)}\" on {LoadoutRules.SlotLabel(slot)}? This is permanent.", () =>
        {
            Data.bodyCommits.Add(new BodyCommit { slot = (int)slot, itemId = itemId });
            GameManager.Instance.Autosave();
            RefreshHeader();
            RefreshList();
        });
    }

    void CommitAi(AiAbilityInfo info)
    {
        if (OpenAiSlots <= 0 || Data.aiAbilityIds.Contains(info.Id)) return;
        confirmPopup.Ask($"Commit to \"{info.Label}\"? This is permanent.", () =>
        {
            Data.aiAbilityIds.Add(info.Id);
            GameManager.Instance.Autosave();
            RefreshHeader();
            RefreshList();
        });
    }

    public void ReturnToBattle() => SceneManager.LoadScene(GameManager.BattleScene);
}