#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Six v0.7 acceptance paths on the existing virtual input + isolated Archive driver. No new harness.
public partial class TopDownSmokeTest
{
    private IEnumerator FlowKey(Key key) { Keys(); yield return Frames(2); Keys(key); yield return Frames(2); Keys(); yield return Frames(2); }
    private IEnumerator FlowClose(MissionUI ui)
    {
        for (int i=0; i<16 && ui.DialogueVisible && !ui.ChoicesVisible; i++) yield return FlowKey(Key.F);
    }
    private IEnumerator FlowNear(GameSessionFlow flow, TowerCharacter npc)
    {
        flow.ActiveFight.Player.transform.position = npc.transform.position + new Vector3(-1.3f,.85f,0);
        flow.ActiveFight.Player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
        yield return Frames(4);
    }
    private IEnumerator FlowShootBoss(GameSessionFlow flow)
    {
        float end=Time.unscaledTime+70;
        int shots=flow.ActiveFight.Player.GetComponent<PlayerShooting>().ShotsFired;
        while (flow.ActiveFight.EncounterActive && Time.unscaledTime<end)
        {
            if(flow.ActiveFight.BossHealth!=null) AimAtWorld(flow.ActiveFight.BossHealth.GetComponent<Collider2D>().bounds.center,true);
            yield return null;
        }
        MouseTo(.2f,.8f); yield return Frames(3);
        Check("Real rifle resolves encounter",!flow.ActiveFight.Player.IsDead && flow.ActiveFight.BossHealth.IsDead && !flow.ActiveFight.EncounterActive && flow.ActiveFight.Player.GetComponent<PlayerShooting>().ShotsFired>shots,flow.TowerStageId);
    }
    private IEnumerator FlowJump(GameSessionFlow flow, string node, int tier)
    {
        Keys(); MouseTo(.2f,.8f);
        var menu=flow.GetComponent<DeveloperRouteJump>(); menu.SetVisible(true); menu.SelectNode(node); menu.SelectTier(tier); menu.JumpSelected();
        yield return WeaponsWait(()=>flow.TowerStageId==node && flow.State==GameSessionFlow.FlowState.Combat,4);
        flow.ActiveFight.Player.IsInvulnerable=true;
        yield return Frames(3);
    }
    private IEnumerator FlowFinalChoice(GameSessionFlow flow, TowerCharacter target, int index)
    {
        yield return FlowNear(flow,target); yield return FlowKey(Key.F);
        for(int i=0;i<index;i++) yield return FlowKey(Key.S);
        yield return FlowKey(Key.F);
    }

    private IEnumerator FlowRoute()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>(); var ui=flow.GetComponent<MissionUI>();
        var focus=StartCoroutine(S01Focus(flow));
        Record("v0.7 normal route: no DEV jump/completion, actual rifle damage and F. ASSISTS: invulnerable player, automatic mouse tracking, short doorway/interaction reposition. Isolated Archive; not balance/hardware proof.");
        try
        {
            Check("Launch stops at normal loadout",flow.State==GameSessionFlow.FlowState.Loadout,flow.State.ToString());
            S01Field(flow,"developmentPermissionTier",0);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Practice,4);
            var player=flow.ActiveFight.Player; player.IsInvulnerable=true; var deployment=flow.Deployment; var officer=flow.Officer;
            player.transform.position=new Vector3(-27,-1.6f,0); Keys(Key.D);
            yield return WeaponsWait(()=>flow.TowerStageId=="tower_entry",8); Keys();
            player.IsInvulnerable=true; // Door reapplies deployment and clears transient dodge/invulnerability state.
            Check("Practice door enters actual tower",flow.TowerStageId=="tower_entry",flow.TowerStageId);
            yield return FlowNear(flow,flow.CurrentRoom.Character);
            Check("Only nearby F is visible",ui.InteractionKeyVisible,ui.InteractionKeyVisible.ToString());
            yield return FlowKey(Key.F);
            Check("F opens external English without double-consuming",ui.DialogueVisible && ui.DisplayedNarrativeText=="Hi" && Time.timeScale==0,"worker");
            yield return FlowKey(Key.F);
            Check("Fresh F closes/resumes once",!ui.DialogueVisible && Time.timeScale==1,"worker");
            yield return S02WalkExit(flow);
            yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F); yield return FlowClose(ui);
            yield return Seconds(2.5f); yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F);
            yield return FlowKey(Key.S); yield return FlowKey(Key.F);
            Check("Real 1F path/cancel is recorded alive",flow.Narrative.MaintenanceResetDecision==TowerNarrativeState.ResetDecision.Cancelled && flow.Narrative.MaintenanceAlive==true,"floor01");
            yield return S02WalkExit(flow); yield return FlowClose(ui);
            Check("P1 response accepts live gameplay",ui.ChoicesVisible && flow.ReplyOrAttackActive && !flow.ActiveFight.NarrativePaused,"p1");
            AimAtWorld(flow.ActiveFight.BossHealth.transform.position,true); yield return Frames(3);
            Check("Actual LMB starts P1 and launches opening shot",flow.ActiveFight.EncounterActive && !ui.DialogueVisible && player.GetComponent<PlayerShooting>().TriggerPulls>0,"p1");
            yield return FlowShootBoss(flow); yield return S02WalkExit(flow);
            yield return Seconds(2.1f);
            Check("50F waits for F radio, no entry timer",!flow.ActiveFight.EncounterActive && flow.CurrentRoom.Receptionist.IsAlive,"floor50");
            yield return FlowNear(flow,flow.CurrentRoom.Receptionist); yield return FlowKey(Key.F); yield return FlowClose(ui);
            Check("Radio completion starts Security",flow.ActiveFight.EncounterActive,"floor50");
            yield return FlowShootBoss(flow);
            Check("Normal guard fight preserves unhit receptionist",flow.CurrentRoom.Receptionist.IsAlive && flow.CurrentRoom.Prop.IsHanging,$"player={player.transform.position}, npc={flow.CurrentRoom.Receptionist.transform.position}, cause={flow.Narrative.ReceptionistLethalCause}");
            yield return S02WalkExit(flow); yield return new WaitForSecondsRealtime(2);
            Check("Tier zero P2 has only curiosity line",ui.CurrentNarrativeId=="boss.p2.intro.no_ai" && !flow.HasInnerAI,ui.CurrentNarrativeId);
            yield return FlowClose(ui); yield return FlowKey(Key.S); yield return FlowKey(Key.F);
            Check("Selecting denial alone is not delivery",!flow.Narrative.AntiAiLifeHostility && ui.CurrentNarrativeId=="boss.p2.reply.deny_ai_consciousness",ui.CurrentNarrativeId);
            yield return FlowClose(ui); yield return FlowShootBoss(flow); yield return S02WalkExit(flow);
            Check("99F actual command before fight",ui.CurrentNarrativeId=="npc.floor99.security",ui.CurrentNarrativeId);
            yield return FlowClose(ui); yield return FlowShootBoss(flow); yield return S02WalkExit(flow);
            yield return FlowClose(ui);
            int before=player.GetComponent<PlayerShooting>().TriggerPulls;
            AimAtWorld(flow.President.transform.position+Vector3.up,true); yield return WeaponsWait(()=>ui.CurrentNarrativeId=="office.summon",3); MouseTo(.2f,.8f);
            Check("Office attack shoots, separation protects CEO",player.GetComponent<PlayerShooting>().TriggerPulls>before && flow.President.IsAlive && ui.CurrentNarrativeId=="office.summon",ui.CurrentNarrativeId);
            yield return FlowClose(ui); yield return new WaitForSecondsRealtime(1.1f);
            Check("Real selector freezes denial to overdrive",flow.Narrative.FrozenP3Mode==TowerNarrativeState.Mode.Overdrive && flow.ActiveFight.EncounterActive,flow.Narrative.FrozenP3Mode.ToString());
            yield return FlowShootBoss(flow);
            Check("P3 defeat keeps same living run for disposition",flow.TowerStageId=="final_disposition" && flow.Officer==officer && flow.Deployment==deployment && flow.ActiveFight.Player==player,"identity/snapshot");
            yield return FlowFinalChoice(flow,flow.President,0); yield return FlowFinalChoice(flow,flow.ContinuingCore,0);
            Check("Two actual final resolutions finish A once",flow.State==GameSessionFlow.FlowState.Result && flow.TowerEndingId=="ending.A" && !flow.President.IsAlive && flow.ContinuingCore.IsAlive && flow.TowerCompletionCount==1,flow.TowerEndingId);
            Check("Normal record preserves explicit A without old percentage ending",PoliceArchive.Current.RunRecords.Count==1 && PoliceArchive.Current.LatestRun.NarrativeEndingId=="ending.A" && PoliceArchive.Current.HasEnding("ending.A") && !PoliceArchive.Current.HasEnding(PoliceArchive.HumanEnding),"isolated Archive");
            yield return ClickSessionButton("ResultContinue");
            Check("Ending uses exact JSON only",ui.CurrentNarrativeId=="ending.A" && ui.DisplayedNarrativeText==ui.NarrativeSequence("ending.A")[0],ui.DisplayedNarrativeText);
            yield return FlowClose(ui);
            Check("Ending F returns to Main Menu",flow.State==GameSessionFlow.FlowState.MainMenu,flow.State.ToString());
        }
        finally { StopCoroutine(focus); Keys(); MouseTo(.2f,.8f); }
    }

    private IEnumerator FlowLocal()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();var focus=StartCoroutine(S01Focus(flow));
        Record("v0.7 local acceptance: DEV jumps, real F/fire/impact, virtual input, invulnerability and controlled death. Isolated Archive; not normal-route/balance proof.");
        try
        {
            yield return FlowKey(Key.F8); Check("F8 opens from loadout",flow.GetComponent<DeveloperRouteJump>().MenuVisible,"loadout"); yield return FlowKey(Key.F8);
            yield return FlowJump(flow,"floor50",2); yield return FlowNear(flow,flow.CurrentRoom.Receptionist); yield return FlowKey(Key.F); yield return FlowClose(ui);
            yield return WeaponsWait(()=>ui.DialogueVisible,3); int shots=flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls;
            float elapsed=flow.ActiveFight.CombatElapsed;yield return new WaitForSecondsRealtime(.8f);
            Check("Dialogue holds combat timer",flow.ActiveFight.CombatElapsed==elapsed && !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride,"pause");
            yield return FlowClose(ui); Keys(Key.A);yield return Seconds(3.15f);Keys();
            Check("Actual complete T2 veto/no projectile/surviving NPC",flow.Narrative.Floor50T2VetoCompleted && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==shots && flow.CurrentRoom.Receptionist.IsAlive,"T2");
            Check("Selector: exact restrained, denial precedence, murder default",TowerNarrativeState.SelectP3(false,TowerNarrativeState.ResetDecision.Cancelled,true,true,true)==TowerNarrativeState.Mode.Restrained
                && TowerNarrativeState.SelectP3(true,TowerNarrativeState.ResetDecision.Cancelled,true,true,true)==TowerNarrativeState.Mode.Overdrive
                && TowerNarrativeState.SelectP3(false,TowerNarrativeState.ResetDecision.Unresolved,false,false,false)==TowerNarrativeState.Mode.FullForce
                && TowerNarrativeState.SelectP3(false,TowerNarrativeState.ResetDecision.Cancelled,false,true,true)==TowerNarrativeState.Mode.FullForce,"finite rule");
            yield return FlowJump(flow,"floor50",3);yield return FlowNear(flow,flow.CurrentRoom.Receptionist);yield return FlowKey(Key.F);yield return FlowClose(ui);
            yield return WeaponsWait(()=>ui.DialogueVisible,3);int ammo=flow.ActiveFight.Player.GetComponent<PlayerShooting>().AmmoInMagazine;yield return FlowClose(ui);
            yield return WeaponsWait(()=>!flow.CurrentRoom.Receptionist.IsAlive && flow.CurrentRoom.Prop.HasLanded,5);
            Check("T3 actual ammo/hit/fall/death keeps InnerAI",flow.CurrentRoom.Prop.HasLanded && flow.Narrative.ReceptionistAlive==false && flow.Narrative.ReceptionistCausalActor==ProjectileActor.InnerAI && flow.ActiveFight.Player.GetComponent<PlayerShooting>().AmmoInMagazine<ammo,$"landed={flow.CurrentRoom.Prop.HasLanded}, alive={flow.Narrative.ReceptionistAlive}, actor={flow.Narrative.ReceptionistCausalActor}, ammo={flow.ActiveFight.Player.GetComponent<PlayerShooting>().AmmoInMagazine}/{ammo}");
            yield return FlowJump(flow,"floor50",0);yield return FlowNear(flow,flow.CurrentRoom.Receptionist);
            AimAtWorld(flow.CurrentRoom.Receptionist.transform.position+Vector3.up,true);yield return Seconds(.65f);MouseTo(.2f,.8f);yield return Frames(3);
            Check("Direct pre-call death starts guard once, no corpse speech",!flow.CurrentRoom.Receptionist.IsAlive && flow.ActiveFight.EncounterActive && flow.Narrative.ReceptionistLethalCause=="dead_projectile" && flow.Narrative.ReceptionistCausalActor==ProjectileActor.Player && !ui.DialogueVisible,"real NPC damage");
            foreach(var pair in new[]{("A",0,0), ("B",0,1), ("C",1,1), ("D",1,0), ("E",1,2)})
            {
                yield return FlowJump(flow,"final_disposition",0);
                Check("DEV final has a defeated carrier "+pair.Item1,flow.ActiveFight.BossHealth.IsDead,"local prerequisite only");
                yield return FlowNear(flow,flow.ContinuingCore);yield return FlowKey(Key.F);
                Check("Control unavailable before explicit Spare "+pair.Item1,ui.SelectedChoiceId=="office.final.puppeteer.preserve","core first");
                yield return FlowKey(Key.S);yield return FlowKey(Key.S);
                Check("Core cycles two legal early choices "+pair.Item1,ui.SelectedChoiceId=="office.final.puppeteer.preserve",ui.SelectedChoiceId);
                yield return FlowKey(Key.Escape);
                yield return FlowFinalChoice(flow,flow.President,pair.Item2);
                Check("First final decision does not complete "+pair.Item1,flow.State==GameSessionFlow.FlowState.Combat && flow.TowerCompletionCount==0,"first only");
                yield return FlowNear(flow,flow.ContinuingCore);yield return FlowKey(Key.F);
                if(pair.Item1=="D") Check("D cue before fresh commitment",ui.DisplayedConsequence==ui.NarrativeEntry("office.final.restore_queued") && flow.TowerCompletionCount==0,ui.DisplayedConsequence);
                for(int i=0;i<pair.Item3;i++)yield return FlowKey(Key.S);
                yield return FlowKey(Key.F);
                Check("Final F result "+pair.Item1,flow.State==GameSessionFlow.FlowState.Result && flow.TowerEndingId=="ending."+pair.Item1 && flow.TowerCompletionCount==1,flow.TowerEndingId);
            }
            Check("DEV endings did not write normal Archive",PoliceArchive.Current.RunRecords.Count==0 && PoliceArchive.Current.UnlockedEndings.Count==0,"isolated profile");
            yield return FlowJump(flow,"floor50",2);yield return FlowNear(flow,flow.CurrentRoom.Receptionist);yield return FlowKey(Key.F);
            yield return FlowJump(flow,"boss_p2",0);yield return new WaitForSecondsRealtime(2);
            Check("Jump replaced pause/facts/aim with target intro only",!flow.Narrative.Floor50Connected && !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride && ui.CurrentNarrativeId=="boss.p2.intro.no_ai",ui.CurrentNarrativeId);
            flow.ActiveFight.Player.IsInvulnerable=false;flow.ActiveFight.Player.TakeDamage(100000,gameObject);yield return Frames(4);
            Check("Mid-run death clears dialogue/pause/aim",flow.State==GameSessionFlow.FlowState.Result && !ui.DialogueVisible && Time.timeScale==1 && flow.TowerStageId==null,"death");
            flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);
            Check("Full restart returns normal preview/default facts",flow.State==GameSessionFlow.FlowState.Loadout && !flow.IsDevelopmentJumpRun && !flow.Narrative.AntiAiLifeHostility,"loadout");
        }
        finally {StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }
    private IEnumerator FlowEdges()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>(); var ui=flow.GetComponent<MissionUI>(); var focus=StartCoroutine(S01Focus(flow));
        Record("Focused boundary checks: same DEV fixture/isolated Archive; no balance or hardware claim.");
        try
        {
            yield return FlowJump(flow,"floor01",0); yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F);
            yield return FlowKey(Key.F8); string greeting=ui.DisplayedNarrativeText; yield return FlowKey(Key.F);
            Check("F8 holds underlying dialogue input",ui.DisplayedNarrativeText==greeting && flow.ActiveFight.NarrativePaused,"menu");
            yield return FlowKey(Key.F8); yield return FlowClose(ui); yield return Seconds(2.3f);
            yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F); yield return FlowKey(Key.F); yield return Seconds(2.1f);
            Check("Allow reset requires actual movement/completion",flow.Narrative.MaintenanceResetDecision==TowerNarrativeState.ResetDecision.Allowed && flow.CurrentRoom.Character.IsAlive,"1F");
            yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F);
            Check("Reset changed subsequent F binding",ui.CurrentNarrativeId=="npc.floor01.restored",ui.CurrentNarrativeId); yield return FlowClose(ui);

            yield return FlowJump(flow,"floor50",2); var menu=flow.GetComponent<DeveloperRouteJump>(); menu.SetVisible(true);
            menu.SelectUpgrade(LoadoutSlot.Torso,LoadoutRules.TorsoSlow); menu.RestartCurrent();
            yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Combat && flow.TowerStageId=="floor50",4); yield return Frames(3);
            var player=flow.ActiveFight.Player; player.IsInvulnerable=true;var gun=player.GetComponent<PlayerShooting>(); var input=player.GetComponent<PlayerInputReader>(); var slow=player.GetComponent<PlayerTimeSlow>();
            yield return FlowNear(flow,flow.CurrentRoom.Receptionist); yield return FlowKey(Key.F); yield return FlowClose(ui); slow.TryActivate();
            // Held Fire points harmlessly upward before/through the intervention, then is released explicitly.
            AimAtWorld(player.transform.position+Vector3.up*8,true); yield return WeaponsWait(()=>ui.DialogueVisible,2);
            float active=slow.ActiveRemaining, cooldown=slow.CooldownRemaining; int shots=gun.TriggerPulls;
            yield return new WaitForSecondsRealtime(.7f);
            Check("Dialogue preserves unscaled Time Slow duration/cooldown",Mathf.Abs(slow.ActiveRemaining-active)<.01f && Mathf.Abs(slow.CooldownRemaining-cooldown)<.01f,"Q prototype");
            yield return FlowClose(ui); yield return Seconds(.2f);
            Check("Old held Fire and closing F cannot authorize T2",gun.TriggerPulls==shots && input.HasTemporaryAimOverride,"release gate");
            MouseTo(.2f,.8f);yield return Frames(3); AimAtWorld(flow.CurrentRoom.Prop.AimTarget.position,true); yield return Seconds(.4f);MouseTo(.2f,.8f);
            yield return WeaponsWait(()=>!flow.CurrentRoom.Receptionist.IsAlive,3);
            Check("Fresh T2 Fire keeps Player causality and no veto",flow.Narrative.ChandelierCausalActor==ProjectileActor.Player && flow.Narrative.ReceptionistCausalActor==ProjectileActor.Player && !flow.Narrative.Floor50T2VetoCompleted,"real hit/death");
            yield return FlowJump(flow,"floor50",2);yield return FlowNear(flow,flow.CurrentRoom.Receptionist);yield return FlowKey(Key.F);yield return FlowClose(ui);yield return WeaponsWait(()=>ui.DialogueVisible,2);yield return FlowClose(ui);
            Check("T2 override active before jump",flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride,"active");
            yield return FlowJump(flow,"tower_entry",0);
            Check("Jump clears active override and skipped facts",!flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride && !flow.ActiveFight.NarrativePaused && !flow.Narrative.Floor50T2VetoCompleted,"new run");

            yield return FlowJump(flow,"office_intro",3);yield return FlowClose(ui); yield return FlowKey(Key.F);
            yield return FlowClose(ui); // Includes exact taunt/laugh, then warning.
            yield return new WaitForSecondsRealtime(.85f);
            Check("High AI office points without autonomous fire",ui.CurrentNarrativeId=="office.summon" && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==0 && !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride,ui.CurrentNarrativeId);
            yield return FlowClose(ui);yield return new WaitForSecondsRealtime(1);
            Check("Office dialogue reaches real default P3",flow.TowerStageId=="boss_p3" && flow.ActiveFight.EncounterActive && flow.P3DevelopmentMode==GameSessionFlow.DevelopmentP3Mode.FullForce,"default profile");

            yield return FlowJump(flow,"final_disposition",0);yield return FlowFinalChoice(flow,flow.ContinuingCore,0);
            yield return FlowNear(flow,flow.President);yield return FlowKey(Key.F);yield return FlowKey(Key.S);
            Check("D cue also precedes CEO-second commitment",ui.DisplayedConsequence==ui.NarrativeEntry("office.final.restore_queued") && flow.TowerCompletionCount==0,"reverse order");
            yield return FlowKey(Key.F);Check("Reverse order finishes D once",flow.TowerEndingId=="ending.D" && flow.TowerCompletionCount==1,"D");
            yield return FlowJump(flow,"final_disposition",0);yield return FlowNear(flow,flow.President);
            AimAtWorld(flow.President.transform.position+Vector3.up,true);yield return Seconds(.9f);MouseTo(.2f,.8f);yield return Frames(3);yield return FlowKey(Key.F);
            Check("Dead CEO F acknowledges actual death without speech",!flow.President.IsAlive && flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Execute && !ui.DialogueVisible,"no resurrection");
        }
        finally {StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }

}
#endif
