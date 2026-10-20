#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Run-history / TEMP achievement / TEMP ending smoke. Controlled outcomes in the isolated Archive; not balance evidence.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator RunArchive()
    {
        Record("Run archive smoke: real uGUI clicks, controlled outcomes, isolated Archive. Not natural victory/balance.");
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var ui = FindAnyObjectByType<MissionUI>();
        var store = (PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null);
        try
        {
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.MainMenu, "Main Menu", 3);
            yield return Frames(4);
            Check("Empty Archive shows placeholders", PoliceArchive.Current.RunRecords.Count == 0 && StrictText("Value_OFFICER #") == "—", "no fake history");

            // Defeat run.
            yield return ClickSessionButton("StartMission");
            var officer = flow.Officer;
            Check("START MISSION generates one officer", officer != null && officer.OfficerNumber == 1 && officer.Age >= 22 && officer.Age <= 55
                && officer.YearsOfService >= 1 && officer.YearsOfService <= officer.Age - 21 && OfficerProfile.TempRanks.Contains(officer.Rank)
                && officer.MissionsCompleted >= 0 && officer.MissionSuccessRate >= 0 && officer.MissionSuccessRate <= 100,
                $"#{officer?.OfficerNumber} age={officer?.Age} rank={officer?.Rank} years={officer?.YearsOfService} missions={officer?.MissionsCompleted} rate={officer?.MissionSuccessRate}%");
            flow.TrySetDraftSlot(LoadoutSlot.Torso, LoadoutRules.TorsoSlow);
            yield return ClickSessionButton("BeginOperation");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "first combat", 3);
            var f = flow.ActiveFight;
            Check("Combat entry creates no record and keeps officer", PoliceArchive.Current.RunRecords.Count == 0 && ReferenceEquals(flow.Officer, officer),
                "officer profile stable across deployment");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION FAILED", 3);
            yield return Frames(3);
            var defeat = PoliceArchive.Current.LatestRun;
            Check("Defeat creates exactly one record", PoliceArchive.Current.RunRecords.Count == 1 && !defeat.Success
                && defeat.OfficerNumber == officer.OfficerNumber && defeat.Age == officer.Age && defeat.Rank == officer.Rank,
                "officer frozen into record");
            Check("Defeat record holds snapshot and frozen stats", defeat.SlotItemId(LoadoutSlot.Torso) == LoadoutRules.TorsoSlow
                && defeat.SlotItemId(LoadoutSlot.Weapon) == LoadoutRules.AssaultRifle && defeat.BodyModificationPercent == f.Deployment.BodyModificationPercent
                && defeat.CombatDuration == f.CombatDuration && defeat.BossHealth == f.ResultBossHealth && defeat.PlayerHealth == f.ResultPlayerHealth
                && defeat.AccuracyPercent == f.AccuracyPercent, $"body={defeat.BodyModificationPercent}% time={defeat.CombatDuration:0.00}");
            Check("Defeat grants no achievement/ending", !PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory)
                && PoliceArchive.Current.UnlockedEndings.Count == 0, "defeat path");
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            Check("Main Menu shows latest officer/build", StrictText("Value_OFFICER #") == "1" && StrictText("Value_STATUS") == "MISSION FAILED"
                && StrictText("Value_TORSO") == LoadoutRules.ItemLabel(LoadoutRules.TorsoSlow) && StrictText("Value_WEAPON") == "Assault Rifle",
                "latest real run");
            yield return ClickSessionButton("PoliceArchive");
            Check("Police Records shows saved record", GameObject.Find("Records").transform.childCount == 1
                && FindObjectsByType<ScrollRect>().Length == 1, "one card in one vertical list");
            yield return ClickSessionButton("ArchiveBack");

            // Victory run at 0%; a later loadout change must not alter the stored defeat record.
            yield return ClickSessionButton("StartMission");
            Check("Next officer number is sequential", flow.Officer.OfficerNumber == 2, "derived from Archive history");
            yield return ClickSessionButton("BeginOperation");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "second combat", 3);
            f = flow.ActiveFight;
            f.BossHealth.TakeDamage(10000, f.Player.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION SUCCESS", 3);
            yield return Frames(3);
            var victory = PoliceArchive.Current.LatestRun;
            Check("Victory adds exactly one record", PoliceArchive.Current.RunRecords.Count == 2 && victory.Success && victory.OfficerNumber == 2
                && PoliceArchive.Current.RunRecords[0].SlotItemId(LoadoutSlot.Torso) == LoadoutRules.TorsoSlow, "earlier record unchanged");
            Check("Victory keeps TEMP achievement and Drone unlock", PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory)
                && PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm), "existing placeholder reward");
            Check("Victory routes one TEMP ending", PoliceArchive.Current.UnlockedEndings.SequenceEqual(new[] { PoliceArchive.HumanEnding })
                && victory.BodyModificationPercent == 0, "0% < 50% TEST threshold");
            yield return ClickSessionButton("ResultContinue");
            yield return ClickSessionButton("PoliceArchive");
            StrictRecordsAudit();
            Check("Police Records newest first", GameObject.Find("Records").transform.childCount == 2
                && GameObject.Find("Record0").GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Value_STATUS" && t.text == "MISSION SUCCESS"), "two cards");
            yield return ClickSessionButton("ArchiveTab2");
            Check("Case Closure shows TEMP ending IDs", StrictTexts().Contains(PoliceArchive.HumanEnding) && StrictTexts().Length == 5, "IDs only");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/run-archive-case-closure.png");
            yield return ClickSessionButton("ArchiveTab0");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/run-archive-records.png");
            yield return Frames(2);
            yield return ClickSessionButton("ArchiveBack");

            var reloaded = new PoliceArchiveStore(store.FilePath).Load();
            Check("Archive reload keeps everything", reloaded.RunRecords.Count == 2 && reloaded.HasAchievement(PoliceArchive.TempFirstVictory)
                && reloaded.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm) && reloaded.UnlockedEndings.Contains(PoliceArchive.HumanEnding)
                && reloaded.RunRecords[0].SlotItemId(LoadoutSlot.Torso) == LoadoutRules.TorsoSlow && reloaded.RunRecords[1].Success, store.FilePath);

            string v1 = Path.Combine(Path.GetDirectoryName(store.FilePath), "v1-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(v1, "{\"saveVersion\":1,\"unlockedEndings\":[\"OLD_ENDING\"],\"completedAchievements\":[\"TEMP_FIRST_VICTORY\"],\"unlockedHiddenAugmentations\":[\"detached_drone_arm\"]}");
            var v1Store = new PoliceArchiveStore(v1);
            var migrated = v1Store.Load();
            bool saved = v1Store.TrySave(migrated, out string error);
            var v2 = new PoliceArchiveStore(v1).Load();
            Check("Version-1 Archive migrates", v1Store.LastLoadStatus == PoliceArchiveLoadStatus.Loaded && saved && migrated.RunRecords.Count == 0
                && v2.UnlockedEndings[0] == "OLD_ENDING" && v2.HasAchievement(PoliceArchive.TempFirstVictory) && v2.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
                && File.ReadAllText(v1).Contains("\"saveVersion\": 2"), error ?? "v1 data kept, empty history, saved as v2");
        }
        finally
        {
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
