#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Analytics Edges")]
    public static void RunAnalyticsEdges() => Start("analytics-edges", false);
    static IEnumerator AnalyticsEdges()
    {
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Right});
            V2Rule(d,V2Ground("F06"));
        });
        AlphaPlace(-6,10);yield return Seconds(.15f);TurningCursor(1,true);yield return Seconds(.35f);mouseHeld=false;yield return Frames(4);
        Check("Automatic real movement has action provenance",Metric(ControlOperation.Movement).automaticSeconds>.2f&&Metric(ControlOperation.Movement).manualSeconds==0
            &&Metric(ControlOperation.Movement).actions.Any(a=>a.action=="M11"&&a.seconds>.2f));
        // Render frames may be shorter than one physics step. Flush the last automatic
        // displacement observation before taking the baseline for the manual phase.
        yield return Seconds(Time.fixedDeltaTime*3);
        float automatic=Metric(ControlOperation.Movement).automaticSeconds;
        heldKeys=new[]{Key.A};yield return Seconds(.25f);heldKeys=Array.Empty<Key>();yield return Frames(4);
        Check("Movement source switch preserves prior auto steps",Metric(ControlOperation.Movement).automaticSeconds==automatic&&Metric(ControlOperation.Movement).manualSeconds>.15f,
            "auto before="+automatic+" after="+Metric(ControlOperation.Movement).automaticSeconds+" manual="+Metric(ControlOperation.Movement).manualSeconds);
        AlphaPlace(game.ArenaBounds.max.x-.5f,-8);yield return Seconds(.15f);
        heldKeys=new[]{Key.D};yield return Seconds(.4f);float blocked=Metric(ControlOperation.Movement).manualSeconds;yield return Seconds(.25f);
        heldKeys=Array.Empty<Key>();
        Check("Wall-blocked movement adds no duration",Mathf.Abs(Metric(ControlOperation.Movement).manualSeconds-blocked)<.025f);
        game.MainMenu();CheckMetricsFiles("movement");

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="F01",AmmoUse=AmmoUseOption.KeepTwoRounds}));
        RuleCardsAmmo(2);TurningCursor(1,true);yield return Seconds(.55f);
        mouseBurstHeld=true;yield return Until(()=>game.Rifle.IsReloading,.4f);mouseHeld=mouseBurstHeld=false;yield return Frames(3);
        Check("Consumed Fire 1 Shot falls back to a fresh manual RMB; legacy reserve is ignored",Metric(ControlOperation.Fire).automaticCount==1&&Metric(ControlOperation.Fire).manualCount==1&&game.Rifle.ShotsFired==2);
        var movement=game.Player.GetComponent<PlayerMovement>();
        AlphaPlace(-5,10);yield return Seconds(.15f);heldKeys=new[]{Key.Space};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Seconds(.1f);
        int jumps=Metric(ControlOperation.Jump).manualCount;
        heldKeys=new[]{Key.Space};yield return Frames(3);heldKeys=Array.Empty<Key>();
        Check("Failed airborne Jump adds no event",jumps==1&&Metric(ControlOperation.Jump).manualCount==1);
        game.MainMenu();CheckMetricsFiles("fallback");

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="J04",PlatformDirection=PlatformDirectionOption.Lower}));
        movement=game.Player.GetComponent<PlayerMovement>();var tiers=BehaviorLeftTiers();var shape=game.Player.GetComponent<Collider2D>();
        float x=Mathf.Max(tiers[0].bounds.min.x,tiers[2].bounds.min.x)+shape.bounds.extents.x+.3f;
        PlatformPlace(tiers[2],x);yield return Seconds(.15f);TurningCursor(1,true);
        yield return Until(()=>movement.DropThroughCount>0,.5f);mouseHeld=false;yield return Seconds(.9f);
        Check("J04 lower records auto drop without fake jump",Metric(ControlOperation.Jump).automaticDrops==1&&Metric(ControlOperation.Jump).automaticCount==0
            &&Metric(ControlOperation.Jump).actions.Any(a=>a.action=="J04"&&a.drops==1));
        heldKeys=new[]{Key.S};yield return Until(()=>movement.DropThroughCount>1,.4f);heldKeys=Array.Empty<Key>();
        Check("Manual S drop stays separately manual",Metric(ControlOperation.Jump).manualDrops==1&&Metric(ControlOperation.Jump).manualCount==0);
        game.MainMenu();CheckMetricsFiles("drops");

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="J04",PlatformDirection=PlatformDirectionOption.Higher}));
        tiers=BehaviorLeftTiers();PlatformPlace(tiers[0],tiers[0].bounds.center.x);yield return Seconds(.15f);TurningCursor(1,true);
        yield return Until(()=>game.Rules.PlatformTransitionsCompleted>0,2.5f);mouseHeld=false;yield return Frames(3);
        Check("J04 higher reaches platform with automatic jump",game.Rules.PlatformTransitionsCompleted>0&&Metric(ControlOperation.Jump).automaticCount>0);
        // Offset supports may require movement. Any recorded transfer movement must carry J04, never Manual.
        Check("J04 shared Move attribution",Metric(ControlOperation.Movement).manualSeconds==0&&Metric(ControlOperation.Movement).actions.All(a=>a.action=="J04"));
        game.MainMenu();CheckMetricsFiles("platform");

        yield return AlphaFresh(1,d=>V2Rule(d,V2Ground("Q01")));
        yield return Seconds(.2f);PlayMetrics.Calculate(PlayMetrics.Current.operations,out bool valid,out float percentage);
        Check("All observed control automatic gives 100 percent",valid&&percentage==100&&Metric(ControlOperation.Aim).manualSeconds==0);
        checkingPause=true;game.SetPause(true);yield return DeveloperKey(Key.F8);ClickConfig("Analytics");yield return DeveloperKey(Key.Escape);
        Check("F8 closes to original paused state",game.Paused&&!game.DeveloperToolsOpen&&Time.timeScale==0);
        game.SetPause(false);checkingPause=false;
        game.MainMenu();CheckMetricsFiles("automatic-only");
        Check("Old actor callbacks cannot mutate next run",!PlayMetrics.Counting&&PlayMetrics.Current==null);
        game.NewGame();Check("New Game isolates history",PlayMetrics.Run.attempts.Count==0&&PlayMetrics.Run.combatSeconds==0);
        game.MainMenu();
    }
}
#endif
