#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Help and Named Metrics")]
    public static void RunHelpMetrics() => Start("help-metrics", false);
    static PlayMetrics.RunData ReadNamedRun(string path) => JsonUtility.FromJson<PlayMetrics.RunData>(File.ReadAllText(path));
    static IEnumerator HelpMetrics()
    {
        Record("Named runs, help UI, existing physical executors and controlled lifecycle. No balance or physical-input claim.");
        const string oldProgress="GO_GHOST.Presets.UnlockedCount";
        bool hadProgress=PlayerPrefs.HasKey(oldProgress); int progress=PlayerPrefs.GetInt(oldProgress);
        game.MainMenu(); game.NewGame(); PlayMetrics.MarkTestData(); yield return Frames(2);
        string firstPath=PlayMetrics.JsonPath, firstId=PlayMetrics.Run.runId;
        Check("New Game immediately saves an empty record", File.Exists(firstPath)&&ReadNamedRun(firstPath).attempts.Count==0);
        var name=Object.FindAnyObjectByType<DuelUI>().GetComponentInChildren<TMP_InputField>();
        name.text="同名 <A> \"test\""; ClickConfig("CreateCharacterContinue"); yield return Frames(3);
        Check("Nickname is stored verbatim as data",ReadNamedRun(firstPath).playerName==name.text&&PlayMetrics.Run.characterCreated);
        var ui=Object.FindAnyObjectByType<DuelUI>();
        ClickConfig("AddRule"); yield return Frames(2);
        var pending=Private<ConditionalRule>(ui,"pendingRule");
        ClickConfig("ConfigurationHelp"); SetAlphaGameSize(1280,720); yield return Frames(8);
        Check("Controls title and no preset UI",ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="What're the controls?")
            &&!ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Presets"||t.text=="Apply"));
        ScreenCapture.CaptureScreenshot(Output+"help-controls-1280.png"); yield return Frames(3);
        ClickConfig("HelpTab1"); yield return Frames(5);
        Check("Configuration help defines Condition and Action only",ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="What is configuration?")
            &&ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Condition: when to act.\nAction: what to do.")
            &&!ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("(AND)")));
        ScreenCapture.CaptureScreenshot(Output+"help-configuration-1280.png"); yield return Frames(3);
        var scroll=Private<UnityEngine.UI.ScrollRect>(ui,"helpScroll"); scroll.verticalNormalizedPosition=0;
        yield return Frames(3); ScreenCapture.CaptureScreenshot(Output+"help-configuration-bottom.png"); yield return Frames(3);
        ClickConfig("HelpBack"); yield return Frames(3);
        Check("Help returns to the same draft without applying rules",ReferenceEquals(pending,Private<ConditionalRule>(ui,"pendingRule"))&&game.DraftLoadout.TotalRules==0&&PlayMetrics.Run.helpOpened==1);
        ClickConfig("CancelRuleEdit"); yield return Frames(2);
        Check("Cancelled draft does not count as a rule",PlayMetrics.Run.rulesAdded==0);
        ClickConfig("AddRule"); yield return Frames(2); yield return V2UISelect(17); yield return V2UIAction("F03");
        Check("Actual rule commitment counted",game.DraftLoadout.TotalRules==1&&PlayMetrics.Run.rulesAdded==1&&PlayMetrics.Run.firstRuleAtSeconds>=0);
        ClickConfig("Rule0Action"); yield return Frames(2); yield return V2UIAction("F01");
        Check("Actual rule edit counted",PlayMetrics.Run.rulesEdited==1&&game.DraftLoadout.OrderedRules()[0].Action=="F01");
        ClickConfig("AddRule"); yield return Frames(2); yield return V2UISelect(17); yield return V2UIAction("F03");
        ClickConfig("MoveRuleUp1"); yield return Frames(2); ClickConfig("RemoveRule1"); yield return Frames(2);
        Check("Reorder/removal and highest rule count recorded",PlayMetrics.Run.rulesReordered==1&&PlayMetrics.Run.rulesRemoved==1&&PlayMetrics.Run.maxRulesEquipped==2);
        game.StartConfiguredEncounter(); yield return Frames(5);
        yield return Seconds(.25f);
        Check("Training has time but no numbered attempt",game.Phase==DuelPhase.Training&&PlayMetrics.Run.trainingSeconds>.1&&PlayMetrics.Current==null);
        game.Player.IsInvulnerable=false; game.Player.TakeDamage(100000,null); yield return Frames(5);
        Check("Training death counted once without unlocking",PlayMetrics.Run.trainingDeaths==1&&PlayerPrefs.HasKey(oldProgress)==hadProgress&&PlayerPrefs.GetInt(oldProgress)==progress);
        game.MainMenu(); yield return Frames(2);
        var training=ReadNamedRun(firstPath);
        Check("Training-only run persists its rules and normal end",training.finished&&training.endReason=="main_menu"&&training.attempts.Count==0&&training.latestRules.Length==1&&!File.Exists(PlayMetrics.CsvPath));
        Check("Real-time stage accounting is mutually exclusive",Math.Abs(training.playSeconds-(training.creationSeconds+training.configurationSeconds+training.trainingSeconds+training.preparationSeconds+training.fightSeconds+training.pauseSeconds+training.resultSeconds))<.01);
        string oldBytes=File.ReadAllText(firstPath);

        game.NewGame(); game.Appearance.Name=training.playerName; game.CompleteCharacterCreation(); PlayMetrics.MarkTestData();
        string secondPath=PlayMetrics.JsonPath;
        Check("Same nickname creates independent New Game record",PlayMetrics.Run.runId!=firstId&&PlayMetrics.JsonPath!=firstPath&&PlayMetrics.Run.playerName==training.playerName&&PlayMetrics.Run.rulesAdded==0);
        game.StartFight(); yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f); StopPolicies(); PlayMetrics.MarkTestData();
        AlphaPlace(-5,10); yield return Seconds(.1f);
        heldKeys=new[]{Key.D}; TurningCursor(1,true); yield return Seconds(.55f); heldKeys=Array.Empty<Key>(); mouseHeld=false;
        Check("Real manual shots and movement measured",Metric(ControlOperation.Fire).manualCount>0&&Metric(ControlOperation.Movement).manualSeconds>0);
        checkingPause=true; game.SetPause(true); float clock=game.FightClock; double paused=PlayMetrics.Run.pauseSeconds;
        yield return RealSeconds(.25);
        Check("Pause freezes combat and records real pause time",game.FightClock==clock&&PlayMetrics.Run.pauseSeconds>paused+.1);
        game.ToggleDeveloperTools(); ClickConfig("Analytics"); ClickConfig("Run"); SetAlphaGameSize(1920,1080); yield return Frames(6);
        Check("F8 has named run summary and no Presets",ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==training.playerName&&!t.richText)
            &&!ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Presets"));
        ScreenCapture.CaptureScreenshot(Output+"named-metrics-run-1920.png"); yield return Frames(3);
        typeof(PlayMetrics).GetField("lastSave",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,Time.realtimeSinceStartupAsDouble-11);
        yield return Frames(3);
        var checkpoint=ReadNamedRun(secondPath);
        Check("Periodic checkpoint saves active attempt and HP",!checkpoint.finished&&checkpoint.activeAttempt!=null&&checkpoint.activeAttempt.started&&checkpoint.activeAttempt.playerMaxHealth>0&&checkpoint.activeAttempt.gunnerMaxHealth>0);
        game.ToggleDeveloperTools(); game.SetPause(false); checkingPause=false;
        game.Player.IsInvulnerable=false; game.Player.TakeDamage(100000,null); yield return Until(()=>game.Phase==DuelPhase.Failed,.5f);
        Check("Death and endpoint health finalized once",PlayMetrics.History.Count==1&&PlayMetrics.Run.deathsByWave[1]==1&&PlayMetrics.History[0].playerHealth==0&&PlayMetrics.Current==null);
        game.Reconfigure(); game.StartFight(); yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f); StopPolicies();
        Check("Retry keeps nickname and run, gets fresh attempt",PlayMetrics.JsonPath==secondPath&&PlayMetrics.Run.playerName==training.playerName&&PlayMetrics.History.Count==1);
        game.MainMenu();
        Check("Abandoned retry records reason and no extra death",PlayMetrics.History.Count==2&&PlayMetrics.History[1].endReason=="main_menu"&&PlayMetrics.Run.deathsByWave[1]==1);
        Check("Earlier named record stays unchanged",File.ReadAllText(firstPath)==oldBytes);

        yield return AlphaFresh(1,d=>V2Rule(d,V2Ground("Q06"))); PlayMetrics.MarkTestData(); AlphaPlace(-5,5); yield return Frames(2);
        // Keep the virtual cursor a few degrees off the live body centre, inside the assist cone.
        MouseAt((Vector2)game.Bosses[0].Body.bounds.center+Vector2.up*.6f,false); yield return Seconds(.3f);
        Check("Aim Assist is distinct from full automatic aim",Metric(ControlOperation.Aim).assistedSeconds>0&&Metric(ControlOperation.Aim).automaticSeconds==0);
        game.MainMenu(); game.NewGame(); PlayMetrics.MarkTestData(); game.Appearance.Name="Skip check"; ReminderDue(); yield return Frames(3);
        ClickConfig("PlaytestYes"); yield return Frames(3);
        Check("Skip before first fight has an explicit record",PlayMetrics.Run.waveOneSkipped&&PlayMetrics.Run.playtestReminder=="yes"&&PlayMetrics.History.Count==0&&game.Wave==2&&!PlayMetrics.Current.developerWaveJump);
        game.SetPause(false); yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f); yield return Seconds(.25f);
        Check("W2 after skip advances",game.Phase==DuelPhase.Fight&&game.FightClock>.2f);
        game.MainMenu();
        yield return AlphaFresh(2,null,false); PlayMetrics.MarkTestData(); yield return Seconds(.25f);
        Check("W2 advances with Swordsman health snapshot",game.Phase==DuelPhase.Fight&&game.FightClock>.2f&&PlayMetrics.Current.swordMaxHealth>0&&PlayMetrics.Current.gunnerMaxHealth<0);
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.FinalBossReady,.6f);
        var complete=ReadNamedRun(PlayMetrics.JsonPath);
        Check("Controlled W2 completion closes run with Swordsman endpoint",game.Phase==DuelPhase.FinalBossReady&&complete.finished&&complete.endReason=="complete"
            &&complete.levelCleared&&complete.attempts.Last().wave==2&&complete.attempts.Last().swordHealth==0&&complete.activeAttempt.result=="none");
        Check("Single JSON export and schema 4",complete.schemaVersion==4&&!File.Exists(PlayMetrics.CsvPath)&&string.IsNullOrEmpty(PlayMetrics.LastSaveError));
        Record("Named evidence files: "+firstPath+" ; "+secondPath);
        game.MainMenu(); yield return Frames(3);
    }
}
#endif
