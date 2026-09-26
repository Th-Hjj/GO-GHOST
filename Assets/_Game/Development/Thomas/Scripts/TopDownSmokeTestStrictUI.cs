#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class TopDownSmokeTest
{
    private static string[] StrictTexts() => FindObjectsByType<TMP_Text>().Where(t => t.isActiveAndEnabled).Select(t => t.text).ToArray();
    private static string StrictText(string name) => GameObject.Find(name).GetComponent<TMP_Text>().text;
    private static string[] StrictOfficerFields = { "OFFICER #", "AGE", "RANK", "YEARS OF SERVICE", "MISSIONS COMPLETED", "MISSION SUCCESS RATE", "STATUS" };
    private static string[] StrictRegions = { "HEAD", "TORSO", "LEFT ARM", "RIGHT ARM", "LEFT LEG", "RIGHT LEG", "SPINE" };
    private static string[] StrictTabs = { "POLICE RECORDS", "MISSION INTELLIGENCE", "CASE CLOSURE RECORDS" };

    private void StrictAudit(string screen, string[] permitted)
    {
        var texts = StrictTexts();
        Check(screen + " text allowlist", texts.OrderBy(x => x).SequenceEqual(permitted.OrderBy(x => x)), string.Join(" | ", texts));
        var ui = FindAnyObjectByType<MissionUI>();
        var graphics = Resources.FindObjectsOfTypeAll<Graphic>().Where(g => g.gameObject.scene.IsValid()).ToArray();
        Check(screen + " UI audit", FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length == 1
            && graphics.All(g => g.transform.IsChildOf(ui.transform) && (g.color == Color.white || g.color == Color.black))
            && FindObjectsByType<TextMesh>(FindObjectsInactive.Include).Length == 0
            && FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include).Length == 0
            && FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsInactive.Include).Length == 0
            && FindObjectsByType<Button>(FindObjectsInactive.Include).All(b => b.transition == Selectable.Transition.None),
            $"one Canvas; {graphics.Length} active/inactive graphics; black/white only; no other text UI; no button effects");
        System.IO.File.WriteAllLines("Logs/GO_GHOST_VS/strict-" + screen + "-text.txt", texts);
    }

    private IEnumerator StrictClickRegion(int index)
    {
        var region = GameObject.Find("Region_" + StrictRegions[index].Replace(' ', '_'));
        // The Spine is intentionally inside the Torso; hit the Torso's exposed left side.
        Vector3 world = region.transform.position;
        if (index == 1) world = region.transform.TransformPoint(new Vector3(-70, 0, 0));
        Vector2 p = RectTransformUtility.WorldToScreenPoint(null, world);
        Mouse(p.x / Screen.width, p.y / Screen.height, false); yield return Frames(3);
        Mouse(p.x / Screen.width, p.y / Screen.height, true); yield return Frames(3);
        Mouse(p.x / Screen.width, p.y / Screen.height, false); yield return Frames(3);
    }

    private IEnumerator StrictUI()
    {
        Record("Strict UI smoke: real uGUI pointer events and virtual combat input; controlled outcomes, isolated Archive. Not natural victory/balance.");
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var ui = FindAnyObjectByType<MissionUI>();
        try
        {
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.MainMenu, "Main Menu", 3);
            yield return Frames(4);
            string[] mainText = new[] { "GO GHOST", "START MISSION", "POLICE ARCHIVE" }.Concat(StrictOfficerFields).Concat(StrictRegions).Concat(Enumerable.Repeat("—", 14)).ToArray();
            StrictAudit("main", mainText);
            Check("Launch to Main Menu", flow.ActiveFight == null && FindObjectsByType<Button>().Length == 2
                && GameObject.Find("PreviousOfficer") != null && GameObject.Find("AugmentationRecord") != null,
                "title, seven officer fields, seven augmentation rows, neutral values; exactly two buttons");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-main.png");
            yield return Frames(2);
            yield return ClickSessionButton("StartMission");
            Check("START MISSION opens Upgrade", flow.State == GameSessionFlow.FlowState.Loadout && flow.ActiveFight == null
                && FindObjectsByType<MissionBodyRegion>().Length == 7 && flow.Draft.BodyModificationPercent == 0,
                "seven selectable regions, fresh editable build, no intervening screen");
            StrictAudit("upgrade-empty", new[] { "BEGIN OPERATION" });
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-upgrade.png");
            yield return Frames(2);
            LoadoutSlot[] slots = { LoadoutSlot.Brain, LoadoutSlot.Torso, LoadoutSlot.LeftArm, LoadoutSlot.RightArm, LoadoutSlot.LeftLeg, LoadoutSlot.RightLeg };
            bool selected = true, choicesWork = true, validOnly = true;
            for (int i = 0; i < 7; i++)
            {
                yield return StrictClickRegion(i);
                selected &= ui.SelectedRegion == i;
                var definitions = i == 6 ? Array.Empty<AugmentationDefinition>()
                    : LoadoutRules.PrototypeCatalog.Where(d => d.Occupies(slots[i]) && d.ItemId != LoadoutRules.DetachedDroneArm).ToArray();
                string[] expected = new[] { "BEGIN OPERATION" }.Concat(definitions.Select(d => LoadoutRules.ItemLabel(d.ItemId))).ToArray();
                validOnly &= StrictTexts().OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x));
                foreach (var definition in definitions)
                {
                    yield return ClickSessionButton("Choice_" + definition.ItemId);
                    choicesWork &= flow.Draft.GetSlot(slots[i]).ItemId == definition.ItemId;
                }
            }
            Check("Seven body regions and existing choices", selected && choicesWork && validOnly,
                $"seven clicks={selected}; all available choices equip={choicesWork}; catalog-only/Spine empty={validOnly}; Drone unavailable before existing unlock");
            // Show a real populated submenu for visual inspection, without altering the final selected build.
            yield return StrictClickRegion(0);
            StrictAudit("upgrade-head", new[] { "BEGIN OPERATION", "Targeting AI", "Predictive Cortex" });
            yield return Seconds(.25f);
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-upgrade-head.png");
            yield return Frames(2);
            float deployRequestedAt = Time.unscaledTime;
            yield return ClickSessionButton("BeginOperation");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "BEGIN OPERATION", 3);
            var f = flow.ActiveFight;
            Check("BEGIN OPERATION deploys selected build", f.Deployment.GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.PredictiveCortex
                && f.Deployment.HasTimeSlow && f.Deployment.HasCounterArm && flow.Draft.IsLocked && Time.unscaledTime - deployRequestedAt < 2,
                "selected snapshot locked; only existing six fixed settling steps; no presentation wait");
            FocusV2(flow); yield return Frames(4);
            Check("Only the two health bars", GameObject.Find("CombatHUD").transform.childCount == 2
                && GameObject.Find("BossHealth") != null && GameObject.Find("PlayerHealth") != null && StrictTexts().Length == 0,
                "Boss at top / Player at bottom; no combat text or other HUD");
            StrictAudit("combat", Array.Empty<string>());
            var rb = f.Player.GetComponent<Rigidbody2D>();
            float x = rb.position.x;
            Keys(Key.D); yield return Seconds(.2f); Keys();
            bool moved = rb.position.x > x + .5f;
            var shooting = f.Player.GetComponent<PlayerShooting>();
            // One clear miss followed by live aimed fire proves the accuracy denominator and numerator.
            AimAtWorld(f.Player.transform.position + Vector3.left * 20, true); yield return Seconds(.12f); MouseTo(.5f, .5f);
            yield return Seconds(.35f);
            float shotDeadline = Time.unscaledTime + 3;
            while (shooting.ShotsHit < 1 && Time.unscaledTime < shotDeadline)
            {
                AimAtWorld(f.Boss.transform.position, true);
                yield return null;
            }
            MouseTo(.5f, .5f); yield return Frames(3);
            yield return Phase8Until(() => f.Boss.ShotsFired > 0, "first Boss shot", 5);
            Check("Core fight and accuracy counters", moved && f.Boss.ShotsFired > 0 && shooting.ShotsHit > 0 && shooting.ShotsFired > shooting.ShotsHit,
                $"move={moved}; Boss shots={f.Boss.ShotsFired}; player hits/shots={shooting.ShotsHit}/{shooting.ShotsFired}; Boss HP={f.BossHealth.CurrentHealth}");
            Keys(Key.E, Key.Q); yield return Frames(2); Keys();
            Check("Selected abilities remain usable", f.Player.GetComponent<PlayerCounterArm>().IsWindowOpen && f.Player.GetComponent<PlayerTimeSlow>().IsActive,
                "existing E Counter / Q Time Slow; no additional UI");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(1, f.Boss.gameObject);
            yield return Frames(2);
            var bossBar = GameObject.Find("BossHealth").transform.Find("Health");
            var playerBar = GameObject.Find("PlayerHealth").transform.Find("Health");
            Check("Both HP bars track current health", Mathf.Approximately(bossBar.localScale.x, (float)f.BossHealth.CurrentHealth / f.BossHealth.MaxHealth)
                && Mathf.Approximately(playerBar.localScale.x, (float)f.Player.CurrentHealth / f.Player.MaxHealth)
                && bossBar.localScale.x < 1 && playerBar.localScale.x < 1, $"fill: boss={bossBar.localScale.x}, player={playerBar.localScale.x}");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-combat.png");
            yield return Frames(2);
            // Controlled damage tests the existing outcome resolver, not natural winnability.
            f.BossHealth.TakeDamage(10000, f.Player.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION SUCCESS", 3);
            yield return Frames(3);
            string[] success = new[] { "MISSION SUCCESS", "TIME", "BOSS HP", "PLAYER HP", "ACCURACY", "CONTINUE",
                StrictText("StatisticValue0"), StrictText("StatisticValue1"), StrictText("StatisticValue2"), StrictText("StatisticValue3") };
            StrictAudit("success", success);
            float frozenTime = f.CombatDuration;
            float expectedAccuracy = 100f * shooting.ShotsHit / shooting.ShotsFired;
            Check("Victory result / four frozen statistics", f.Result == BossFight.Outcome.Won && frozenTime > 0 && f.ResultBossHealth == 0
                && f.ResultPlayerHealth == f.Player.CurrentHealth && Mathf.Approximately(f.AccuracyPercent, expectedAccuracy)
                && StrictText("StatisticValue1") == "0" && StrictText("StatisticValue2") == f.ResultPlayerHealth.ToString()
                && StrictText("StatisticValue3") == expectedAccuracy.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
                && FindObjectsByType<Button>().Length == 1,
                $"TIME={frozenTime:0.00}s; BOSS HP={f.ResultBossHealth}; PLAYER HP={f.ResultPlayerHealth}; ACCURACY={f.AccuracyPercent:0.0}%");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-success.png");
            yield return Seconds(.25f);
            Check("Result values stay frozen", f.CombatDuration == frozenTime && StrictTexts().OrderBy(s => s).SequenceEqual(success.OrderBy(s => s)), "combat cleanup cannot change displayed results");
            yield return ClickSessionButton("ResultContinue");
            Check("CONTINUE returns to Main Menu", flow.State == GameSessionFlow.FlowState.MainMenu && flow.ActiveFight == null && FindObjectsByType<Button>().Length == 2,
                "attempt disposed; previous records remain ungenerated placeholders");
            StrictAudit("main-return", mainText);
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "second combat", 3);
            FocusV2(flow); yield return Frames(4);
            f = flow.ActiveFight;
            Check("Fresh attempt reset", f.Deployment.BodyModificationPercent == 0 && f.Player.GetComponent<PlayerShooting>().ShotsFired == 0
                && f.Player.GetComponent<PlayerShooting>().ShotsHit == 0 && FindObjectsByType<BossFight>().Length == 1,
                "fresh human build and counters, one attempt");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION FAILED", 3);
            yield return Frames(3);
            StrictAudit("failure", new[] { "MISSION FAILED", "TIME", "BOSS HP", "PLAYER HP", "ACCURACY", "CONTINUE",
                StrictText("StatisticValue0"), f.ResultBossHealth.ToString(), "0", "0.0%" });
            Check("Player death opens failure result", f.Result == BossFight.Outcome.Died && f.ResultPlayerHealth == 0 && f.ResultBossHealth == f.BossHealth.CurrentHealth
                && f.AccuracyPercent == 0 && !f.Player.GetComponent<PlayerInputReader>().GameplayInputEnabled,
                "no auto restart or death/transition screen; zero-shot accuracy handled");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-failure.png");
            yield return Frames(2);
            yield return ClickSessionButton("ResultContinue");
            yield return ClickSessionButton("PoliceArchive");
            Check("POLICE ARCHIVE opens shell", flow.State == GameSessionFlow.FlowState.Archive && ui.ArchiveTab == 0 && FindObjectsByType<Button>().Length == 3,
                "exactly the three specified tabs; no extra navigation");
            StrictAudit("police-records", StrictTabs);
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-police-records.png");
            yield return Frames(2);
            yield return ClickSessionButton("ArchiveTab1");
            StrictAudit("intelligence", StrictTabs.Concat(new[] { PoliceArchive.TempFirstVictory, "Defeat the Boss once.", "COMPLETED" }).ToArray());
            Check("Existing achievement without reward information", ui.ArchiveTab == 1 && PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory)
                && !StrictTexts().Any(t => t.Contains("Drone") || t.Contains("Unlock")), "existing achievement ID, trigger condition and completion only");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-intelligence.png");
            yield return Frames(2);
            yield return ClickSessionButton("ArchiveTab2");
            StrictAudit("case-closure", StrictTabs);
            Check("Case Closure structure only", ui.ArchiveTab == 2 && StrictTexts().Length == 3, "empty content; no endings or counters");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/strict-case-closure.png");
            yield return Frames(2);
            yield return ClickSessionButton("ArchiveTab0");
            Check("Police Records stays empty", ui.ArchiveTab == 0 && StrictTexts().Length == 3, "no historical officers generated after either outcome");
        }
        finally
        {
            v2ProtectedPlayer = null;
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
