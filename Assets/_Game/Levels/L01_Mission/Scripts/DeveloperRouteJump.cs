using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.InputSystem;
#endif

/// <summary>F8 tooling only. Release compilation contains no input handler, canvas or jump API.</summary>
public sealed class DeveloperRouteJump : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private GameSessionFlow flow;
    [SerializeField] private TMP_FontAsset font;
    private GameObject menu;
    private TMP_Text selection;
    private readonly Dictionary<LoadoutSlot, TMP_Text> slotLabels = new Dictionary<LoadoutSlot, TMP_Text>();
    public bool MenuVisible => menu != null && menu.activeSelf;
    public string SelectedNode { get; private set; }
    public int SelectedTier { get; private set; }
    public LoadoutDraft Choices { get; private set; }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) SetVisible(!MenuVisible);
        if (MenuVisible && flow.ActiveFight != null && !flow.ActiveFight.NarrativePaused
            && (flow.State == GameSessionFlow.FlowState.Practice || flow.State == GameSessionFlow.FlowState.Combat))
            flow.DevelopmentPauseMenu(true);
    }

    public void SetVisible(bool visible)
    {
        if (visible == MenuVisible || flow == null) return;
        if (visible)
        {
            // Deployment's fixed-step settling must complete before we pause it. Menu actions still replace it safely.
            SelectedNode = flow.DevelopmentCurrentNode;
            SelectedTier = Mathf.Clamp(flow.Deployment != null ? flow.Deployment.BrainPermissionTier : 0, 0, 3);
            Choices = new LoadoutDraft(flow.Deployment, true) { DevelopmentAllowLockedItems = true };
            if (menu == null) BuildMenu();
            flow.DevelopmentPauseMenu(true);
            menu.SetActive(true);
            RefreshLabels();
        }
        else
        {
            menu.SetActive(false);
            flow.DevelopmentPauseMenu(false);
        }
    }

    public void SelectNode(string stageId)
    {
        if (flow.DevelopmentRouteNodes.Contains(stageId)) { SelectedNode = stageId; RefreshLabels(); }
    }
    public void SelectTier(int tier) { SelectedTier = Mathf.Clamp(tier, 0, 3); RefreshLabels(); }
    public bool SelectUpgrade(LoadoutSlot slot, string id)
    {
        bool accepted = Choices != null && Choices.TrySetSlot(slot, id);
        RefreshLabels();
        return accepted;
    }
    public void JumpSelected() { SetVisible(false); flow.DevelopmentJump(SelectedNode, SelectedTier, Choices); }
    public void RestartCurrent() { var node = flow.DevelopmentCurrentNode; SetVisible(false); flow.DevelopmentJump(node, SelectedTier, Choices); }
    public void RestartFull() { SetVisible(false); flow.DevelopmentRestartFullRun(); }
    private void OnDisable() { if (MenuVisible) SetVisible(false); }

    private void CycleSlot(LoadoutSlot slot)
    {
        var options = slot == LoadoutSlot.Weapon ? LoadoutRules.OfferedWeapons.ToList()
            : new[] { "" }.Concat(LoadoutRules.PrototypeCatalog.Where(d => d.Occupies(slot)
                && LoadoutRules.IsOffered(slot, d.ItemId)).Select(d => d.ItemId).Distinct()).ToList();
        int current = options.IndexOf(Choices.GetSlot(slot).ItemId);
        for (int step = 1; step <= options.Count; step++)
            if (SelectUpgrade(slot, options[(current + step) % options.Count])) break;
    }

    private void RefreshLabels()
    {
        if (selection != null) selection.text = $"NODE: {SelectedNode}    PERMISSION: {(SelectedTier == 0 ? "0" : "T" + SelectedTier)}";
        foreach (var pair in slotLabels)
        {
            string id = Choices.GetSlot(pair.Key).ItemId;
            pair.Value.text = LoadoutRules.SlotLabel(pair.Key) + ": "
                + (pair.Key == LoadoutSlot.Weapon ? LoadoutRules.WeaponLabel(id) : LoadoutRules.ItemLabel(id));
        }
    }

    private void BuildMenu()
    {
        menu = new GameObject("DEV Route Jump", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        menu.transform.SetParent(transform, false);
        var canvas = menu.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
        var scaler = menu.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = 1f;
        var shade = Rect("Input blocker", menu.transform, 0, 0, 1280, 800);
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
        shade.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .94f);
        var panel = Rect("Controls", menu.transform, 0, 0, 1060, 740);
        Text(panel, "DEV ROUTE JUMP   [F8 close]", 0, 330, 1000, 42, 28);
        selection = Text(panel, "", 0, 280, 1000, 38, 21);
        Text(panel, "ROUTE (fresh run; skipped facts remain unknown)", -245, 225, 510, 32, 17);
        int i = 0;
        foreach (string node in flow.DevelopmentRouteNodes)
        {
            string id = node;
            Button(panel, id == "practice" ? "Practice Room" : id, -380 + (i % 2) * 250, 178 - (i / 2) * 50, 240, 42, () => SelectNode(id));
            i++;
        }
        Text(panel, "UPGRADES (click to cycle existing choices)", 285, 225, 490, 32, 17);
        for (int slot = 0; slot < LoadoutRules.SlotCount; slot++)
        {
            var value = (LoadoutSlot)slot;
            slotLabels[value] = Button(panel, "", 285, 180 - slot * 47, 470, 40, () => CycleSlot(value));
        }
        for (int tier = 0; tier < 4; tier++)
        {
            int value = tier;
            Button(panel, tier == 0 ? "0" : "T" + tier, -435 + tier * 124, -110, 114, 40, () => SelectTier(value));
        }
        Text(panel, "Apply upgrades with Jump / Restart Current. Full Run restores normal defaults.", 0, -192, 1030, 40, 18);
        Button(panel, "Jump to selected node", -350, -255, 320, 54, JumpSelected);
        Button(panel, "Restart Current Node", 0, -255, 320, 54, RestartCurrent);
        Button(panel, "Restart Full Run", 350, -255, 320, 54, RestartFull);
        Text(panel, "DEV runs do not save Archive records or unlocks. No branch facts are prefilled.", 0, -322, 1030, 35, 17);
    }

    private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private TMP_Text Text(Transform parent, string value, float x, float y, float width, float height, float size)
    {
        var text = Rect(value, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return text;
    }
    private TMP_Text Button(Transform parent, string label, float x, float y, float width, float height, Action action)
    {
        var rect = Rect(label, parent, x, y, width, height);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.12f, .12f, .12f);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
        button.onClick.AddListener(() => action());
        return Text(rect, label, 0, 0, width - 8, height - 4, 20);
    }
#endif
}
