#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private IEnumerator ManualShootCharacter(GameSessionFlow flow, TowerCharacter target)
    {
        yield return FlowNear(flow,target);
        AimAtWorld(target.transform.position+Vector3.up,true);
        yield return WeaponsWait(()=>!target.IsAlive,3);
        MouseTo(.1f,.8f);yield return Frames(4);
    }
    private IEnumerator ManualPlaytestFixes(bool coverOnly = false)
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();
        var focus=StartCoroutine(S01Focus(flow));
        Record("Manual-fix focused paths: DEV room setups, virtual F/fire, focus/invulnerability assist; real projectiles and damage. No balance/build claim.");
        try
        {
            yield return FlowJump(flow,"boss_p1",0);
            yield return FlowKey(Key.F);
            Check("P1 sole response is the next spoken line",!ui.ChoicesVisible && ui.CurrentNarrativeId=="boss.p1.reply" && ui.DisplayedSpeaker=="Officer",ui.CurrentNarrativeId);
            yield return FlowKey(Key.F);
            Check("Sole response is not duplicated",!ui.DialogueVisible && flow.ActiveFight.EncounterActive,"P1 starts");
            flow.ActiveFight.Boss.StopCombat();
            var prop=flow.CurrentRoom.Prop;
            AimAtWorld(prop.AimTarget.position,true);yield return WeaponsWait(()=>!prop.IsHanging,3);MouseTo(.1f,.8f);
            yield return WeaponsWait(()=>prop.HasLanded,4);
            Check("Chandelier drops through a real shot",prop.HasLanded && prop.LastActor==ProjectileActor.Player,"landed");
            foreach(var b in flow.ActiveFight.GetComponentsInChildren<Projectile>())b.Retire();
            var body=prop.GetComponent<BoxCollider2D>();int blocked=prop.BlockedProjectiles;
            AimAtWorld(body.bounds.center,true);yield return WeaponsWait(()=>prop.BlockedProjectiles>blocked,3);MouseTo(.1f,.8f);yield return Frames(3);
            Check("Fallen cover blocks real rifle fire",prop.BlockedProjectiles>blocked,"Player contact consumed");
            var shooting=flow.ActiveFight.Player.GetComponent<PlayerShooting>();
            var prefab=(Projectile)typeof(PlayerShooting).GetField("bulletPrefab",System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(shooting);
            foreach(var b in flow.ActiveFight.GetComponentsInChildren<Projectile>())b.Retire();
            yield return Frames(2);
            blocked=prop.BlockedProjectiles;
            var probe=Instantiate(prefab,new Vector3(body.bounds.max.x+1,body.bounds.center.y,0),Quaternion.identity);
            probe.Launch(Vector2.left,12,9,flow.ActiveFight.Boss.gameObject);yield return Seconds(.4f);
            Check("Same receiver still blocks hostile fire",probe==null && prop.BlockedProjectiles==blocked+1,$"spent={probe==null}, blocked={blocked}->{prop.BlockedProjectiles}");
            if(coverOnly){flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);yield break;}

            yield return FlowJump(flow,"boss_p2",2);yield return WeaponsWait(()=>ui.DialogueVisible,4);
            yield return FlowKey(Key.F);yield return FlowKey(Key.F);
            Check("P2 sole remaining reply has no choice UI",!ui.ChoicesVisible && ui.CurrentNarrativeId=="boss.p2.reply.why",ui.CurrentNarrativeId);
            var content=System.IO.File.ReadAllText("Assets/_Game/Shared/Data/Narrative/go_ghost_narrative.json");
            Check("Unintended entry and branch are absent",!content.Contains("deny_ai_consciousness") && !content.Contains("That's not conscious"),"JSON");
            Check("Mode selector no longer depends on deleted denial",TowerNarrativeState.SelectP3(TowerNarrativeState.ResetDecision.Unresolved,false,false,false)==TowerNarrativeState.Mode.FullForce
                && TowerNarrativeState.SelectP3(TowerNarrativeState.ResetDecision.Cancelled,true,true,true)==TowerNarrativeState.Mode.Restrained,"remaining rules");

            yield return FlowJump(flow,"floor99",0);yield return Seconds(1.5f);Keys(Key.D);yield return Seconds(.4f);Keys();
            Check("99F entry keeps control without dialogue or combat",!ui.DialogueVisible && !flow.ActiveFight.NarrativePaused && !flow.ActiveFight.EncounterActive,"walk alone");
            var guard=flow.ActiveFight.BossHealth;
            flow.ActiveFight.Player.transform.position=guard.transform.position+Vector3.left*1.3f;
            flow.ActiveFight.Player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
            AimAtWorld(guard.transform.position,false);yield return Frames(5);
            Check("99F proximity only offers F",ui.InteractionKeyVisible && !ui.DialogueVisible && !flow.ActiveFight.EncounterActive,"no auto trigger");
            yield return FlowKey(Key.F);
            Check("F starts security announcement",ui.CurrentNarrativeId=="npc.floor99.security",ui.CurrentNarrativeId);
            yield return FlowClose(ui);
            Check("F announcement leads to encounter",flow.ActiveFight.EncounterActive,"active");
            yield return FlowJump(flow,"floor99",0);
            AimAtWorld(flow.ActiveFight.BossHealth.transform.position,true);yield return WeaponsWait(()=>ui.DialogueVisible,3);MouseTo(.1f,.8f);yield return Frames(3);
            Check("Actual shot starts 99F announcement",ui.CurrentNarrativeId=="npc.floor99.security" && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls>0,"real shot");
            yield return FlowClose(ui);Check("Attack announcement leads to encounter",flow.ActiveFight.EncounterActive,"active");

            yield return FlowJump(flow,"office_intro",0);yield return FlowKey(Key.F);yield return FlowKey(Key.F);
            Check("Office sole response is spoken without choices",ui.CurrentNarrativeId=="office.reply.surrender" && !ui.ChoicesVisible,ui.CurrentNarrativeId);
            for(int i=0;i<10 && ui.CurrentNarrativeId!="office.summon";i++)yield return FlowKey(Key.F);
            Check("Mandatory President line is exact with CEO speaker",ui.CurrentNarrativeId=="office.summon" && ui.DisplayedNarrativeText=="傀儡师，保护我。" && ui.DisplayedSpeaker=="CEO",ui.CurrentNarrativeId);
            var text=ui.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="DialogueLine");
            Check("Chinese line has baked glyphs",text.font.HasCharacters(ui.DisplayedNarrativeText),text.font.name);
            ScreenCapture.CaptureScreenshot("Logs/ManualFix/president-summon.png");yield return Frames(3);
            yield return FlowKey(Key.F);yield return Seconds(1.1f);
            Check("Normal P3 starts after one summon",flow.TowerStageId=="boss_p3" && flow.ActiveFight.EncounterActive && !ui.DialogueVisible,"no duplicate summon");
            flow.DevelopmentCompleteEncounter();yield return Frames(5);
            Check("Combat-carrier defeat preserves disposition choices",flow.TowerStageId=="final_disposition" && flow.ContinuingCore.IsAlive
                && flow.Narrative.CoreDisposition==TowerNarrativeState.CoreDecision.Unresolved,"clarified scope");
            yield return FlowJump(flow,"boss_p3",0);
            Check("Direct P3 DEV entry includes mandatory summon",ui.CurrentNarrativeId=="office.summon" && ui.DisplayedNarrativeText=="傀儡师，保护我。",ui.CurrentNarrativeId);

            yield return FlowJump(flow,"final_disposition",0);yield return FlowFinalChoice(flow,flow.President,1);
            yield return ManualShootCharacter(flow,flow.ContinuingCore);
            Check("Shooting core immediately completes C after Spare",flow.State==GameSessionFlow.FlowState.Result && flow.TowerEndingId=="ending.C" && flow.TowerCompletionCount==1,"no corpse F");
            yield return Frames(20);yield return FlowKey(Key.F);
            Check("Core death does not resolve twice",flow.TowerCompletionCount==1,"once");
            yield return FlowJump(flow,"final_disposition",0);yield return FlowFinalChoice(flow,flow.ContinuingCore,0);
            yield return ManualShootCharacter(flow,flow.President);
            Check("Shooting President immediately completes A after Preserve",flow.State==GameSessionFlow.FlowState.Result && flow.TowerEndingId=="ending.A" && flow.TowerCompletionCount==1,"no corpse F");
            yield return Frames(20);yield return FlowKey(Key.F);
            Check("President death does not resolve twice",flow.TowerCompletionCount==1,"once");
            yield return FlowJump(flow,"final_disposition",0);yield return ManualShootCharacter(flow,flow.President);
            Check("First kill commits only its actual fate",flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Execute && flow.Narrative.CoreDisposition==TowerNarrativeState.CoreDecision.Unresolved
                && flow.TowerCompletionCount==0 && !ui.InteractionKeyVisible,"other choice retained");
            yield return ManualShootCharacter(flow,flow.ContinuingCore);
            Check("Two direct kills complete B once",flow.TowerEndingId=="ending.B" && flow.TowerCompletionCount==1,"both automatic");
            yield return FlowJump(flow,"final_disposition",0);yield return FlowFinalChoice(flow,flow.President,0);yield return FlowFinalChoice(flow,flow.ContinuingCore,1);
            Check("F finishers share the same once-only resolution",flow.TowerEndingId=="ending.B" && flow.TowerCompletionCount==1,"no death callback double commit");
            Check("DEV results did not award normal Archive",PoliceArchive.Current.RunRecords.Count==0,"isolated test");
            flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.1f,.8f);}
    }
}
#endif
