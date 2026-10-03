#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public partial class TopDownSmokeTest
{
    private bool BubbleOnScreen(MissionUI ui)
    {
        var corners = new Vector3[4]; ((RectTransform)DialoguePart(ui,"DialoguePanel")).GetWorldCorners(corners);
        return corners.All(p => p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height);
    }

    private IEnumerator ProductionUI()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>(); var ui=flow.GetComponent<MissionUI>();
        var focus=StartCoroutine(S01Focus(flow));
        Record("Production UI: isolated Archive, DEV rooms and virtual keyboard/mouse; controlled edge positioning. No hardware-input claim.");
        try
        {
            yield return FlowJump(flow,"tower_entry",2); yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F);
            var bubble=ui.GetComponentInChildren<WorldSpeechBubble>(true);
            Check("Worker greeting and spatial anchor",ui.DisplayedNarrativeText=="Hi." && ui.DisplayedSpeaker=="Worker" && bubble.Speaker==flow.CurrentRoom.Character.InteractionAnchor,"entry line 1");
            yield return DialogueClick(.1f,.8f);
            Check("Company remains explicit placeholder",ui.DisplayedNarrativeText=="Welcome to [COMPANY NAME TBD].",ui.DisplayedNarrativeText);
            ScreenCapture.CaptureScreenshot("Logs/ProductionPass/entry-bubble.png");yield return Frames(3);
            yield return FlowKey(Key.F);
            Check("F closes and dialogue clicks never shoot",!ui.DialogueVisible && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==0,"shared input gate");
            Check("T2 worker and open exit are perceived",flow.CurrentRoom.Character.GetComponent<InteractionOutline>().Visible
                && flow.CurrentRoom.GetComponentInChildren<TowerDoor>().GetComponent<InteractionOutline>().Visible,"relevant shapes");

            flow.ActiveFight.SetNarrativePaused(true); ui.ShowNarrative("boss.p1.reply",()=>flow.ActiveFight.SetNarrativePaused(false));yield return Frames(3);
            Check("Player label is You and anchored to player",ui.DisplayedSpeaker=="You" && bubble.Speaker==flow.ActiveFight.Player.transform,"player");
            var player=flow.ActiveFight.Player;var original=player.transform.position;
            foreach(float x in new[]{.01f,.99f})
            {
                player.transform.position=Camera.main.ViewportToWorldPoint(new Vector3(x,.9f,10));Physics2D.SyncTransforms();yield return Frames(3);
                Check("Bubble stays visible near edge "+x,BubbleOnScreen(ui),((RectTransform)bubble.transform).anchoredPosition.ToString());
            }
            ScreenCapture.CaptureScreenshot("Logs/ProductionPass/edge-bubble.png");yield return Frames(3);
            player.transform.position=original;Physics2D.SyncTransforms();yield return FlowKey(Key.F);
            flow.ActiveFight.SetNarrativePaused(true);ui.ShowNarrative("intervention.chandelier.advice",()=>flow.ActiveFight.SetNarrativePaused(false));yield return Frames(3);
            Check("Inner AI label and player anchor",ui.DisplayedSpeaker=="Voice in your head" && bubble.Speaker==player.transform,"distinct voice");
            var text=DialoguePart(ui,"DialogueLine").GetComponent<TMP_Text>();
            Check("Bubble body is opaque plain readable text",text.color==Color.white && !text.richText && !text.enableAutoSizing && text.fontSize==38 && !text.isTextOverflowing,"TMP");
            yield return FlowKey(Key.F);

            string[] ids={"npc.floor01.choice.allow_reset","npc.floor01.choice.cancel_reset","npc.floor01.choice.attack"};
            int commits=0;string picked=null;
            System.Action<MissionUI.DialogueResult,string> done=(r,id)=>{if(r==MissionUI.DialogueResult.ChoiceCommitted){commits++;picked=id;}flow.ActiveFight.SetNarrativePaused(false);};
            flow.ActiveFight.SetNarrativePaused(true);ui.ShowChoices(ids,done);yield return Frames(3);
            yield return FlowKey(Key.S);Check("S chooses next",ui.SelectedChoiceId==ids[1],ui.SelectedChoiceId);
            yield return FlowKey(Key.W);Check("W chooses previous",ui.SelectedChoiceId==ids[0],ui.SelectedChoiceId);
            yield return FlowKey(Key.DownArrow);Check("Arrow does not navigate",ui.SelectedChoiceId==ids[0],ui.SelectedChoiceId);
            DialoguePointer(ui,2);yield return Frames(3);Check("Hover highlights choice",ui.SelectedChoiceId==ids[2],ui.SelectedChoiceId);
            yield return FlowKey(Key.W);yield return FlowKey(Key.F);Check("Stationary hover permits W/F selection",commits==1 && picked==ids[1],picked);
            flow.ActiveFight.SetNarrativePaused(true);ui.ShowChoices(ids,done);yield return Frames(3);
            ScreenCapture.CaptureScreenshot("Logs/ProductionPass/choices-bubble.png");yield return Frames(3);
            DialoguePointer(ui,2,true);yield return Frames(6);
            Check("One click selects and confirms, held click never fires",commits==2 && picked==ids[2] && !ui.DialogueVisible && player.GetComponent<PlayerShooting>().TriggerPulls==0,picked);
            MouseTo(.1f,.8f);yield return Frames(3);
            flow.ActiveFight.SetNarrativePaused(true);ui.ShowChoices(new[]{"boss.p1.choice.reply"},done);yield return Frames(3);
            Check("Global sole-response API speaks without choice UI",!ui.ChoicesVisible && ui.DisplayedSpeaker=="You" && ui.DisplayedNarrativeText==ui.NarrativeEntry("boss.p1.choice.reply"),"direct line");
            yield return FlowKey(Key.F);Check("Sole response commits once after its line",commits==3,"once");
            yield return ManualShootCharacter(flow,flow.CurrentRoom.Character);yield return Frames(3);
            Check("Dead worker loses outline",!flow.CurrentRoom.Character.GetComponent<InteractionOutline>().Visible,"actual rifle death");
            yield return FlowJump(flow,"tower_entry",1);yield return Frames(3);
            Check("T1 has no blue perception",!flow.CurrentRoom.Character.GetComponent<InteractionOutline>().Visible,"tier gate and rebinding");
            flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }

    private IEnumerator ProductionP1()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();var focus=StartCoroutine(S01Focus(flow));
        Record("P1: DEV tier deployments, focus/invulnerability assist except damage check; real weapon/ammo, falling prop, laser and projectile contacts. Terrain probe repositions player. Not balance proof.");
        try
        {
            for(int tier=1;tier<=3;tier++)
            {
                yield return FlowJump(flow,"boss_p1",tier);
                var fight=flow.ActiveFight;var player=fight.Player;var boss=fight.Boss;var reader=player.GetComponent<PlayerInputReader>();var gun=player.GetComponent<PlayerShooting>();var prop=flow.CurrentRoom.Prop;
                Check("P1 unknown speaker T"+tier,ui.DisplayedSpeaker=="???" && ui.GetComponentInChildren<WorldSpeechBubble>().Speaker==boss.transform,"identity hidden");
                yield return FlowKey(Key.F);Check("Original player reply T"+tier,ui.CurrentNarrativeId=="boss.p1.reply" && ui.DisplayedSpeaker=="You" && !ui.ChoicesVisible,ui.CurrentNarrativeId);
                yield return FlowKey(Key.F);Check("Silence immediately follows player T"+tier,ui.CurrentNarrativeId=="boss.p1.silence" && ui.DisplayedNarrativeText=="......" && ui.DisplayedSpeaker=="???","silence");
                yield return FlowKey(Key.F);
                Check("Silence starts charged laser T"+tier,boss.State==CyberpsychoBoss.AttackState.OpeningLaserCharge,"no extra line");
                Vector2 first=boss.OpeningLaserDirection;
                Keys(Key.Space);yield return Frames(2);Keys();
                yield return WeaponsWait(()=>ui.DialogueVisible,2);
                Check("Laser tracks moving player during charge T"+tier,Vector2.Distance(first,boss.OpeningLaserDirection)>.005f,boss.OpeningLaserDirection.ToString());
                var danger=player.GetComponent<InnerAIDangerSense>();
                Check("Tier danger gate T"+tier,tier==1 ? danger==null : danger!=null && danger.Visible && danger.PulseCount>0,"brief red streaks");
                Check("Tier blue chandelier gate T"+tier,prop.GetComponent<InteractionOutline>().Visible==(tier>=2),"physical target outline");
                Check("Existing tactical advice T"+tier,ui.DisplayedSpeaker=="Voice in your head" && ui.DisplayedNarrativeText==ui.NarrativeEntry("intervention.chandelier.plan"),"advice");
                if(tier==2){ScreenCapture.CaptureScreenshot("Logs/ProductionPass/p1-t2-advice.png");yield return Frames(3);}
                int ammo=gun.AmmoInMagazine;int shots=gun.TriggerPulls;
                yield return FlowKey(Key.F);
                if(tier<3)
                {
                    AimAtWorld(player.transform.position+Vector3.left*6,false);yield return Frames(4);
                    Check("Manual aim and no autonomous fire T"+tier,!reader.HasTemporaryAimOverride && !reader.AutomaticFireRequested && reader.AimDirection.x<-.99f && gun.TriggerPulls==shots,"mouse owns aim");
                    yield return S01ManualHit(prop);
                }
                else
                {
                    yield return WeaponsWait(()=>prop.HasLanded,3);
                    Check("T3 one real paid intervention",prop.LastActor==ProjectileActor.InnerAI && gun.TriggerPulls==shots+1 && gun.AmmoInMagazine==ammo-1 && !reader.HasTemporaryAimOverride,$"shots={gun.TriggerPulls-shots},ammo={ammo}->{gun.AmmoInMagazine}");
                }
                Check("Physical release and landing once T"+tier,prop.HasLanded && prop.OutcomeCount==1 && prop.LastPermissionTier==tier,"physics");
                // Restore a grounded position behind the actual cover before the final tracked ray fires.
                player.transform.position=new Vector3(-6,-1.7f,0);player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return Frames(3);
                int hp=player.CurrentHealth;player.IsInvulnerable=false;
                yield return WeaponsWait(()=>boss.OpeningLaserShots==1,4);
                Check("Fallen chandelier blocks damaging laser T"+tier,boss.OpeningLaserBlocked && player.CurrentHealth==hp,"nearest cover contact");
                player.IsInvulnerable=true;
                if(tier==2)
                {
                    ScreenCapture.CaptureScreenshot("Logs/ProductionPass/p1-laser-cover.png");yield return Frames(3);
                    boss.StopCombat();foreach(var bullet in fight.GetComponentsInChildren<Projectile>())bullet.Retire();yield return Frames(3);
                    var body=prop.GetComponent<BoxCollider2D>();int blocked=prop.BlockedProjectiles;
                    AimAtWorld(body.bounds.center,true);yield return WeaponsWait(()=>prop.BlockedProjectiles>blocked,2);MouseTo(.1f,.8f);yield return Frames(3);
                    Check("Cover consumes real player bullets",prop.BlockedProjectiles>blocked,"two-way cover");
                    foreach(var bullet in fight.GetComponentsInChildren<Projectile>())bullet.Retire();yield return Frames(2);
                    blocked=prop.BlockedProjectiles;
                    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
                    var probe=Instantiate(prefab,new Vector3(body.bounds.max.x+1,body.bounds.center.y,0),Quaternion.identity);
                    probe.Launch(Vector2.left,12,9,boss.gameObject);yield return Seconds(.4f);
                    Check("Cover consumes hostile bullets",probe==null && prop.BlockedProjectiles==blocked+1,"same physical body");
                    player.transform.position=new Vector3(body.bounds.center.x,body.bounds.max.y+1,0);player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return Seconds(.4f);
                    float y=player.transform.position.y;yield return FlowKey(Key.Space);yield return Seconds(.12f);
                    Check("Fallen chandelier supports real jump",player.transform.position.y>y+.4f,"standable terrain");
                    flow.DevelopmentRepeatRoomEvent();yield return Frames(3);Check("Opportunity cannot double resolve",prop.OutcomeCount==1 && !ui.DialogueVisible,"once key");
                }
            }
            // No cover and no invulnerability: prove the new attack actually damages through PlayerHealth.
            yield return FlowJump(flow,"boss_p1",0);yield return FlowClose(ui);
            var bare=flow.ActiveFight;bare.Player.IsInvulnerable=false;int before=bare.Player.CurrentHealth;
            yield return WeaponsWait(()=>bare.Boss.OpeningLaserShots==1,5);
            Check("Uncovered opening laser does real damage",bare.Player.CurrentHealth<before && !bare.Boss.OpeningLaserBlocked,$"HP {before}->{bare.Player.CurrentHealth}");
            bare.Player.IsInvulnerable=true;
            yield return FlowShootBoss(flow);
            Check("Existing P1 completion and 50F exit survive",flow.CurrentRoom.ExitOpen && flow.TowerStageId=="boss_p1","real rifle victory with invulnerability assist");
            yield return S02WalkExit(flow);Check("Same officer continues to 50F",flow.TowerStageId=="floor50" && !ui.DialogueVisible,"continue path");
            Check("DEV testing leaves normal Archive untouched",PoliceArchive.Current.RunRecords.Count==0,"isolated Archive");
            flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }

    private IEnumerator ProductionEdges()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();var focus=StartCoroutine(S01Focus(flow));
        Record("Focused edge fixtures: DEV deployments, controlled reload/cadence state and in-flight projectile probe; actual collision/weapon release. No normal Archive changes.");
        try
        {
            for(int mode=0;mode<3;mode++)
            {
                yield return FlowJump(flow,"boss_p1",3);yield return FlowClose(ui);
                yield return WeaponsWait(()=>ui.CurrentNarrativeId=="intervention.chandelier.advice",2);
                var fight=flow.ActiveFight;var gun=fight.Player.GetComponent<PlayerShooting>();var reader=fight.Player.GetComponent<PlayerInputReader>();var prop=flow.CurrentRoom.Prop;
                S01Field(gun,"<AmmoInMagazine>k__BackingField",0);
                S01Field(gun,"<IsReloading>k__BackingField",true);S01Field(gun,"reloadDoneAt",Time.time+.8f);
                yield return FlowKey(Key.F);yield return Seconds(.15f);
                Check("T3 waits for real reload "+mode,gun.TriggerPulls==0 && gun.IsReloading && reader.HasTemporaryAimOverride,"no free shot");
                if(mode==1)
                {
                    // An already travelling player projectile invalidates the target while the AI is waiting for reload.
                    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
                    var probe=Instantiate(prefab,prop.AimTarget.position+Vector3.left,Quaternion.identity);
                    probe.Launch(Vector2.right,18,12,fight.Player.gameObject,actor:ProjectileActor.Player,permissionTier:3);
                }
                if(mode==2) prop.AimTarget.GetComponent<Collider2D>().enabled=false;
                yield return Seconds(1.3f);
                if(mode==0) Check("Reload completes then T3 shoots once",gun.TriggerPulls==1 && gun.AmmoInMagazine==gun.MagazineSize-1 && !prop.IsHanging && prop.LastActor==ProjectileActor.InnerAI,"paid real weapon");
                else Check("Invalid/released target cancels T3 "+mode,gun.TriggerPulls==0 && !reader.HasTemporaryAimOverride && !reader.AutomaticFireRequested,"no invalid shot");
                if(mode==1) Check("Actual prior projectile wins attribution",prop.HasLanded && prop.OutcomeCount==1 && prop.LastActor==ProjectileActor.Player,"no double resolution");
                if(mode==2) Check("Disabled target loses perception",!prop.GetComponent<InteractionOutline>().Visible,"no false target");
            }
            yield return FlowJump(flow,"floor99",2);var player=flow.ActiveFight.Player;
            AimAtWorld(player.transform.position+Vector3.up*6,true);yield return Seconds(.6f);MouseTo(.1f,.8f);yield return Frames(3);
            Check("99F ignores shots away from Security",!ui.DialogueVisible && !flow.ActiveFight.EncounterActive,"unrelated shots");
            AimAtWorld(flow.ActiveFight.BossHealth.GetComponent<Collider2D>().bounds.center,true);
            yield return WeaponsWait(()=>ui.DialogueVisible,3);MouseTo(.1f,.8f);yield return Frames(3);
            Check("99F starts on actual Security hit",ui.CurrentNarrativeId=="npc.floor99.security","real damage gate");
            yield return FlowClose(ui);yield return Frames(3);
            Check("Danger sense reused outside P1",player.GetComponent<InnerAIDangerSense>()?.PulseCount>0,"Security threat");

            yield return FlowJump(flow,"boss_p2",2);yield return WeaponsWait(()=>ui.DialogueVisible,4);
            Check("P2 identity remains hidden pending authored introduction",ui.DisplayedSpeaker=="???",ui.DisplayedSpeaker);
            yield return FlowJump(flow,"office_intro",2);yield return FlowClose(ui);yield return Frames(3);
            Check("T2 office never takes aim control",!flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride,"global T2 contract");

            yield return FlowJump(flow,"floor01",2);yield return FlowNear(flow,flow.CurrentRoom.Character);yield return FlowKey(Key.F);yield return FlowClose(ui);
            yield return Seconds(2.6f);
            Check("Colliderless reset unit has a relevant blue contour",flow.CurrentRoom.ResetUnit.GetComponent<InteractionOutline>().Visible,"shared sprite fallback");
            yield return FlowNear(flow,flow.CurrentRoom.Character);yield return FlowKey(Key.F);yield return FlowKey(Key.S);yield return FlowKey(Key.F);yield return Frames(3);
            Check("Resolved reset interaction loses its outline",!flow.CurrentRoom.ResetUnit.GetComponent<InteractionOutline>().Visible,"relevance gate");
            flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }

    private IEnumerator ProductionFinish()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();var focus=StartCoroutine(S01Focus(flow));
        Record("Final affected-path check: practice perception, unpaused sole-response click, P1 pause/outline and forced death cleanup; DEV virtual input, no balance/build claim.");
        try
        {
            var menu=flow.GetComponent<DeveloperRouteJump>();menu.SetVisible(true);menu.SelectNode("practice");menu.SelectTier(2);menu.JumpSelected();
            yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Practice,4);yield return Frames(3);
            var door=FindAnyObjectByType<PracticeDoor>();
            Check("Practice door shares T2 perception",door.GetComponent<InteractionOutline>().Visible,"actual trigger");
            flow.RequestEnterBossRoom();yield return Frames(4);
            Check("Practice outline retires at room transition",!door.GetComponent<InteractionOutline>().Visible,"relevance");
            ui.ShowChoices(new[]{"boss.p1.choice.reply"},(_,__)=>{},true);yield return Frames(3);
            yield return DialogueClick(.1f,.8f);
            Check("Unpaused sole-response click cannot also fire",!ui.DialogueVisible && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==0,"global one-response safety");

            yield return FlowJump(flow,"boss_p1",2);yield return FlowClose(ui);
            yield return WeaponsWait(()=>ui.CurrentNarrativeId=="intervention.chandelier.advice",2);yield return Frames(3);
            var boss=flow.ActiveFight.Boss;float timer=boss.StateTimeRemaining;
            yield return new WaitForSecondsRealtime(.25f);
            Check("Dialogue genuinely pauses charged laser",Mathf.Approximately(timer,boss.StateTimeRemaining) && Time.timeScale==0,"unchanged combat clock");
            var prop=flow.CurrentRoom.Prop;
            Check("Hanging chandelier and suspension both outlined",prop.GetComponentsInChildren<LineRenderer>().Count(l=>l.enabled)>=2,"visual body plus active target");
            ScreenCapture.CaptureScreenshot("Logs/ProductionPass/p1-t2-advice-final.png");yield return Frames(3);
            yield return FlowKey(Key.F);flow.ActiveFight.Player.IsInvulnerable=false;
            flow.ActiveFight.Player.TakeDamage(int.MaxValue,boss.gameObject);yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Result,3);yield return Frames(3);
            Check("Death cancels laser, bubble and temporary control",boss.State==CyberpsychoBoss.AttackState.Stopped && !ui.DialogueVisible
                && !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride && Time.timeScale==1,"forced-death cleanup");
            flow.RequestContinue();yield return Frames(3);flow.RequestNewGame();yield return Frames(3);
            Check("Continue opens new officer loadout",flow.State==GameSessionFlow.FlowState.Loadout,"no softlock");
            yield return FlowJump(flow,"tower_entry",0);yield return Frames(3);
            Check("Fresh tier zero clears previous perception",!flow.CurrentRoom.Character.GetComponent<InteractionOutline>().Visible && flow.ActiveFight.Player.GetComponent<InnerAIDangerSense>()==null,"fresh deployment");
            menu.RestartFull();yield return Frames(3);
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }
}
#endif
