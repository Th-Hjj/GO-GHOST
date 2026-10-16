using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

/// <summary>Existing screens with scoped terminal presentation. GameSessionFlow owns every gameplay transition.</summary>
public sealed partial class MissionUI : MonoBehaviour
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
    // TEST colours, one place: Boss Phase 2 bar, and the player bar end colour at 100% Cyberization (white at 0%).
    private static readonly Color BossPhase2Fill = Color.red;
    private static readonly Color CyberizedHealthFill = new Color(.10f, .28f, .90f);
    private UnityEngine.UI.Image bossFillImage, playerFillImage;
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
    // Selected state only: the selected region / equipped choice / open tab is drawn inverted (white fill, black text).
    private readonly MissionBodyRegion[] bodyRegions = new MissionBodyRegion[6];
    private UnityEngine.UI.Button weaponRegion;
    private readonly UnityEngine.UI.Button[] archiveTabs = new UnityEngine.UI.Button[3];

    private static void Highlight(UnityEngine.UI.Button button, bool on)
    {
        if (TerminalContext(button.transform)) { TerminalButtonStyle(button, on); return; }
        button.targetGraphic.color = on ? Color.white : Color.black;
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = on ? Color.black : Color.white;
    }

    private void ShowSelectedRegion(int index)
    {
        for (int i = 0; i < bodyRegions.Length; i++)
        {
            bodyRegions[i].Selected = i == index;
            bodyRegions[i].Equipped = !string.IsNullOrEmpty(flow.Draft.GetSlot(Slots[i]).ItemId);
            bodyRegions[i].SetVerticesDirty();
        }
        Highlight(weaponRegion, index == 6);
    }

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
        if (SelectedRegion >= 0) SelectRegion(SelectedRegion); // redraw the equipped state
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
        BuildDialogue(root);
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
        TerminalRule(main.transform, "TitleRule", new Vector2(960, 918), new Vector2(620, 1));
        TerminalRule(main.transform, "CommandRule", new Vector2(960, 330), new Vector2(160, 2));
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
        BodyRegion(0, new Vector2(860, 821), new Vector2(72, 88), true);
        BodyRegion(1, new Vector2(860, 645), new Vector2(146, 222));
        BodyRegion(2, new Vector2(663, 757.5f), new Vector2(264, 117));
        BodyRegion(3, new Vector2(1057, 757.5f), new Vector2(264, 117));
        BodyRegion(4, new Vector2(801.5f, 364), new Vector2(103, 334));
        BodyRegion(5, new Vector2(918.5f, 364), new Vector2(103, 334));
        weaponRegion = Button(upgrade.transform, "Region_WEAPON", "WEAPON", new Vector2(245, 565), new Vector2(250, 72), () => SelectRegion(6));
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
        graphic.Contour = BodyContour(index);
        graphic.NeuralStyle = true;
        graphic.NeuralRegion = index;
        bodyRegions[index] = graphic;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = graphic;
        ConfigureButton(button);
        button.onClick.AddListener(() => { Play(Sfx.Click); SelectRegion(index); });
    }

    private void SelectRegion(int index)
    {
        if (flow.State != GameSessionFlow.FlowState.Loadout) return;
        SelectedRegion = index;
        ShowSelectedRegion(index);
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
                    Highlight(Button(choices, "Choice_" + weaponId, LoadoutRules.WeaponLabel(weaponId), new Vector2(250, 374 - i * 90), new Vector2(450, 72), () => Equip(slot, weaponId), false),
                        flow.Draft.WeaponId == weaponId);
                }
            return;
        }
        // Human first (removes the augmentation), then this round's offered upgrades. Hidden items are simply not listed.
        string equipped = flow.Draft.GetSlot(slot).ItemId;
        Highlight(Button(choices, "Choice_Human", "Human", new Vector2(250, 374), new Vector2(450, 72), () => Equip(slot, ""), false), string.IsNullOrEmpty(equipped));
        int row = 1;
        foreach (var definition in LoadoutRules.PrototypeCatalog)
        {
            if (!definition.Occupies(slot) || !LoadoutRules.IsOffered(slot, definition.ItemId)) continue;
            if (definition.ItemId == LoadoutRules.DetachedDroneArm && !PoliceArchive.Current.HasHiddenAugmentation(definition.ItemId)) continue;
            string id = definition.ItemId;
            // A leg ability already on the other leg cannot be installed twice.
            if (LoadoutRules.IsLeg(slot) && flow.Draft.GetSlot(slot == LoadoutSlot.LeftLeg ? LoadoutSlot.RightLeg : LoadoutSlot.LeftLeg).ItemId == id) continue;
            Highlight(Button(choices, "Choice_" + id, LoadoutRules.ItemLabel(id), new Vector2(250, 374 - row * 90), new Vector2(450, 72), () => Equip(slot, id), false), equipped == id);
            row++;
        }
    }

    private void BuildCombat(RectTransform root)
    {
        combat = Rect(root, "CombatHUD", Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)combat.transform);
        bossFill = HealthBar(combat.transform, "BossHealth", new Vector2(960, 970), new Vector2(1200, 28));
        playerFill = HealthBar(combat.transform, "PlayerHealth", new Vector2(960, 28), new Vector2(1800, 28));
        bossFillImage = bossFill.GetComponent<UnityEngine.UI.Image>();
        playerFillImage = playerFill.GetComponent<UnityEngine.UI.Image>();
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
        Panel(result.transform, "ResultSummary", new Vector2(960, 500), new Vector2(900, 490));
        TerminalRule(result.transform, "ResultRule", new Vector2(960, 805), new Vector2(720, 1));
        resultHeading = Text(result.transform, "ResultHeading", "", new Vector2(960, 890), new Vector2(1280, 120), 64);
        var values = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            TerminalRule(result.transform, "StatisticRule" + i, new Vector2(960, 618 - i * 110), new Vector2(790, 1));
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
            archiveTabs[i] = Button(archive.transform, "ArchiveTab" + i, tabs[i], new Vector2(380 + i * 580, 950), new Vector2(560, 90), () => SelectArchiveTab(index));
        }
        archiveContent = Panel(archive.transform, "ArchiveContent", new Vector2(960, 470), new Vector2(1760, 820));
    }

    private void SelectArchiveTab(int tab)
    {
        ArchiveTab = tab;
        for (int i = 0; i < archiveTabs.Length; i++) Highlight(archiveTabs[i], i == tab);
        foreach (Transform child in archiveContent)
            if (child.name.StartsWith("Content_", StringComparison.Ordinal)) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        if (tab == 0) BuildRunRecords();
        else if (tab == 1)
        {
            // Title, condition and completion only; the reward is never named here.
            Text(archiveContent, "Content_Achievement", PoliceArchive.MarksmanProtocolTitle, new Vector2(880, 690), new Vector2(1500, 70), 32);
            Text(archiveContent, "Content_Condition", PoliceArchive.MarksmanProtocolCondition, new Vector2(880, 590), new Vector2(1500, 70), 30);
            Text(archiveContent, "Content_Completion", PoliceArchive.Current.HasAchievement(PoliceArchive.MarksmanProtocol) ? "COMPLETED" : "INCOMPLETE", new Vector2(880, 490), new Vector2(1500, 70), 30);
        }
        else if (tab == 2)
        {
            // Two fixed, equal cards (left = Human slot, right = AI slot). A locked card is completely blank: no name,
            // hint or condition. Older TEMP ending IDs stay in the save and fill nothing.
            string[] slots = { PoliceArchive.HumanEnding, PoliceArchive.AiEnding };
            for (int i = 0; i < slots.Length; i++)
            {
                var card = Panel(archiveContent, "Content_EndingCard" + i, new Vector2(450 + i * 860, 410), new Vector2(820, 740));
                if (!PoliceArchive.Current.HasEnding(slots[i])) continue;
                Text(card, "CardTitle", PoliceArchive.EndingTitle(slots[i]), new Vector2(410, 590), new Vector2(720, 90), 44);
                var body = Text(card, "CardText", PoliceArchive.EndingText(slots[i]), new Vector2(410, 330), new Vector2(680, 320), 30);
                body.textWrappingMode = TextWrappingModes.Normal;
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
        TickDialogueInput();
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
            var target = flow.ActiveFight.BossHealth;
            bossFill.parent.gameObject.SetActive(target != null && (!flow.ActiveFight.TowerMission || flow.ActiveFight.EncounterActive));
            if (target != null) Fill(bossFill, (float)target.CurrentHealth / target.MaxHealth);
            bossFillImage.color = target != null && target.PhaseIndex == 2 ? BossPhase2Fill : Color.white; // same bar, second health pool
            Fill(playerFill, (float)flow.ActiveFight.Player.CurrentHealth / flow.ActiveFight.Player.MaxHealth);
        }
    }

    private void Refresh()
    {
        var previous = shown;
        shown = flow.State;
        // One result cue per resolved attempt, when its Mission Result page opens.
        if (shown == GameSessionFlow.FlowState.Result && previous != shown && flow.ActiveFight != null)
            Play(flow.ActiveFight.Result == BossFight.Outcome.Won ? Sfx.Victory : Sfx.Defeat);
        main.SetActive(shown == GameSessionFlow.FlowState.MainMenu);
        upgrade.SetActive(shown == GameSessionFlow.FlowState.Loadout);
        combat.SetActive(shown == GameSessionFlow.FlowState.Combat);
        result.SetActive(shown == GameSessionFlow.FlowState.Result);
        archive.SetActive(shown == GameSessionFlow.FlowState.Archive);
        practice.SetActive(shown == GameSessionFlow.FlowState.Practice);
        ending.SetActive(shown == GameSessionFlow.FlowState.Ending && !flow.HasTowerEnding);
        // One colour per deployment: white at 0% Cyberization, CyberizedHealthFill at 100%, from the locked snapshot only.
        if (shown == GameSessionFlow.FlowState.Combat && flow.Deployment != null)
            playerFillImage.color = Color.Lerp(Color.white, CyberizedHealthFill, Mathf.Clamp01(flow.Deployment.BodyModificationPercent / 100f));
        bool armed = (shown == GameSessionFlow.FlowState.Practice || shown == GameSessionFlow.FlowState.Combat) && flow.ActiveFight != null;
        ammoSource = armed ? flow.ActiveFight.Player.GetComponent<PlayerShooting>() : null;
        ammo.SetActive(armed);
        shownAmmo = shownMagazine = -1; // force one refresh for the new officer
        if (shown == GameSessionFlow.FlowState.Ending && !flow.HasTowerEnding)
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
        if (shown == GameSessionFlow.FlowState.Loadout) { SelectedRegion = -1; choices.gameObject.SetActive(false); ShowSelectedRegion(-1); }
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
        if (TerminalContext(parent))
        {
            // Stacked labels/values keep long stored names readable within the existing card bounds.
            bool compact = spacing < 60;
            for (int i = 0; i < names.Length; i++)
            {
                float y = top - i * spacing;
                Text(parent, "Field_" + names[i], names[i], new Vector2(width * .5f, y),
                    new Vector2(width - 56, compact ? 18 : 28), compact ? 14 : 18, TextAlignmentOptions.Left);
                valueTexts[i] = Text(parent, "Value_" + names[i], "", new Vector2(width * .5f, y - (compact ? 20 : 34)),
                    new Vector2(width - 56, compact ? 25 : 36), compact ? 22 : 26, TextAlignmentOptions.Left);
            }
            SetValues(valueTexts, values);
            return valueTexts;
        }
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
        if (TerminalContext(parent)) { TerminalPanelStyle(rect); return rect; }
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
        if (TerminalContext(button.transform) && button.targetGraphic is UnityEngine.UI.Image)
            TerminalButtonStyle(button, false);
    }
    private TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var rect = Rect(parent, name, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = fontSize; text.color = Color.white;
        text.alignment = alignment; text.text = value; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        if (TerminalContext(parent)) TerminalTextStyle(text, name);
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
    private void OnDestroy() { HideDialogue(); foreach (var reference in uiReferences) if (reference != null) Destroy(reference); }
}

/// <summary>Six existing clickable regions, redrawn as a minimal geometric expanded pose.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionBodyRegion : UnityEngine.UI.MaskableGraphic, ICanvasRaycastFilter,
    UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
    UnityEngine.EventSystems.ISelectHandler, UnityEngine.EventSystems.IDeselectHandler
{
    public bool Round;
    public bool Selected;
    public bool Equipped;
    public bool Monochrome;
    public Vector2[] Contour;
    public bool NeuralStyle;
    public int NeuralRegion;
    public float NeuralSignal, NeuralFlow = -1;
    [Header("Contour pulse (local path units)")]
    [Min(0)] public float PulseSpeed = 100;
    [Min(8)] public float PulseLength = 90;
    [Range(0, 2)] public float PulseIntensity = 1;
    [Tooltip("Equally spaced pulses per closed contour. One keeps the six-region figure quiet.")]
    [Range(1, 3)] public int PulseFrequency = 1;
    readonly List<Vector2> pathPoints = new List<Vector2>();
    readonly List<Vector2> pathNormals = new List<Vector2>();
    readonly List<float> pathDistances = new List<float>();
    Vector2[] cachedContour;
    Rect cachedRect;
    bool cachedRound, hovered, focused;
    float pathLength, activity, pulseVisibility;
    float NeuralStrokeScale => Mathf.Max(1, canvas != null ? canvas.scaleFactor : 1);

    void Update()
    {
        if (!NeuralStyle || NeuralRegion == 0) return;
        // UI runs while configuration freezes combat time. Absolute phase survives page rebuilds.
        float target = Mathf.Max(NeuralSignal, Selected || hovered || focused ? 1 : Equipped ? .38f : 0);
        if (NeuralFlow >= 0 && NeuralFlow < 1)
            target = Mathf.Max(target, Mathf.Sin(NeuralFlow * Mathf.PI));
        float previousVisibility = pulseVisibility;
        activity = Mathf.Lerp(activity, Mathf.Clamp01(target), 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
        pulseVisibility = Mathf.Lerp(pulseVisibility, target > 0 ? 1 : 0, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8));
        if (pulseVisibility < .001f) pulseVisibility = 0;
        if (pulseVisibility > 0 || previousVisibility > 0) SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        hovered = focused = false;
        activity = pulseVisibility = 0;
        base.OnDisable();
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData data) => hovered = true;
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data) => hovered = false;
    public void OnSelect(UnityEngine.EventSystems.BaseEventData data) => focused = true;
    public void OnDeselect(UnityEngine.EventSystems.BaseEventData data) => focused = false;

    private int PointCount => Round ? 32 : Contour != null ? Contour.Length : 4;
    private Vector2 Point(int index)
    {
        var r = rectTransform.rect;
        Vector2 p;
        if (Round)
        {
            float angle = index * Mathf.PI * 2 / PointCount;
            p = new Vector2(Mathf.Cos(angle) * .5f, Mathf.Sin(angle) * .5f);
        }
        else if (Contour != null) p = Contour[index];
        else p = new Vector2(index == 0 || index == 3 ? -.5f : .5f, index < 2 ? -.5f : .5f);
        return new Vector2(p.x * r.width, p.y * r.height) + r.center;
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        if (NeuralStyle) { DrawContourPulse(mesh); return; }
        int count = PointCount;
        var center = rectTransform.rect.center;
        Color outline = Selected ? new Color(.52f, .85f, 1f)
            : Equipped ? new Color(.30f, .68f, 1f) : new Color(.86f, .91f, .94f);
        Color fill = Selected ? new Color(.045f, .19f, .29f)
            : Equipped ? new Color(.025f, .065f, .095f) : new Color(.012f, .018f, .024f);
        if (Monochrome)
        {
            outline = Selected ? Color.white : new Color(.55f, .55f, .55f);
            fill = Selected ? new Color(.12f, .12f, .12f) : Color.black;
        }
        mesh.AddVert(center, fill, Vector2.zero);
        for (int i = 0; i < count; i++) mesh.AddVert(Point(i), fill, Vector2.zero);
        for (int i = 0; i < count; i++) mesh.AddTriangle(0, i + 1, (i + 1) % count + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 a = Point(i), b = Point((i + 1) % count);
            if (Selected || Equipped)
            {
                Color glow = outline; glow.a = Selected ? .10f : .05f;
                Segment(mesh, a, b, 9, glow);
            }
            float width = Monochrome ? 1.5f * (NeuralStyle ? NeuralStrokeScale : 1) / Mathf.Max(.05f, rectTransform.lossyScale.x) : Selected ? 3 : 2;
            Segment(mesh, a, b, width, outline);
            if (Equipped) Segment(mesh, Vector2.Lerp(center, a, .88f), Vector2.Lerp(center, b, .88f), 1,
                new Color(outline.r, outline.g, outline.b, .40f));
        }
    }

    void CacheContourPath()
    {
        var rect = rectTransform.rect;
        if (pathPoints.Count > 0 && cachedRect == rect && cachedContour == Contour && cachedRound == Round) return;
        cachedRect = rect; cachedContour = Contour; cachedRound = Round;
        pathPoints.Clear(); pathNormals.Clear(); pathDistances.Clear(); pathLength = 0;
        // Subdivide the original edges, never cut across a corner or bridge the anatomical gaps.
        for (int edge = 0; edge < PointCount; edge++)
        {
            Vector2 a = Point(edge), b = Point((edge + 1) % PointCount);
            float length = Vector2.Distance(a, b);
            if (length < .001f) continue;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2.5f));
            for (int step = 0; step < steps; step++)
            {
                float t = (float)step / steps;
                pathPoints.Add(Vector2.Lerp(a, b, t));
                pathDistances.Add(pathLength + t * length);
            }
            pathLength += length;
        }
        for (int i = 0; i < pathPoints.Count; i++)
        {
            Vector2 incoming = (pathPoints[i] - pathPoints[(i + pathPoints.Count - 1) % pathPoints.Count]).normalized;
            Vector2 outgoing = (pathPoints[(i + 1) % pathPoints.Count] - pathPoints[i]).normalized;
            Vector2 normal = new Vector2(-outgoing.y, outgoing.x);
            Vector2 join = (normal + new Vector2(-incoming.y, incoming.x)).normalized;
            // Shared, bounded miter vertices avoid cracks and overlapping bright corner quads.
            pathNormals.Add(join / Mathf.Max(.4f, Vector2.Dot(join, normal)));
        }
    }

    void DrawContourPulse(UnityEngine.UI.VertexHelper mesh)
    {
        CacheContourPath();
        if (pathLength < .001f) return;
        float pixel = NeuralStrokeScale / Mathf.Max(.05f, rectTransform.lossyScale.x);
        Color outline = NeuralPalette.Alpha(NeuralPalette.Secondary, .62f);
        Color identity = NeuralPalette.Region(NeuralRegion);
        float spacing = pathLength / Mathf.Clamp(PulseFrequency, 1, 3);
        float length = Mathf.Clamp(PulseLength, 8, spacing * .7f);
        // Double precision avoids long-session phase jitter. Both ends of the trail reach zero.
        // Paired contours already mirror one another. Shared phase preserves mirrored motion.
        int phaseRegion = NeuralRegion == 3 ? 2 : NeuralRegion == 5 ? 4 : NeuralRegion;
        float head = (float)((Time.unscaledTimeAsDouble * Mathf.Max(0, PulseSpeed) + phaseRegion * 97.0) % spacing);
        float intensity = Mathf.Clamp01(PulseIntensity * Mathf.Lerp(.62f, 1, activity)) * pulseVisibility;
        DrawContourBand(mesh, pixel, outline, identity, spacing, length, head, intensity, false);
        // Head is reserved for its own future effect, including in the legacy Upgrade screen.
        if (NeuralRegion != 0 && pulseVisibility > .001f)
            DrawContourBand(mesh, pixel, outline, identity, spacing, length, head, intensity, true);
    }

    // Feathered strips use the default UI material: Overlay-compatible, no texture or Bloom needed.
    static readonly float[] OutlineOffsets = { -1.25f, -.55f, .55f, 1.25f };
    static readonly float[] PulseOffsets = { -4.5f, -2.2f, -1.05f, -.35f, .35f, 1.05f, 2.2f, 4.5f };
    void DrawContourBand(UnityEngine.UI.VertexHelper mesh, float pixel, Color outline, Color identity,
        float spacing, float length, float head, float intensity, bool pulse)
    {
        float[] offsets = pulse ? PulseOffsets : OutlineOffsets;
        int start = mesh.currentVertCount, columns = offsets.Length, count = pathPoints.Count;
        for (int i = 0; i < count; i++)
        {
            float behind = Mathf.Repeat(head - pathDistances[i], spacing) / length;
            float strength = behind < 1 ? Mathf.SmoothStep(0, 1, behind / .12f) * (1 - behind) * (1 - behind) * intensity : 0;
            for (int j = 0; j < columns; j++)
            {
                bool edge = j == 0 || j == columns - 1;
                Color c = outline;
                if (pulse)
                {
                    c = j == 3 || j == 4 ? Color.white : identity;
                    c.a = edge ? 0 : (j == 1 || j == 6 ? .18f : 1) * strength;
                }
                else if (edge) c.a = 0;
                mesh.AddVert(pathPoints[i] + pathNormals[i] * (offsets[j] * pixel), c, Vector2.zero);
            }
        }
        for (int i = 0; i < count; i++)
            for (int j = 0; j < columns - 1; j++)
            {
                int a = start + i * columns + j, b = start + ((i + 1) % count) * columns + j;
                mesh.AddTriangle(a, a + 1, b + 1); mesh.AddTriangle(a, b + 1, b);
            }
    }

    private static void Segment(UnityEngine.UI.VertexHelper mesh, Vector2 a, Vector2 b, float width, Color c)
    {
        var delta = b - a;
        if (delta.sqrMagnitude < .0001f) return;
        var normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(a - normal, c, Vector2.zero); mesh.AddVert(a + normal, c, Vector2.zero);
        mesh.AddVert(b + normal, c, Vector2.zero); mesh.AddVert(b - normal, c, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        // Do not let an expanded limb's empty bounding-box corners steal adjacent region clicks.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var p)) return false;
        bool inside = false;
        int count = PointCount;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = Point(i), b = Point(j), segment = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, segment) / Mathf.Max(.0001f, segment.sqrMagnitude));
            if ((p - a - segment * t).sqrMagnitude <= 64) return true;
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }
}
