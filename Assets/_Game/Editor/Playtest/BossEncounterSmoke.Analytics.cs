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
    [MenuItem("GO GHOST/Playtest/Analytics")]
    public static void RunAnalytics() => Start("analytics", false);
    static PlayMetrics.OperationStats Metric(ControlOperation op) => PlayMetrics.Current.operations[(int)op];
    static IEnumerator Analytics()
    {
        Record("Analytics: real executor events, virtual input, controlled placements and deaths; not balance evidence.");
        game.NewGame();game.StartConfiguredEncounter();yield return Frames(6);
        mouseHeld=true;heldKeys=new[]{Key.Space,Key.D};yield return Seconds(.25f);
        heldKeys=Array.Empty<Key>();mouseHeld=false;
        Check("Training excluded",game.Phase==DuelPhase.Training&&PlayMetrics.Current==null&&PlayMetrics.Run.attempts.Count==0);
        game.ContinueFromTraining();yield return Frames(3);
        game.StartFight();yield return Frames(3);
        Check("Preparation has no action samples",!PlayMetrics.Counting&&PlayMetrics.Current.operations.All(o=>o.automaticCount+o.manualCount==0&&o.automaticSeconds+o.manualSeconds==0));
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();PlayMetrics.MarkTestData();
        AlphaPlace(-5,10);yield return Seconds(.15f);
        heldKeys=new[]{Key.D};yield return Seconds(.25f);heldKeys=Array.Empty<Key>();yield return Frames(3);
        heldKeys=new[]{Key.Space};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Seconds(.1f);
        heldKeys=new[]{Key.LeftShift};yield return Frames(3);heldKeys=Array.Empty<Key>();
        TurningCursor(1,true);yield return Seconds(.6f);mouseHeld=false;yield return Frames(3);
        Check("Manual real jump, dodge, fire counted",Metric(ControlOperation.Jump).manualCount==1&&Metric(ControlOperation.Dodge).manualCount==1&&Metric(ControlOperation.Fire).manualCount==game.Rifle.ShotsFired&&game.Rifle.ShotsFired>0);
        Check("Manual movement and aim durations",Metric(ControlOperation.Movement).manualSeconds>.1f&&Metric(ControlOperation.Aim).manualSeconds>.5f);
        Check("Manual run has no auto credit",PlayMetrics.Current.operations.All(o=>o.automaticCount==0&&o.automaticSeconds==0));
        PlayMetrics.Calculate(PlayMetrics.Current.operations,out bool valid,out float pct);
        Check("Manual automation is zero",valid&&pct==0);
        checkingPause=true;yield return DeveloperKey(Key.F8);
        float clock=game.FightClock,aimSeconds=Metric(ControlOperation.Aim).manualSeconds;
        yield return RealSeconds(.2);
        Check("F8 pauses clock and metrics",game.DeveloperToolsOpen&&game.Paused&&game.FightClock==clock&&Metric(ControlOperation.Aim).manualSeconds==aimSeconds);
        ClickConfig("Analytics");yield return Frames(3);
        Check("Analytics has five operations and no F1 overlay",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Automation  0.0%")&&GameObject.Find("Metrics")==null);
        yield return DeveloperKey(Key.F1);
        Check("F1 has no effect",game.DeveloperToolsOpen&&GameObject.Find("Metrics")==null);
        SetAlphaGameSize(1280,720);yield return Frames(8);ScreenCapture.CaptureScreenshot(Output+"analytics-fight-1280.png");yield return Frames(3);
        CheckAnalyticsText("Fight 1280");
        ClickConfig("Actions");SetAlphaGameSize(1920,1080);yield return Frames(8);ScreenCapture.CaptureScreenshot(Output+"analytics-actions-1920.png");yield return Frames(3);
        CheckAnalyticsText("Actions 1920");
        ClickConfig("Tuning");ClickConfig("Health");yield return Frames(3);
        Check("Tuning preserved",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_InputField>().Length==6);
        ClickConfig("Player");game.SetDeveloperValue(DeveloperSetting.MoveSpeed,8);
        Check("Tuning tagged and recorded",PlayMetrics.Current.tuningChanged&&PlayMetrics.Current.finalTuning[0]==8);
        game.ResetDeveloperTuning();yield return DeveloperKey(Key.F8);checkingPause=false;
        game.Player.TakeDamage(10000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,.5f);
        Check("Death saved exactly once",PlayMetrics.Current==null&&PlayMetrics.Run.attempts.Count==1&&PlayMetrics.Run.deathsByWave[1]==1);
        CheckMetricsFiles("manual");
        game.Reconfigure();game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();PlayMetrics.MarkTestData();
        Check("Retry retains run and fresh counts",PlayMetrics.Run.attempts.Count==1&&Metric(ControlOperation.Fire).manualCount==0);
        checkingPause=true;yield return DeveloperKey(Key.F8);ClickConfig("Waves");ClickConfig("Dev_Wave2");checkingPause=false;yield return Frames(4);
        Check("Wave jump records abandoned old attempt",PlayMetrics.Run.attempts.Count==2&&PlayMetrics.Run.attempts[1].result=="abandoned"&&PlayMetrics.Current.developerWaveJump&&PlayMetrics.Current.testData&&game.Wave==2);
        game.MainMenu();CheckMetricsFiles("abandoned");

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="M04",ShortMoveDirection=ShortMoveDirectionOption.Forward});
            V2Rule(d,new ConditionalRule{Condition=17,Action="J01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="Q01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F02"});
            V2Rule(d,new ConditionalRule{Condition=57,Action="D03"});
        });
        PlayMetrics.MarkTestData();AlphaPlace(-5,10);yield return Seconds(.15f);TurningCursor(1,true);
        yield return Until(()=>game.Rifle.ShotsFired>=1,.6f);mouseHeld=false;yield return Seconds(2.2f);
        Check("Auto jump and event Dodge recorded",Metric(ControlOperation.Jump).automaticCount==1&&Metric(ControlOperation.Dodge).automaticCount==1,
            "jump="+Metric(ControlOperation.Jump).automaticCount+" dodge="+Metric(ControlOperation.Dodge).automaticCount);
        Check("Six-round Burst retains automatic provenance after trigger release",Metric(ControlOperation.Fire).automaticCount==6&&Metric(ControlOperation.Fire).manualCount==0&&game.Rifle.ShotsFired==6);
        Check("Automatic aim then manual fallback are both measured",Metric(ControlOperation.Aim).automaticSeconds>0&&Metric(ControlOperation.Aim).manualSeconds>0);
        Check("Action detail uses actual action IDs",Metric(ControlOperation.Fire).actions.Any(a=>a.action=="F02"&&a.count==6));
        PlayMetrics.Calculate(PlayMetrics.Current.operations,out valid,out pct);
        var used=PlayMetrics.Current.operations.Where(o=>o.hasData).ToArray();
        Check("Total percentage uses equal nonempty operation mean",valid&&Mathf.Abs(pct-used.Average(o=>o.automaticPercent))<.001f);
        game.MainMenu();CheckMetricsFiles("automatic");

        yield return AlphaFresh(1,d=>{V2Rule(d,V2Ground("M10"));V2Rule(d,V2Ground("F06"));});
        PlayMetrics.MarkTestData();AlphaPlace(-5,10);yield return Seconds(.15f);
        float moveHold=Metric(ControlOperation.Movement).automaticHoldSeconds;
        heldKeys=new[]{Key.D};TurningCursor(1,true);yield return Seconds(.4f);heldKeys=Array.Empty<Key>();mouseHeld=false;
        Check("Holds measure control time without fake movement or shots",Metric(ControlOperation.Movement).automaticHoldSeconds>moveHold+.2f
            &&Metric(ControlOperation.Fire).automaticHoldSeconds>.2f&&Metric(ControlOperation.Fire).automaticCount==0&&Metric(ControlOperation.Fire).manualCount==0
            &&Metric(ControlOperation.Movement).automaticSeconds==0&&Metric(ControlOperation.Movement).manualSeconds==0);
        PlayMetrics.Calculate(PlayMetrics.Current.operations,out valid,out pct);
        Check("Unused operations N/A and holds excluded",!Metric(ControlOperation.Fire).hasData&&!Metric(ControlOperation.Movement).hasData&&pct==0);
        game.MainMenu();CheckMetricsFiles("holds");

        yield return AlphaFresh(1,d=>V2Rule(d,V2Ground("Q01")));
        PlayMetrics.MarkTestData();AlphaPlace(-5,10);yield return Seconds(.1f);
        // Manual move/shot remain manual even when Arms' Aim is automatic.
        heldKeys=new[]{Key.D};TurningCursor(1,true);yield return Seconds(.3f);heldKeys=Array.Empty<Key>();mouseHeld=false;
        Check("Independent operations: auto Aim does not claim manual Fire or Move",Metric(ControlOperation.Aim).automaticSeconds>0&&Metric(ControlOperation.Fire).manualCount>0&&Metric(ControlOperation.Fire).automaticCount==0&&Metric(ControlOperation.Movement).manualSeconds>0);
        game.MainMenu();CheckMetricsFiles("mixed");

        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,null,false);PlayMetrics.MarkTestData();yield return Seconds(.6f);
            Check("Live wave "+wave+" advances with metrics",game.Phase==DuelPhase.Fight&&game.FightClock>.5f&&Metric(ControlOperation.Aim).manualSeconds>0);
            if(wave==3)
            {
                foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,game.Player.gameObject);
                yield return Until(()=>game.Phase==DuelPhase.Complete,.6f);
                Check("Controlled completion produces clear summary",PlayMetrics.Run.levelCleared&&PlayMetrics.Run.attempts.Count==1&&PlayMetrics.Run.attempts[0].result=="won");
                CheckMetricsFiles("clear");
            }
        }
        checkingPause=true;yield return DeveloperKey(Key.F8);ClickConfig("Analytics");ClickConfig("Run");
        SetAlphaGameSize(1920,1080);yield return Frames(8);ScreenCapture.CaptureScreenshot(Output+"analytics-run-1920.png");yield return Frames(3);CheckAnalyticsText("Run 1920");
        ClickConfig("History");yield return Frames(3);CheckAnalyticsText("History 1920");
        game.MainMenu();checkingPause=false;yield return Frames(3);
        Check("Cleanup releases all bindings and pause",PlayMetrics.Current==null&&!PlayMetrics.Counting&&game.Player==null&&Time.timeScale==1);
        Record("NOT TESTED: physical keyboard/mouse, standalone, full natural runs, balance or other PCs.");
    }
    static void CheckMetricsFiles(string label)
    {
        Check(label+" JSON saved without CSV",File.Exists(PlayMetrics.JsonPath)&&!File.Exists(PlayMetrics.CsvPath)&&string.IsNullOrEmpty(PlayMetrics.LastSaveError),PlayMetrics.LastSaveError);
        var read=JsonUtility.FromJson<PlayMetrics.RunData>(File.ReadAllText(PlayMetrics.JsonPath));
        Check(label+" JSON round trip",read.runId==PlayMetrics.Run.runId&&read.attempts.Count==PlayMetrics.Run.attempts.Count&&Mathf.Abs(read.combatSeconds-PlayMetrics.Run.combatSeconds)<.001f);
        Record("ANALYTICS_FILE "+PlayMetrics.JsonPath);
    }
    static void CheckAnalyticsText(string label)
    {
        Canvas.ForceUpdateCanvases();
        var texts=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_Text>();
        foreach(var t in texts)t.ForceMeshUpdate();
        var overflow=texts.Where(t=>t.isTextOverflowing).Select(t=>t.text).ToArray();
        Check(label+" text fits",overflow.Length==0,string.Join(" | ",overflow));
    }
}
#endif
