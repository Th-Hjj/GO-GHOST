using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>The explicitly specified monochrome screens. GameSessionFlow owns every gameplay transition.</summary>
public sealed class MissionUI : MonoBehaviour
{
    [SerializeField] private GameSessionFlow flow;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private TMP_FontAsset font;
    // Manual data slots only. This pass does not create officer history.
    [Serializable] public sealed class OfficerRecord
    {
        public string officerNumber, age, rank, yearsOfService, missionsCompleted, missionSuccessRate, status;
        public string head, torso, leftArm, rightArm, leftLeg, rightLeg, spine;
        public string[] Fields => new[] { officerNumber, age, rank, yearsOfService, missionsCompleted, missionSuccessRate, status };
        public string[] Equipment => new[] { head, torso, leftArm, rightArm, leftLeg, rightLeg, spine };
    }
    [SerializeField] private OfficerRecord previousOfficer;
    [SerializeField] private OfficerRecord[] policeRecords = Array.Empty<OfficerRecord>();
    private static readonly string[] OfficerFields = { "OFFICER #", "AGE", "RANK", "YEARS OF SERVICE", "MISSIONS COMPLETED", "MISSION SUCCESS RATE", "STATUS" };
    private static readonly string[] Regions = { "HEAD", "TORSO", "LEFT ARM", "RIGHT ARM", "LEFT LEG", "RIGHT LEG", "SPINE" };
    private static readonly LoadoutSlot?[] Slots = { LoadoutSlot.Brain, LoadoutSlot.Torso, LoadoutSlot.LeftArm, LoadoutSlot.RightArm, LoadoutSlot.LeftLeg, LoadoutSlot.RightLeg, null };
    private GameObject main, upgrade, combat, result, archive;
    private RectTransform choices, archiveContent;
    private RectTransform bossFill, playerFill;
    private TMP_Text resultHeading, timeValue, bossValue, playerValue, accuracyValue;
    private GameSessionFlow.FlowState shown = (GameSessionFlow.FlowState)(-1);
    public int SelectedRegion { get; private set; } = -1;
    public int ArchiveTab { get; private set; }
    private readonly List<InputActionReference> uiReferences = new List<InputActionReference>();

    private void Awake()
    {
        var canvasObject = new GameObject("MissionCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        var root = (RectTransform)canvasObject.transform;
        BuildMain(root);
        BuildUpgrade(root);
        BuildCombat(root);
        BuildResult(root);
        BuildArchive(root);
        var events = new GameObject("MissionEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        events.transform.SetParent(transform, false);
        var module = events.GetComponent<InputSystemUIInputModule>();
        module.actionsAsset = inputActions;
        module.point = Action("UI/Point");
        module.leftClick = Action("UI/Click");
        module.move = Action("UI/Navigate");
        module.submit = Action("UI/Submit");
        module.cancel = Action("UI/Cancel");
        Refresh();
    }

    private InputActionReference Action(string path)
    {
        var reference = InputActionReference.Create(inputActions.FindAction(path, true));
        uiReferences.Add(reference);
        return reference;
    }

    private void BuildMain(RectTransform root)
    {
        main = Screen(root, "MainMenu");
        Text(main.transform, "Title", "GO GHOST", new Vector2(960, 980), new Vector2(640, 100), 64);
        Button(main.transform, "StartMission", "START MISSION", new Vector2(960, 570), new Vector2(500, 90), () => flow.RequestNewGame());
        Button(main.transform, "PoliceArchive", "POLICE ARCHIVE", new Vector2(960, 440), new Vector2(500, 90), () => flow.RequestArchive());
        var left = Panel(main.transform, "PreviousOfficer", new Vector2(295, 510), new Vector2(480, 780));
        RecordFields(left, OfficerFields, previousOfficer?.Fields, 700, 96);
        var right = Panel(main.transform, "AugmentationRecord", new Vector2(1625, 510), new Vector2(480, 780));
        RecordFields(right, Regions, previousOfficer?.Equipment, 700, 96);
    }

    private void BuildUpgrade(RectTransform root)
    {
        upgrade = Screen(root, "Upgrade");
        BodyRegion(0, new Vector2(960, 866), new Vector2(124, 140), true);
        BodyRegion(1, new Vector2(960, 628), new Vector2(240, 292));
        BodyRegion(2, new Vector2(782, 623), new Vector2(95, 300));
        BodyRegion(3, new Vector2(1138, 623), new Vector2(95, 300));
        BodyRegion(4, new Vector2(886, 315), new Vector2(100, 300));
        BodyRegion(5, new Vector2(1034, 315), new Vector2(100, 300));
        BodyRegion(6, new Vector2(960, 628), new Vector2(38, 252));
        choices = Panel(upgrade.transform, "UpgradeChoices", new Vector2(1565, 580), new Vector2(500, 450));
        choices.gameObject.SetActive(false);
        Button(upgrade.transform, "BeginOperation", "BEGIN OPERATION", new Vector2(1585, 78), new Vector2(520, 90), () => flow.RequestDeploy());
    }

    private void BodyRegion(int index, Vector2 position, Vector2 size, bool round = false)
    {
        var rect = Rect(upgrade.transform, "Region_" + Regions[index].Replace(' ', '_'), position, size);
        var graphic = rect.gameObject.AddComponent<MissionBodyRegion>();
        graphic.color = Color.white;
        graphic.Round = round;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = graphic;
        ConfigureButton(button);
        button.onClick.AddListener(() => SelectRegion(index));
    }

    private void SelectRegion(int index)
    {
        if (flow.State != GameSessionFlow.FlowState.Loadout) return;
        SelectedRegion = index;
        // Border objects stay; only the old option buttons are replaced.
        foreach (Transform child in choices)
            if (child.name.StartsWith("Choice_", StringComparison.Ordinal)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        choices.gameObject.SetActive(true);
        if (!Slots[index].HasValue) return;
        var slot = Slots[index].Value;
        int row = 0;
        foreach (var definition in LoadoutRules.PrototypeCatalog)
        {
            if (!definition.Occupies(slot)) continue;
            if (definition.ItemId == LoadoutRules.DetachedDroneArm && !PoliceArchive.Current.HasHiddenAugmentation(definition.ItemId)) continue;
            string id = definition.ItemId;
            Button(choices, "Choice_" + id, LoadoutRules.ItemLabel(id), new Vector2(250, 374 - row * 90), new Vector2(450, 72), () => flow.TrySetDraftSlot(slot, id));
            row++;
        }
    }

    private void BuildCombat(RectTransform root)
    {
        combat = Rect(root, "CombatHUD", Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)combat.transform);
        bossFill = HealthBar(combat.transform, "BossHealth", new Vector2(960, 970), new Vector2(1200, 28));
        playerFill = HealthBar(combat.transform, "PlayerHealth", new Vector2(960, 28), new Vector2(1800, 28));
        var top = (RectTransform)bossFill.parent;
        top.anchorMin = top.anchorMax = new Vector2(.5f, 1);
        top.anchoredPosition = new Vector2(0, -50);
        var bottom = (RectTransform)playerFill.parent;
        bottom.anchorMin = bottom.anchorMax = new Vector2(.5f, 0);
        bottom.anchoredPosition = new Vector2(0, 28);
    }

    private void BuildResult(RectTransform root)
    {
        result = Screen(root, "MissionResult");
        resultHeading = Text(result.transform, "ResultHeading", "", new Vector2(960, 890), new Vector2(1280, 120), 64);
        string[] labels = { "TIME", "BOSS HP", "PLAYER HP", "ACCURACY" };
        var values = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            Text(result.transform, "StatisticLabel" + i, labels[i], new Vector2(760, 660 - i * 110), new Vector2(390, 70), 32, TextAlignmentOptions.Left);
            values[i] = Text(result.transform, "StatisticValue" + i, "", new Vector2(1190, 660 - i * 110), new Vector2(360, 70), 32, TextAlignmentOptions.Right);
        }
        timeValue = values[0]; bossValue = values[1]; playerValue = values[2]; accuracyValue = values[3];
        Button(result.transform, "ResultContinue", "CONTINUE", new Vector2(960, 100), new Vector2(420, 90), () => flow.RequestContinue());
    }

    private void BuildArchive(RectTransform root)
    {
        archive = Screen(root, "Archive");
        string[] tabs = { "POLICE RECORDS", "MISSION INTELLIGENCE", "CASE CLOSURE RECORDS" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            Button(archive.transform, "ArchiveTab" + i, tabs[i], new Vector2(380 + i * 580, 970), new Vector2(560, 90), () => SelectArchiveTab(index));
        }
        archiveContent = Panel(archive.transform, "ArchiveContent", new Vector2(960, 470), new Vector2(1760, 820));
    }

    private void SelectArchiveTab(int tab)
    {
        ArchiveTab = tab;
        foreach (Transform child in archiveContent)
            if (child.name.StartsWith("Content_", StringComparison.Ordinal)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        if (tab == 0)
        {
            // Only supplied actual records can populate this. No record generation or fake entries.
            for (int i = 0; i < policeRecords.Length; i++)
            {
                var record = policeRecords[i];
                if (record == null) continue;
                var item = Panel(archiveContent, "Content_Officer" + i, new Vector2(440 + 880 * i, 410), new Vector2(840, 760));
                RecordFields(item, OfficerFields, record.Fields, 705, 49);
                RecordFields(item, Regions, record.Equipment, 347, 46);
            }
        }
        else if (tab == 1)
        {
            Text(archiveContent, "Content_Achievement", PoliceArchive.TempFirstVictory, new Vector2(880, 690), new Vector2(1500, 70), 32);
            Text(archiveContent, "Content_Condition", "Defeat the Boss once.", new Vector2(880, 590), new Vector2(1500, 70), 30);
            Text(archiveContent, "Content_Completion", PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory) ? "COMPLETED" : "INCOMPLETE", new Vector2(880, 490), new Vector2(1500, 70), 30);
        }
        // Case closure has no authored content. It stays empty.
    }

    private void Update()
    {
        if (shown != flow.State) Refresh();
        if (flow.State == GameSessionFlow.FlowState.Combat && flow.ActiveFight != null)
        {
            Fill(bossFill, (float)flow.ActiveFight.BossHealth.CurrentHealth / flow.ActiveFight.BossHealth.MaxHealth);
            Fill(playerFill, (float)flow.ActiveFight.Player.CurrentHealth / flow.ActiveFight.Player.MaxHealth);
        }
    }

    private void Refresh()
    {
        shown = flow.State;
        main.SetActive(shown == GameSessionFlow.FlowState.MainMenu);
        upgrade.SetActive(shown == GameSessionFlow.FlowState.Loadout);
        combat.SetActive(shown == GameSessionFlow.FlowState.Combat);
        result.SetActive(shown == GameSessionFlow.FlowState.Result);
        archive.SetActive(shown == GameSessionFlow.FlowState.Archive);
        if (shown == GameSessionFlow.FlowState.Loadout) { SelectedRegion = -1; choices.gameObject.SetActive(false); }
        if (shown == GameSessionFlow.FlowState.Archive) SelectArchiveTab(0);
        if (shown == GameSessionFlow.FlowState.Result && flow.ActiveFight != null)
        {
            var fight = flow.ActiveFight;
            resultHeading.text = fight.Result == BossFight.Outcome.Won ? "MISSION SUCCESS" : "MISSION FAILED";
            float duration = fight.CombatDuration;
            timeValue.text = $"{(int)(duration / 60):00}:{duration % 60:00.0}";
            bossValue.text = fight.ResultBossHealth.ToString();
            playerValue.text = fight.ResultPlayerHealth.ToString();
            accuracyValue.text = fight.AccuracyPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }
    }

    private void RecordFields(Transform parent, string[] names, string[] values, float top, float spacing)
    {
        float width = ((RectTransform)parent).sizeDelta.x;
        for (int i = 0; i < names.Length; i++)
        {
            string value = values != null && !string.IsNullOrWhiteSpace(values[i]) ? values[i] : "—";
            Text(parent, "Field_" + names[i], names[i], new Vector2(width * .39f, top - i * spacing), new Vector2(width * .7f, 45), 23, TextAlignmentOptions.Left);
            Text(parent, "Value_" + names[i], value, new Vector2(width * .87f, top - i * spacing), new Vector2(width * .15f, 45), 23, TextAlignmentOptions.Right);
        }
    }

    private GameObject Screen(RectTransform parent, string name)
    {
        var rect = Rect(parent, name, Vector2.zero, Vector2.zero);
        Stretch(rect);
        var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = Color.black;
        background.raycastTarget = true;
        return rect.gameObject;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        if (parent.parent != null && parent.parent.GetComponent<Canvas>() != null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position - new Vector2(960, 540);
        }
        return rect;
    }
    private RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, position, size);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black; image.raycastTarget = false;
        Edge(rect, "Top", new Vector2(.5f, 1), new Vector2(1, 0), new Vector2(0, 2));
        Edge(rect, "Bottom", new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(0, 2));
        Edge(rect, "Left", new Vector2(0, .5f), new Vector2(0, 1), new Vector2(2, 0));
        Edge(rect, "Right", new Vector2(1, .5f), new Vector2(0, 1), new Vector2(2, 0));
        return rect;
    }
    private static void Edge(RectTransform parent, string name, Vector2 center, Vector2 stretch, Vector2 size)
    {
        var rect = Rect(parent, name, Vector2.zero, size);
        rect.anchorMin = center - stretch * .5f; rect.anchorMax = center + stretch * .5f;
        rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
        rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
    }
    private UnityEngine.UI.Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        var rect = Panel(parent, name, position, size);
        rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        ConfigureButton(button);
        button.onClick.AddListener(click);
        Text(rect, "Text", label, size * .5f, size - new Vector2(24, 10), 30);
        return button;
    }
    private static void ConfigureButton(UnityEngine.UI.Button button)
    {
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
    }
    private TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = Rect(parent, name, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.color = Color.white;
        text.alignment = alignment; text.text = value; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    private RectTransform HealthBar(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var outer = Panel(parent, name, position, size);
        var fill = Rect(outer, "Health", Vector2.zero, Vector2.zero);
        fill.anchorMin = fill.anchorMax = new Vector2(0, .5f);
        fill.pivot = new Vector2(0, .5f);
        fill.anchoredPosition = new Vector2(4, 0);
        fill.sizeDelta = size - new Vector2(8, 8);
        var image = fill.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.white; image.raycastTarget = false;
        return fill;
    }
    private static void Fill(RectTransform fill, float amount) { fill.localScale = new Vector3(Mathf.Clamp01(amount), 1, 1); fill.gameObject.SetActive(amount > 0); }
    private void OnDestroy() { foreach (var reference in uiReferences) if (reference != null) Destroy(reference); }
}

/// <summary>Only the requested body silhouette: black polygon with a white outline, no texture or effects.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionBodyRegion : UnityEngine.UI.MaskableGraphic
{
    public bool Round;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        int count = Round ? 32 : 4;
        Vector2[] points = Round ? null : new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        mesh.AddVert(Vector2.zero, Color.black, Vector2.zero);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = Round ? new Vector2(Mathf.Cos(i * Mathf.PI * 2 / count), Mathf.Sin(i * Mathf.PI * 2 / count)) : points[i];
            mesh.AddVert(new Vector2(p.x * rect.width * .5f, p.y * rect.height * .5f), Color.black, Vector2.zero);
        }
        for (int i = 0; i < count; i++) mesh.AddTriangle(0, i + 1, (i + 1) % count + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 a = Round ? new Vector2(Mathf.Cos(i * Mathf.PI * 2 / count), Mathf.Sin(i * Mathf.PI * 2 / count)) : points[i];
            Vector2 b = Round ? new Vector2(Mathf.Cos((i + 1) * Mathf.PI * 2 / count), Mathf.Sin((i + 1) * Mathf.PI * 2 / count)) : points[(i + 1) % count];
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(a.x * rect.width * .5f, a.y * rect.height * .5f), Color.white, Vector2.zero);
            mesh.AddVert(new Vector2(b.x * rect.width * .5f, b.y * rect.height * .5f), Color.white, Vector2.zero);
            mesh.AddVert(new Vector2(b.x * (rect.width * .5f - 2), b.y * (rect.height * .5f - 2)), Color.white, Vector2.zero);
            mesh.AddVert(new Vector2(a.x * (rect.width * .5f - 2), a.y * (rect.height * .5f - 2)), Color.white, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
