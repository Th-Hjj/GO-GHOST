#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Focused Archive check: Police Records session reset, the two fixed ending cards and MARKSMAN PROTOCOL with its
/// Drone reward. Isolated Archive files only. Defeats are real flow with a forced death; the accuracy and ending cases
/// feed frozen records through the real PoliceArchive.RecordCompletedRun, so they prove the rule, not a real fight.
/// </summary>
public partial class TopDownSmokeTest
{
    private static string[] EndingCardTexts(int card) =>
        GameObject.Find("Content_EndingCard" + card).GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();

    private static bool EndingCardShows(int card, string endingId)
    {
        var texts = EndingCardTexts(card);
        return texts.Length == 2 && texts[0] == PoliceArchive.EndingTitle(endingId) && texts[1] == PoliceArchive.EndingText(endingId);
    }

    private static bool EndingCardsEqualSize() =>
        ((RectTransform)GameObject.Find("Content_EndingCard0").transform).sizeDelta == ((RectTransform)GameObject.Find("Content_EndingCard1").transform).sizeDelta;

    private static bool NoHiddenNames() => !StrictTexts().Any(t => t.Contains("TEMP") || t.ToUpperInvariant().Contains("DRONE")
        || t.Contains("LOCKED") || t.Contains("???") || t.ToUpperInvariant().Contains("DETACHED"));

    private static DeploymentSnapshot ArchiveFinalDeployment(bool cyberized)
    {
        var draft = new LoadoutDraft(true);
        if (cyberized)
            for (int slot = 0; slot < (int)LoadoutSlot.Weapon && draft.BodyModificationPercent < PoliceArchive.EndingThresholdPercent; slot++)
                foreach (var definition in LoadoutRules.PrototypeCatalog)
                    if (definition.Occupies((LoadoutSlot)slot) && LoadoutRules.IsOffered((LoadoutSlot)slot, definition.ItemId)
                        && draft.TrySetSlot((LoadoutSlot)slot, definition.ItemId)) break;
        return draft.Deploy(1);
    }

    private static PoliceRunRecord ArchiveFinalRecord(bool success, bool cyberized, float accuracy)
    {
        var run = new PoliceRunRecord(OfficerProfile.Generate(PoliceArchive.Current.NextOfficerNumber), ArchiveFinalDeployment(cyberized),
            success, 60f, success ? 0 : 300, success ? 80 : 0, accuracy);
        PoliceArchive.RecordCompletedRun(run, out _);
        return run;
    }

    // The same per-session load the game runs once at launch, pointed at an isolated file.
    private void ArchiveFinalRestart(string path)
    {
        var store = new PoliceArchiveStore(path);
        typeof(PoliceArchive).GetField("store", ArchiveFields).SetValue(null, store);
        typeof(PoliceArchive).GetField("current", ArchiveFields).SetValue(null, PoliceArchive.BeginSession(store));
    }

    private IEnumerator ArchiveFinalTab(int tab)
    {
        yield return ClickSessionButton("PoliceArchive");
        yield return Frames(3);
        if (tab > 0) { yield return ClickSessionButton("ArchiveTab" + tab); yield return Frames(3); }
    }

    private IEnumerator ArchiveFinalShot(string name)
    {
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/archive-final-" + name + ".png");
        yield return Frames(3);
    }

    private IEnumerator ArchiveFinal()
    {
        Record($"Archive final: session reset, ending cards, MARKSMAN PROTOCOL at {Screen.width}x{Screen.height}. Isolated Archive; forced deaths; synthetic frozen records for accuracy / ending cases.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var store = (PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null);
        string path = store.FilePath;
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // ---------------- Police Records: one session ----------------
            Check("Fresh launch: 0 records, next officer #1", PoliceArchive.Current.RunRecords.Count == 0 && PoliceArchive.Current.NextOfficerNumber == 1
                && PoliceArchive.Current.LatestRun == null && flow.Officer.OfficerNumber == 1, $"deployed officer #{flow.Officer.OfficerNumber}");
            yield return TerminalDefeat(flow);
            bool one = PoliceArchive.Current.RunRecords.Count == 1 && PoliceArchive.Current.RunRecords[0].OfficerNumber == 1;
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            string previous = string.Join("|", GameObject.Find("PreviousOfficer").GetComponentsInChildren<TMP_Text>().Select(t => t.text));
            Check("First defeat: 1 record; Main Menu shows that officer", one && flow.State == GameSessionFlow.FlowState.MainMenu && previous.Contains("MISSION FAILED"), previous);

            yield return ArchiveFinalTab(1);
            var intel = StrictTexts();
            Check("Mission Intelligence: MARKSMAN PROTOCOL, exact condition, INCOMPLETE; nothing else named", intel.Contains(PoliceArchive.MarksmanProtocolTitle)
                && intel.Contains("Defeat the Cyber Psycho with at least 70% accuracy.") && intel.Contains("INCOMPLETE") && !intel.Contains("COMPLETED")
                && NoHiddenNames() && OverflowingTexts() == "", string.Join(" / ", intel));
            yield return ArchiveFinalShot("intel-incomplete");
            yield return ClickSessionButton("ArchiveTab2");
            yield return Frames(3);
            Check("No endings: two blank cards of equal size, no ending title or text", EndingCardTexts(0).Length == 0 && EndingCardTexts(1).Length == 0
                && EndingCardsEqualSize() && NoHiddenNames() && !StrictTexts().Any(t => t.Contains("ENDING")), "cards only");
            yield return ArchiveFinalShot("cards-blank");
            yield return ClickSessionButton("ArchiveBack");

            yield return ClickSessionButton("StartMission");
            yield return Frames(2);
            Check("START MISSION keeps the record; next officer #2", PoliceArchive.Current.RunRecords.Count == 1 && flow.Officer.OfficerNumber == 2
                && PoliceArchive.Current.NextOfficerNumber == 2, "records=1");
            yield return ClickSessionButton("Region_LEFT_ARM");
            bool droneHidden = !ChoiceLabels().Contains("Detached Drone Arm");
            yield return ClickSessionButton("BeginOperation");
            yield return TerminalDefeat(flow);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            Check("Second attempt: 2 records, officers #1 and #2; nothing unlocked by defeats", PoliceArchive.Current.RunRecords.Count == 2
                && PoliceArchive.Current.RunRecords[1].OfficerNumber == 2 && droneHidden && PoliceArchive.Current.CompletedAchievements.Count == 0
                && PoliceArchive.Current.UnlockedHiddenAugmentations.Count == 0 && PoliceArchive.Current.UnlockedEndings.Count == 0, "defeats only");

            // ---------------- MARKSMAN PROTOCOL and ending cards (frozen records through the real resolver) ----------------
            ArchiveFinalRecord(false, false, 100f);
            Check("Defeat at 100% accuracy unlocks nothing", !PoliceArchive.Current.HasAchievement(PoliceArchive.MarksmanProtocol)
                && !PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm) && PoliceArchive.Current.UnlockedEndings.Count == 0, "Success=false");
            var human = ArchiveFinalRecord(true, false, 69.9f);
            yield return ArchiveFinalTab(1);
            bool incomplete = StrictTexts().Contains("INCOMPLETE") && !StrictTexts().Contains("COMPLETED");
            yield return ClickSessionButton("ArchiveTab2");
            yield return Frames(3);
            Check("Victory at 69.9%: INCOMPLETE, no Drone; only the Human card shows its title and exact text", incomplete
                && !PoliceArchive.Current.HasAchievement(PoliceArchive.MarksmanProtocol) && !PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
                && EndingCardShows(0, PoliceArchive.HumanEnding) && EndingCardTexts(1).Length == 0 && OverflowingTexts() == "",
                $"body {human.BodyModificationPercent:0}% / {string.Join(" | ", EndingCardTexts(0))}");
            yield return ArchiveFinalShot("cards-human");
            yield return ClickSessionButton("ArchiveBack");

            var ai = ArchiveFinalRecord(true, true, 70f);
            var disk = new PoliceArchiveStore(path).Load();
            Check("Victory at exactly 70.0%: COMPLETED and Drone unlock written by the same save", PoliceArchive.Current.HasAchievement(PoliceArchive.MarksmanProtocol)
                && PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm) && disk.HasAchievement(PoliceArchive.MarksmanProtocol)
                && disk.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm) && disk.UnlockedEndings.Contains(PoliceArchive.HumanEnding)
                && disk.UnlockedEndings.Contains(PoliceArchive.AiEnding) && !disk.HasAchievement(PoliceArchive.TempFirstVictory), $"body {ai.BodyModificationPercent:0}%; reloaded from {path}");
            ArchiveFinalRecord(true, true, 95f);
            Check("A later qualifying victory adds no duplicate", PoliceArchive.Current.CompletedAchievements.Count == 1
                && PoliceArchive.Current.UnlockedHiddenAugmentations.Count == 1 && PoliceArchive.Current.UnlockedEndings.Count == 2, "1 achievement / 1 hidden / 2 endings");
            yield return ArchiveFinalTab(1);
            intel = StrictTexts();
            Check("Mission Intelligence shows COMPLETED and never names the reward", intel.Contains("COMPLETED") && !intel.Contains("INCOMPLETE") && NoHiddenNames(), string.Join(" / ", intel));
            yield return ArchiveFinalShot("intel-completed");
            yield return ClickSessionButton("ArchiveTab2");
            yield return Frames(3);
            Check("Both endings: both cards show title and exact text", EndingCardShows(0, PoliceArchive.HumanEnding) && EndingCardShows(1, PoliceArchive.AiEnding)
                && EndingCardsEqualSize() && OverflowingTexts() == "", string.Join(" | ", EndingCardTexts(1)));
            yield return ArchiveFinalShot("cards-both");
            yield return ClickSessionButton("ArchiveBack");
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_LEFT_ARM");
            Check("Next Upgrade: Left Arm lists Detached Drone Arm", ChoiceLabels().Contains("Detached Drone Arm"), string.Join(", ", ChoiceLabels()));
            yield return ClickSessionButton("UpgradeBack");
            yield return Frames(2);

            // ---------------- Next application session on the same file ----------------
            int before = PoliceArchive.Current.RunRecords.Count;
            ArchiveFinalRestart(path);
            yield return ArchiveFinalTab(0);
            int cards = GameObject.Find("Records").transform.childCount;
            yield return ClickSessionButton("ArchiveBack");
            yield return Frames(3);
            previous = string.Join("|", GameObject.Find("PreviousOfficer").GetComponentsInChildren<TMP_Text>().Select(t => t.text));
            Check("Restart: records back to 0, officer #1, Main Menu previous officer empty, Police Records empty", before == 6 && PoliceArchive.Current.RunRecords.Count == 0
                && PoliceArchive.Current.NextOfficerNumber == 1 && cards == 0 && !previous.Contains("MISSION"), $"{before} -> 0; {previous}");
            Check("Restart keeps achievement, Drone and both endings", PoliceArchive.Current.HasAchievement(PoliceArchive.MarksmanProtocol)
                && PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm) && PoliceArchive.Current.UnlockedEndings.Count == 2, "unlock lists unchanged");
            yield return ClickSessionButton("StartMission");
            yield return Frames(2);
            bool numberOne = flow.Officer.OfficerNumber == 1;
            yield return ClickSessionButton("Region_LEFT_ARM");
            bool droneKept = ChoiceLabels().Contains("Detached Drone Arm");
            yield return ClickSessionButton("BeginOperation");
            yield return TerminalDefeat(flow);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            disk = new PoliceArchiveStore(path).Load();
            Check("First attempt of the new session: officer #1, 1 record on disk, unlocks still saved", numberOne && droneKept && disk.RunRecords.Count == 1
                && disk.RunRecords[0].OfficerNumber == 1 && disk.HasAchievement(PoliceArchive.MarksmanProtocol)
                && disk.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm) && disk.UnlockedEndings.Count == 2, "saved file after the reset");

            // ---------------- Only the AI ending, on an older save with legacy TEMP IDs ----------------
            string legacy = Path.Combine(Path.GetDirectoryName(path), "legacy", "archive.json");
            Phase9UseArchive(legacy);
            PoliceArchive.Current.CompleteAchievement(PoliceArchive.TempFirstVictory);
            PoliceArchive.Current.UnlockEnding("TEMP_ENDING_LOW_BODY");
            ArchiveFinalRecord(true, true, 50f);
            yield return ArchiveFinalTab(1);
            bool legacyIncomplete = StrictTexts().Contains("INCOMPLETE") && NoHiddenNames();
            yield return ClickSessionButton("ArchiveTab2");
            yield return Frames(3);
            Check("Only AI: Human card blank, AI card filled; legacy TEMP IDs fill nothing and do not complete MARKSMAN", legacyIncomplete
                && EndingCardTexts(0).Length == 0 && EndingCardShows(1, PoliceArchive.AiEnding) && NoHiddenNames()
                && !PoliceArchive.Current.HasAchievement(PoliceArchive.MarksmanProtocol) && !PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
                && PoliceArchive.Current.UnlockedEndings.Contains("TEMP_ENDING_LOW_BODY"), "TEMP ending still saved, not shown");
            yield return ArchiveFinalShot("cards-ai");
            yield return ClickSessionButton("ArchiveBack");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
