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
    // Officer/build values come only from completed Police Archive run records; "—" until one exists.
    private static readonly string[] OfficerFields = { "OFFICER #", "AGE", "RANK", "YEARS OF SERVICE", "MISSIONS COMPLETED", "MISSION SUCCESS RATE", "STATUS" };
    private static readonly string[] Regions = { "HEAD", "TORSO", "LEFT ARM", "RIGHT ARM", "LEFT LEG", "RIGHT LEG", "WEAPON" };
    private static readonly string[] ResultLabels = { "TIME", "BOSS HP", "PLAYER HP", "ACCURACY" };
    private static readonly LoadoutSlot[] Slots = { LoadoutSlot.Brain, LoadoutSlot.Torso, LoadoutSlot.LeftArm, LoadoutSlot.RightArm, LoadoutSlot.LeftLeg, LoadoutSlot.RightLeg, LoadoutSlot.Weapon };
    private GameObject main, upgrade, combat, result, archive, practice, ending;
    private RectTransform choices, archiveContent;
    private RectTransform bossFill, playerFill;
    private TMP_Text resultHeading, timeValue, bossValue, playerValue, accuracyValue, practiceControls, endingTitle, endingText, ammoText;
    private GameObject ammo;
    private PlayerShooting ammoSource;
    private int shownAmmo = -1, shownMagazine = -1;
    private bool shownReloading;
    private TMP_Text[] previousOfficerValues, previousEquipmentValues;
    private GameSessionFlow.FlowState shown = (GameSessionFlow.FlowState)(-1);
    public int SelectedRegion { get; private set; } = -1;
    public int ArchiveTab { get; private set; }
    private readonly List<InputActionReference> uiReferences = new List<InputActionReference>();
    private SfxSet sfx; // menu/result scope, separate from the combat attempt's voices

    private void Start()
    {
        if (sfx != null) sfx.BeginScope();
    }

    private void Play(Sfx id) { if (sfx != null) sfx.Play(id); }

    // S08 only when the draft actually changes; an unchanged or rejected choice stays silent.
    private void Equip(LoadoutSlot slot, string id)
    {
        string before = flow.Draft.GetSlot(slot).ItemId;
        if (flow.TrySetDraftSlot(slot, id) && flow.Draft.GetSlot(slot).ItemId != before) Play(Sfx.Equip);
    }

    private void Awake()
    {
        sfx = GetComponent<SfxSet>();
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
        BuildEnding(root);
        // Practice room only: the approved controls lines, shown for the whole stay and hidden at the door.
        practice = Rect(root, "PracticeControls", Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)practice.transform);
        practiceControls = Text(practice.transform, "Controls", "", new Vector2(540, 590), new Vector2(700, 380), 26, TextAlignmentOptions.TopLeft);
        // Magazine readout just above the player HP bar position, in the practice room and the Boss room: "12 / 24" or "RELOADING".
        ammo = Rect(root, "AmmoHUD", Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)ammo.transform);
        ammoText = Text(ammo.transform, "Ammo", "", new Vector2(960, 72), new Vector2(400, 40), 26);
        var ammoRect = (RectTransform)ammoText.transform;
        ammoRect.anchorMin = ammoRect.anchorMax = new Vector2(.5f, 0);
        ammoRect.anchoredPosition = new Vector2(0, 72);
        var events = new GameObject("MissionEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
        events.transform.SetParent(transform, false);
        var module = events.GetComponent<InputSystemUIInputModule>();
        module.actionsAsset = inputActions;
        module.point = Action("UI/Point");
        module.leftClick = Action("UI/Click");
        module.move = Action("UI/Navigate");
        module.submit = Action("UI/Submit");
        module.cancel = Action("UI/Cancel");
        module.scrollWheel = Action("UI/ScrollWheel"); // Existing UI action; only scrolls the Police Records list.
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
        previousOfficerValues = RecordFields(left, OfficerFields, null, 700, 96);
        var right = Panel(main.transform, "AugmentationRecord", new Vector2(1625, 510), new Vector2(480, 780));
        previousEquipmentValues = RecordFields(right, Regions, null, 700, 96);
    }

    private void BuildUpgrade(RectTransform root)
    {
        upgrade = Screen(root, "Upgrade");
        BackButton(upgrade.transform, "UpgradeBack");
        BodyRegion(0, new Vector2(960, 866), new Vector2(124, 140), true);
        BodyRegion(1, new Vector2(960, 628), new Vector2(240, 292));
        BodyRegion(2, new Vector2(782, 623), new Vector2(95, 300));
        BodyRegion(3, new Vector2(1138, 623), new Vector2(95, 300));
        BodyRegion(4, new Vector2(886, 315), new Vector2(100, 300));
        BodyRegion(5, new Vector2(1034, 315), new Vector2(100, 300));
        Button(upgrade.transform, "Region_WEAPON", "WEAPON", new Vector2(570, 628), new Vector2(220, 72), () => SelectRegion(6));
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
        button.onClick.AddListener(() => { Play(Sfx.Click); SelectRegion(index); });
    }

    private void SelectRegion(int index)
    {
        if (flow.State != GameSessionFlow.FlowState.Loadout) return;
        SelectedRegion = index;
        // Border objects stay; only the old option buttons are replaced.
        foreach (Transform child in choices)
            if (child.name.StartsWith("Choice_", StringComparison.Ordinal)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        choices.gameObject.SetActive(true);
        var slot = Slots[index];
        if (slot == LoadoutSlot.Weapon)
        {
            if (flow.Draft.CanUseWeapon)
                for (int i = 0; i < LoadoutRules.OfferedWeapons.Length; i++)
                {
                    string weaponId = LoadoutRules.OfferedWeapons[i];
                    Button(choices, "Choice_" + weaponId, LoadoutRules.WeaponLabel(weaponId), new Vector2(250, 374 - i * 90), new Vector2(450, 72), () => Equip(slot, weaponId), false);
                }
            return;
        }
        // Human first (removes the augmentation), then this round's offered upgrades. Hidden items are simply not listed.
        Button(choices, "Choice_Human", "Human", new Vector2(250, 374), new Vector2(450, 72), () => Equip(slot, ""), false);
        int row = 1;
        foreach (var definition in LoadoutRules.PrototypeCatalog)
        {
            if (!definition.Occupies(slot) || !LoadoutRules.IsOffered(slot, definition.ItemId)) continue;
            if (definition.ItemId == LoadoutRules.DetachedDroneArm && !PoliceArchive.Current.HasHiddenAugmentation(definition.ItemId)) continue;
            string id = definition.ItemId;
            // A leg ability already on the other leg cannot be installed twice.
            if (LoadoutRules.IsLeg(slot) && flow.Draft.GetSlot(slot == LoadoutSlot.LeftLeg ? LoadoutSlot.RightLeg : LoadoutSlot.LeftLeg).ItemId == id) continue;
            Button(choices, "Choice_" + id, LoadoutRules.ItemLabel(id), new Vector2(250, 374 - row * 90), new Vector2(450, 72), () => Equip(slot, id), false);
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
        var values = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            Text(result.transform, "StatisticLabel" + i, ResultLabels[i], new Vector2(760, 660 - i * 110), new Vector2(390, 70), 32, TextAlignmentOptions.Left);
            values[i] = Text(result.transform, "StatisticValue" + i, "", new Vector2(1190, 660 - i * 110), new Vector2(360, 70), 32, TextAlignmentOptions.Right);
        }
        timeValue = values[0]; bossValue = values[1]; playerValue = values[2]; accuracyValue = values[3];
        Button(result.transform, "ResultContinue", "CONTINUE", new Vector2(960, 100), new Vector2(420, 90), () => flow.RequestContinue());
    }

    // Only the ending name, the team's exact ending text and CONTINUE.
    private void BuildEnding(RectTransform root)
    {
        ending = Screen(root, "Ending");
        endingTitle = Text(ending.transform, "EndingTitle", "", new Vector2(960, 800), new Vector2(1280, 120), 64);
        endingText = Text(ending.transform, "EndingText", "", new Vector2(960, 540), new Vector2(1400, 300), 36);
        endingText.textWrappingMode = TextWrappingModes.Normal;
        Button(ending.transform, "EndingContinue", "CONTINUE", new Vector2(960, 100), new Vector2(420, 90), () => flow.RequestContinue());
    }

    private void BuildArchive(RectTransform root)
    {
        archive = Screen(root, "Archive");
        BackButton(archive.transform, "ArchiveBack");
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
        if (tab == 0) BuildRunRecords();
        else if (tab == 1)
        {
            Text(archiveContent, "Content_Achievement", PoliceArchive.TempFirstVictory, new Vector2(880, 690), new Vector2(1500, 70), 32);
            Text(archiveContent, "Content_Condition", "Defeat the Boss once.", new Vector2(880, 590), new Vector2(1500, 70), 30);
            Text(archiveContent, "Content_Completion", PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory) ? "COMPLETED" : "INCOMPLETE", new Vector2(880, 490), new Vector2(1500, 70), 30);
        }
        else if (tab == 2)
        {
            // Unlocked formal ending names only: no ending text or descriptions. Older TEMP IDs stay in the save, unlisted.
            var endings = PoliceArchive.Current.UnlockedEndings;
            int listed = 0;
            for (int i = 0; i < endings.Count; i++)
            {
                if (endings[i] != PoliceArchive.HumanEnding && endings[i] != PoliceArchive.AiEnding) continue;
                Text(archiveContent, "Content_Ending" + listed, PoliceArchive.EndingTitle(endings[i]), new Vector2(880, 690 - listed * 100), new Vector2(1500, 70), 32);
                listed++;
            }
        }
    }

    // One simple vertical list of completed runs, newest first. No scrollbar, sorting, filtering or other controls.
    private void BuildRunRecords()
    {
        const float cardHeight = 380, gap = 20;
        var runs = PoliceArchive.Current.RunRecords;
        var view = Rect(archiveContent, "Content_Records", Vector2.zero, Vector2.zero);
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(4, 4); view.offsetMax = new Vector2(-4, -4);
        var viewImage = view.gameObject.AddComponent<UnityEngine.UI.Image>();
        viewImage.color = Color.black; // Receives drag/scroll events inside the existing content area.
        view.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        float listHeight = gap + runs.Count * (cardHeight + gap);
        var list = Rect(view, "Records", Vector2.zero, Vector2.zero);
        list.anchorMin = new Vector2(0, 1); list.anchorMax = Vector2.one; list.pivot = new Vector2(.5f, 1);
        list.anchoredPosition = Vector2.zero; list.sizeDelta = new Vector2(0, listHeight);
        var scroll = view.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.viewport = view; scroll.content = list;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = 60;
        float width = archiveContent.sizeDelta.x - 8;
        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[runs.Count - 1 - i];
            var card = Panel(list, "Record" + i, new Vector2(width * .5f, listHeight - gap - cardHeight * .5f - i * (cardHeight + gap)), new Vector2(width - 2 * gap, cardHeight));
            var officer = Rect(card, "Officer", new Vector2(300, 190), new Vector2(560, 360));
            RecordFields(officer, OfficerFields, OfficerValues(run), 330, 46);
            var build = Rect(card, "Build", new Vector2(860, 190), new Vector2(560, 360));
            RecordFields(build, Regions, EquipmentValues(run), 330, 46);
            var stats = Rect(card, "Combat", new Vector2(1410, 190), new Vector2(520, 360));
            RecordFields(stats, ResultLabels, new[] { FormatTime(run.CombatDuration), run.BossHealth.ToString(), run.PlayerHealth.ToString(), FormatAccuracy(run.AccuracyPercent) }, 330, 46);
        }
    }

    private static string[] OfficerValues(PoliceRunRecord run) => run == null ? null : new[]
    {
        run.OfficerNumber.ToString(), run.Age.ToString(), run.Rank, run.YearsOfService.ToString(), run.MissionsCompleted.ToString(),
        run.MissionSuccessRate + "%", run.Success ? "MISSION SUCCESS" : "MISSION FAILED"
    };

    // Stored item IDs resolve through the existing LoadoutRules labels.
    private static string[] EquipmentValues(PoliceRunRecord run)
    {
        if (run == null) return null;
        var values = new string[Slots.Length];
        for (int i = 0; i < Slots.Length; i++)
        {
            string id = run.SlotItemId(Slots[i]);
            values[i] = Slots[i] == LoadoutSlot.Weapon ? LoadoutRules.WeaponLabel(id) : LoadoutRules.ItemLabel(id);
        }
        return values;
    }

    private static string FormatTime(float duration) => $"{(int)(duration / 60):00}:{duration % 60:00.0}";
    private static string FormatAccuracy(float percent) => percent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private void Update()
    {
        if (shown != flow.State) Refresh();
        if (ammo.activeSelf && ammoSource != null)
        {
            if (ammoSource.IsReloading != shownReloading || ammoSource.AmmoInMagazine != shownAmmo || ammoSource.MagazineSize != shownMagazine)
            {
                shownReloading = ammoSource.IsReloading; shownAmmo = ammoSource.AmmoInMagazine; shownMagazine = ammoSource.MagazineSize;
                ammoText.text = shownReloading ? "RELOADING" : shownAmmo + " / " + shownMagazine;
            }
        }
        if (flow.State == GameSessionFlow.FlowState.Combat && flow.ActiveFight != null)
        {
            Fill(bossFill, (float)flow.ActiveFight.BossHealth.CurrentHealth / flow.ActiveFight.BossHealth.MaxHealth);
            Fill(playerFill, (float)flow.ActiveFight.Player.CurrentHealth / flow.ActiveFight.Player.MaxHealth);
        }
    }

    private void Refresh()
    {
        var previous = shown;
        shown = flow.State;
        // One result cue per resolved attempt: defeat now lands on the Main Menu, victory on the Result page.
        if (shown == GameSessionFlow.FlowState.MainMenu && previous == GameSessionFlow.FlowState.Combat) Play(Sfx.Defeat);
        if (shown == GameSessionFlow.FlowState.Result) Play(Sfx.Victory);
        main.SetActive(shown == GameSessionFlow.FlowState.MainMenu);
        upgrade.SetActive(shown == GameSessionFlow.FlowState.Loadout);
        combat.SetActive(shown == GameSessionFlow.FlowState.Combat);
        result.SetActive(shown == GameSessionFlow.FlowState.Result);
        archive.SetActive(shown == GameSessionFlow.FlowState.Archive);
        practice.SetActive(shown == GameSessionFlow.FlowState.Practice);
        ending.SetActive(shown == GameSessionFlow.FlowState.Ending);
        bool armed = (shown == GameSessionFlow.FlowState.Practice || shown == GameSessionFlow.FlowState.Combat) && flow.ActiveFight != null;
        ammoSource = armed ? flow.ActiveFight.Player.GetComponent<PlayerShooting>() : null;
        ammo.SetActive(armed);
        shownAmmo = shownMagazine = -1; // force one refresh for the new officer
        if (shown == GameSessionFlow.FlowState.Ending)
        {
            endingTitle.text = PoliceArchive.EndingTitle(flow.EndingId);
            endingText.text = PoliceArchive.EndingText(flow.EndingId);
        }
        if (shown == GameSessionFlow.FlowState.Practice) practiceControls.text = ControlsText(flow.Deployment);
        if (shown == GameSessionFlow.FlowState.MainMenu)
        {
            // The latest completed run is the previous officer. Before any run exists, values stay "—".
            var latest = PoliceArchive.Current.LatestRun;
            SetValues(previousOfficerValues, OfficerValues(latest));
            SetValues(previousEquipmentValues, EquipmentValues(latest));
        }
        if (shown == GameSessionFlow.FlowState.Loadout) { SelectedRegion = -1; choices.gameObject.SetActive(false); }
        if (shown == GameSessionFlow.FlowState.Archive) SelectArchiveTab(0);
        if (shown == GameSessionFlow.FlowState.Result && flow.ActiveFight != null)
        {
            var fight = flow.ActiveFight;
            resultHeading.text = fight.Result == BossFight.Outcome.Won ? "MISSION SUCCESS" : "MISSION FAILED";
            timeValue.text = FormatTime(fight.CombatDuration);
            bossValue.text = fight.ResultBossHealth.ToString();
            playerValue.text = fight.ResultPlayerHealth.ToString();
            accuracyValue.text = FormatAccuracy(fight.AccuracyPercent);
        }
    }

    // Thomas's approved wording. Only what the locked build actually supports; no title, icons or legacy inputs.
    private static string ControlsText(DeploymentSnapshot build)
    {
        var lines = new List<string>
        {
            "A / D — MOVE", build.Aim == AimControl.AI ? "AIM — AUTOMATIC" : "MOUSE — AIM",
            "LEFT MOUSE — SHOOT", "SPACE — JUMP", "LEFT SHIFT — DODGE"
        };
        if (build.HasDoubleJump) lines.Add("SPACE AGAIN — DOUBLE JUMP");
        if (build.HasHover) lines.Add("HOLD SPACE — HOVER");
        if (build.HasAirDash) lines.Add("LEFT SHIFT IN AIR — AIR DASH");
        if (build.HasArmBlade) lines.Add("E — SLASH / DEFLECT");
        else if (build.HasCounterArm) lines.Add("E — COUNTER");
        if (build.HasTimeSlow) lines.Add("Q — TIME SLOW");
        return string.Join("\n", lines);
    }

    private TMP_Text[] RecordFields(Transform parent, string[] names, string[] values, float top, float spacing)
    {
        float width = ((RectTransform)parent).sizeDelta.x;
        var valueTexts = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            Text(parent, "Field_" + names[i], names[i], new Vector2(width * .39f, top - i * spacing), new Vector2(width * .7f, 45), 23, TextAlignmentOptions.Left);
            valueTexts[i] = Text(parent, "Value_" + names[i], "", new Vector2(width * .87f, top - i * spacing), new Vector2(width * .15f, 45), 23, TextAlignmentOptions.Right);
        }
        SetValues(valueTexts, values);
        return valueTexts;
    }

    private static void SetValues(TMP_Text[] texts, string[] values)
    {
        for (int i = 0; i < texts.Length; i++)
            texts[i].text = values != null && !string.IsNullOrWhiteSpace(values[i]) ? values[i] : "—";
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
    private UnityEngine.UI.Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click, bool clickSound = true)
    {
        var rect = Panel(parent, name, position, size);
        rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        ConfigureButton(button);
        if (clickSound) button.onClick.AddListener(() => Play(Sfx.Click));
        button.onClick.AddListener(click);
        Text(rect, "Text", label, size * .5f, size - new Vector2(24, 10), 30);
        return button;
    }
    private void BackButton(Transform parent, string name)
    {
        var button = Button(parent, name, "BACK", Vector2.zero, new Vector2(180, 50), () => flow.RequestBackToMainMenu());
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(120, -30);
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
