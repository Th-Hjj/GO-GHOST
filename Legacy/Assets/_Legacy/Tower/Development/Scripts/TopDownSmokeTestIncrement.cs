#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Focused confirmed-increment checks. Existing virtual input and isolated Archive only.
public partial class TopDownSmokeTest
{
    private IEnumerator Increment50Shot()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();var focus=StartCoroutine(S01Focus(flow));
        Record("50F paid-shot contact probe: DEV T3, virtual input, controlled reload delay, no forced hit; record actual projectile/prop state.");
        try
        {
            yield return FlowJump(flow,"floor50",3);yield return FlowNear(flow,flow.CurrentRoom.Receptionist);yield return FlowKey(Key.F);
            while(ui.CurrentNarrativeId=="npc.floor50.context")yield return FlowKey(Key.F);
            yield return FlowKey(Key.F);
            var fight=flow.ActiveFight;var gun=fight.Player.GetComponent<PlayerShooting>();var prop=flow.CurrentRoom.Prop;
            S01Field(gun,"<AmmoInMagazine>k__BackingField",0);S01Field(gun,"<IsReloading>k__BackingField",true);S01Field(gun,"reloadDoneAt",Time.time+3.4f);
            yield return FlowKey(Key.F);yield return WeaponsWait(()=>gun.TriggerPulls>0,5);
            Record("Shot: player="+fight.Player.transform.position+", aim="+fight.Player.GetComponent<PlayerInputReader>().AimDirection+", target="+prop.AimTarget?.position+", bullets="+string.Join(";",fight.GetComponentsInChildren<Projectile>().Select(p=>p.Actor+":"+p.transform.position+":"+p.GetComponent<Rigidbody2D>().linearVelocity)));
            yield return Seconds(1.4f);
            Record("After 1.4: hanging="+prop.IsHanging+", landed="+prop.HasLanded+", outcomes="+prop.OutcomeCount+", actor="+prop.LastActor+", pos="+prop.transform.position+", npcAlive="+flow.CurrentRoom.Receptionist.IsAlive+", shots="+gun.TriggerPulls);
            ScreenCapture.CaptureScreenshot("Logs/ConfirmedIncrement/floor50-shot.png");yield return Frames(2);
            // Move away using normal input so the officer cannot prop up the falling physical cover.
            Keys(Key.A);yield return Seconds(.6f);Keys();yield return WeaponsWait(()=>prop.HasLanded,3);
            Check("50F real single shot releases and lands",gun.TriggerPulls==1&&!prop.IsHanging&&prop.HasLanded&&prop.LastActor==ProjectileActor.InnerAI&&prop.OutcomeCount==1,"actual weapon/contact/terrain");
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }

    private IEnumerator IncrementCombat()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>(); var ui=flow.GetComponent<MissionUI>(); var focus=StartCoroutine(S01Focus(flow));
        Record("Confirmed increment: DEV jumps/fresh tiers, virtual input, invulnerability except laser damage probe; cover and direction probes reposition actors. Reload/invalid-target fixtures are explicit. Not balance or physical-input proof.");
        try
        {
            for(int tier=1;tier<=3;tier++)
            {
                yield return FlowJump(flow,"boss_p1",tier);
                var fight=flow.ActiveFight;var player=fight.Player;var boss=fight.Boss;var gun=player.GetComponent<PlayerShooting>();var reader=player.GetComponent<PlayerInputReader>();var prop=flow.CurrentRoom.Prop;
                yield return FlowKey(Key.F);
                Check("Existing You response T"+tier,ui.DisplayedSpeaker=="You"&&!ui.ChoicesVisible,ui.CurrentNarrativeId);
                yield return FlowKey(Key.F);
                Check("Unknown silence T"+tier,ui.DisplayedSpeaker=="???"&&ui.DisplayedNarrativeText=="......","before charge");
                yield return FlowKey(Key.F);Vector2 initial=boss.OpeningLaserDirection;
                Keys(Key.Space);yield return Frames(2);Keys();yield return WeaponsWait(()=>ui.DialogueVisible,2);
                Check("Tracking charge without path T"+tier,boss.State==CyberpsychoBoss.AttackState.OpeningLaserCharge&&!boss.OpeningLaserVisible&&Vector2.Distance(initial,boss.OpeningLaserDirection)>.005f,"real tracking, local muzzle charge only");
                Check("Exact advice and perception tier T"+tier,ui.DisplayedNarrativeText=="Shoot down the chandelier."&&ui.DisplayedSpeaker=="Voice in your head"&&prop.GetComponent<InteractionOutline>().Visible==(tier>=2),"blue outline gate");
                int shots=gun.TriggerPulls, ammo=gun.AmmoInMagazine;
                yield return FlowKey(Key.F);
                if(tier==3)
                {
                    Check("T3 second line precedes shot",ui.DisplayedNarrativeText=="Action confirmed."&&gun.TriggerPulls==shots,"still paused");
                    yield return FlowKey(Key.F);yield return WeaponsWait(()=>prop.HasLanded,3);
                    Check("P1 T3 one paid shot",gun.TriggerPulls==shots+1&&gun.AmmoInMagazine==ammo-1&&prop.LastActor==ProjectileActor.InnerAI&&!reader.HasTemporaryAimOverride,"real rifle");
                }
                else
                {
                    AimAtWorld(player.transform.position+Vector3.left*6,false);yield return Frames(3);
                    Check("T"+tier+" manual only",!reader.HasTemporaryAimOverride&&reader.AimDirection.x<-.99f&&gun.TriggerPulls==shots,"no automatic aim/fire");
                    yield return S01ManualHit(prop);
                }
                player.transform.position=new Vector3(-6,-1.7f,0);player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
                var danger=player.GetComponent<InnerAIDangerSense>();
                if(tier>=2) yield return WeaponsWait(()=>danger.Visible,4);
                Check("Attack warning tier T"+tier,tier==1?danger==null:danger.Visible&&danger.LastDirection.x>.8f,"source on right");
                if(tier==2){ScreenCapture.CaptureScreenshot("Logs/ConfirmedIncrement/right-arc.png");yield return Frames(2);}
                int hp=player.CurrentHealth;player.IsInvulnerable=false;
                yield return WeaponsWait(()=>boss.OpeningLaserShots==1,4);
                Check("Visible real laser blocked by cover T"+tier,boss.OpeningLaserVisible&&boss.OpeningLaserBlocked&&player.CurrentHealth==hp&&prop.OutcomeCount==1,"same physical cover");
                player.IsInvulnerable=true;
                if(tier==2)
                {
                    boss.StopCombat();foreach(var bullet in fight.GetComponentsInChildren<Projectile>())bullet.Retire();yield return Frames(2);
                    var body=prop.GetComponent<BoxCollider2D>();int blocked=prop.BlockedProjectiles;
                    AimAtWorld(body.bounds.center,true);yield return WeaponsWait(()=>prop.BlockedProjectiles>blocked,2);MouseTo(.1f,.8f);yield return Frames(2);
                    bool playerBlocked=prop.BlockedProjectiles>blocked;
                    foreach(var bullet in fight.GetComponentsInChildren<Projectile>())bullet.Retire();yield return Frames(2);blocked=prop.BlockedProjectiles;
                    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
                    var probe=Instantiate(prefab,new Vector3(body.bounds.max.x+1,body.bounds.center.y,0),Quaternion.identity);
                    probe.Launch(Vector2.left,12,9,boss.gameObject);yield return Seconds(.4f);
                    Check("Cover still blocks both projectile teams",playerBlocked&&probe==null&&prop.BlockedProjectiles==blocked+1,"real contacts");
                    player.transform.position=new Vector3(body.bounds.center.x,body.bounds.max.y+1,0);player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return Seconds(.4f);
                    float y=player.transform.position.y;yield return FlowKey(Key.Space);yield return Seconds(.12f);
                    Check("Cover remains standable/jumpable",player.transform.position.y>y+.4f,"existing controller");
                }
                flow.DevelopmentRepeatRoomEvent();yield return Frames(2);
                Check("No repeat intervention T"+tier,!ui.DialogueVisible&&prop.OutcomeCount==1,"once per encounter");
            }
            // 50F: a full normal radio interaction on each fresh tier, followed by staged Security.
            for(int tier=1;tier<=3;tier++)
            {
                yield return FlowJump(flow,"floor50",tier);var fight=flow.ActiveFight;var prop=flow.CurrentRoom.Prop;var gun=fight.Player.GetComponent<PlayerShooting>();
                Check("50F safe entry T"+tier,!fight.EncounterActive&&!ui.DialogueVisible,"waits for F");
                yield return FlowNear(flow,flow.CurrentRoom.Receptionist);yield return FlowKey(Key.F);
                while(ui.CurrentNarrativeId=="npc.floor50.context")yield return FlowKey(Key.F);
                Check("Security arrives before advice T"+tier,fight.BossHealth!=null&&!fight.EncounterActive&&ui.DisplayedNarrativeText=="Shoot down the chandelier.","staged Security");
                yield return FlowKey(Key.F);
                if(tier==3)
                {
                    Check("50F exact second line before combat",ui.DisplayedNarrativeText=="Action confirmed."&&!fight.EncounterActive&&gun.TriggerPulls==0,"no early attacks");
                    S01Field(gun,"<AmmoInMagazine>k__BackingField",0);S01Field(gun,"<IsReloading>k__BackingField",true);S01Field(gun,"reloadDoneAt",Time.time+3.4f);
                    yield return FlowKey(Key.F);yield return Seconds(3.05f);
                    Check("T3 reload wait is unpaused and staged",!ui.DialogueVisible&&Time.timeScale==1&&!fight.EncounterActive&&gun.TriggerPulls==0,"wait beyond retired 3s bound");
                    yield return WeaponsWait(()=>gun.TriggerPulls==1,2);
                    Check("Paid shot starts 50F combat before landing",fight.EncounterActive&&gun.AmmoInMagazine==gun.MagazineSize-1&&!prop.HasLanded,"no landing/death prerequisite");
                    yield return WeaponsWait(()=>prop.HasLanded,3);
                    Check("50F T3 release once via actual collision",gun.TriggerPulls==1&&prop.OutcomeCount==1&&prop.LastActor==ProjectileActor.InnerAI,"not dialogue attribution");
                }
                else Check("50F manual tiers start with hanging prop T"+tier,fight.EncounterActive&&prop.IsHanging&&gun.TriggerPulls==0,"no forced prop use");
                if(tier==2)
                {
                    var guard=fight.BossHealth;var player=fight.Player;
                    guard.transform.position=player.transform.position+new Vector3(-4,2,0);guard.GetComponent<Rigidbody2D>().gravityScale=0;Physics2D.SyncTransforms();
                    yield return WeaponsWait(()=>player.GetComponent<InnerAIDangerSense>()?.Visible==true,3);
                    var sense=player.GetComponent<InnerAIDangerSense>();
                    Check("Upper-left attack arc",sense!=null&&sense.Visible&&sense.LastDirection.x<-.5f&&sense.LastDirection.y>0,"actual guard preparation");
                    ScreenCapture.CaptureScreenshot("Logs/ConfirmedIncrement/upper-left-arc.png");yield return Frames(2);
                    int pulses=sense!=null?sense.PulseCount:0;
                    guard.transform.position=player.transform.position+Vector3.right*4;Physics2D.SyncTransforms();
                    yield return WeaponsWait(()=>sense!=null&&sense.PulseCount>pulses&&sense.Visible,4);
                    Check("Next attack warns from right",sense!=null&&sense.PulseCount>pulses&&sense.LastDirection.x>.8f,"not combat-entry-only");
                }
            }
            // Invalid chandelier during a legal weapon wait must release staged Security without firing.
            yield return FlowJump(flow,"floor50",3);yield return FlowNear(flow,flow.CurrentRoom.Receptionist);yield return FlowKey(Key.F);
            while(ui.CurrentNarrativeId=="npc.floor50.context")yield return FlowKey(Key.F);
            yield return FlowKey(Key.F);var waitingGun=flow.ActiveFight.Player.GetComponent<PlayerShooting>();S01Field(waitingGun,"nextFireTime",Time.time+4);
            yield return FlowKey(Key.F);flow.CurrentRoom.Prop.AimTarget.GetComponent<Collider2D>().enabled=false;yield return Frames(5);
            Check("Invalid target cancels and continues 50F",flow.ActiveFight.EncounterActive&&waitingGun.TriggerPulls==0&&!flow.ActiveFight.NarrativePaused,"no invalid shot/deadlock");
            yield return FlowJump(flow,"boss_p1",1);yield return FlowClose(ui);yield return WeaponsWait(()=>ui.DialogueVisible,2);yield return FlowClose(ui);
            var bare=flow.ActiveFight;bare.Player.IsInvulnerable=false;int before=bare.Player.CurrentHealth;
            yield return WeaponsWait(()=>bare.Boss.OpeningLaserShots==1,5);
            Check("Actual uncovered laser still visible and damaging",bare.Boss.OpeningLaserVisible&&bare.Player.CurrentHealth<before,"real PlayerHealth damage");
            bare.Player.IsInvulnerable=true;yield return FlowShootBoss(flow);yield return S02WalkExit(flow);
            Check("P1 real combat continues to 50F",flow.TowerStageId=="floor50"&&!ui.DialogueVisible,"same run, invulnerability assist");
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }

    private IEnumerator IncrementInteraction()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();var ui=flow.GetComponent<MissionUI>();var focus=StartCoroutine(S01Focus(flow));
        Record("Increment interaction: isolated Archive, DEV room jumps, virtual mouse/F/W/S, actor reposition; final target kills use actual rifle. Not hardware-input or normal-route reachability proof.");
        try
        {
            yield return FlowJump(flow,"boss_p2",2);yield return WeaponsWait(()=>ui.DialogueVisible,4);
            Check("P2 reveal waits for actual introduction",ui.DisplayedSpeaker=="???"&&ui.DisplayedNarrativeText=="I am the Puppeteer."&&!flow.PuppeteerIntroduced,"one new line");
            yield return FlowKey(Key.F);
            Check("P2 name changes only after line ends",flow.PuppeteerIntroduced&&ui.DisplayedSpeaker=="Puppeteer"&&ui.CurrentNarrativeId=="boss.p2.intro","existing content follows");
            yield return FlowClose(ui);
            Check("P2 continues existing combat",flow.ActiveFight.EncounterActive&&!ui.DialogueVisible,"no added questions/branches");
            yield return FlowJump(flow,"floor99",2);yield return Frames(10);
            Check("99F entry remains voluntary",!ui.DialogueVisible&&!flow.ActiveFight.EncounterActive,"entry");
            AimAtWorld(flow.ActiveFight.BossHealth.transform.position,true);yield return WeaponsWait(()=>flow.ActiveFight.EncounterActive,3);MouseTo(.2f,.8f);
            Check("99F real attack starts fight directly",flow.ActiveFight.EncounterActive&&!ui.DialogueVisible,"no confirmation speech");
            yield return FlowJump(flow,"office_intro",1);yield return Frames(10);
            Check("Office waits for voluntary interaction",!ui.DialogueVisible,"space before dialogue");
            // Approach through actual movement to the existing barrier; do not teleport through it.
            Keys(Key.D);yield return WeaponsWait(()=>ui.InteractionKeyVisible,4);Keys();yield return Frames(3);yield return FlowKey(Key.F);
            Check("Office F reachable outside barrier",ui.CurrentNarrativeId=="office.intro","physical approach");
            while(ui.DialogueVisible&&ui.CurrentNarrativeId!="office.summon")yield return FlowKey(Key.F);
            yield return WeaponsWait(()=>ui.CurrentNarrativeId=="office.summon",2);
            Check("Guaranteed English President summon",ui.DisplayedNarrativeText=="Puppeteer, protect me."&&ui.DisplayedSpeaker=="CEO",ui.DisplayedNarrativeText);
            ScreenCapture.CaptureScreenshot("Logs/ConfirmedIncrement/president-english.png");yield return Frames(2);
            yield return FlowClose(ui);yield return WeaponsWait(()=>flow.TowerStageId=="boss_p3"&&flow.ActiveFight.EncounterActive,3);
            Check("Office continues to P3",flow.TowerStageId=="boss_p3"&&flow.ActiveFight.EncounterActive,"internal mode retained");

            yield return FlowJump(flow,"final_disposition",2);yield return FlowNear(flow,flow.President);yield return FlowKey(Key.F);
            var gun=flow.ActiveFight.Player.GetComponent<PlayerShooting>();int shots=gun.TriggerPulls;
            DialoguePointer(ui,1);yield return Frames(3);
            Check("Hover highlights without committing",ui.SelectedChoiceId=="office.final.ceo.spare"&&flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Unresolved,"hover");
            yield return FlowKey(Key.W);Check("W changes selection",ui.SelectedChoiceId=="office.final.ceo.kill","keyboard");yield return FlowKey(Key.S);
            DialoguePointer(ui,1,true);yield return Frames(3);
            Check("First click only selects",ui.ChoicesVisible&&flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Unresolved,"two-step mouse");
            DialoguePointer(ui,1);yield return Frames(3);DialoguePointer(ui,1,true);yield return Frames(4);
            Check("Second click commits without weapon leak",!ui.DialogueVisible&&flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Spare&&gun.TriggerPulls==shots&&!ui.CanLeaveWorld,"held closing input");
            MouseTo(.2f,.8f);yield return Frames(3);
            yield return FlowFinalChoice(flow,flow.ContinuingCore,2);
            Check("Both dispositions leave world active",flow.State==GameSessionFlow.FlowState.Combat&&flow.CurrentRoom.ExitOpen&&flow.TowerCompletionCount==0&&flow.Narrative.FrozenExit==TowerNarrativeState.ExitResult.Unresolved,"Control chosen, no early E");
            AimAtWorld(flow.President.InteractionAnchor.position-Vector3.up*.9f,true);yield return WeaponsWait(()=>!flow.President.IsAlive,4);MouseTo(.2f,.8f);yield return Frames(4);
            Check("Actual death overrides prior Spare before exit",!flow.President.IsAlive&&flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Execute&&flow.TowerCompletionCount==0,"no corpse F");
            flow.ActiveFight.Player.transform.position=flow.CurrentRoom.Offset+new Vector2(11,-1.7f);Keys(Key.D);
            yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Result,3);Keys();
            Check("Exit judges actual lives once, never dead-CEO E",flow.TowerEndingId=="ending.A"&&flow.TowerCompletionCount==1,"leave through existing door");
            yield return FlowJump(flow,"final_disposition",1);
            yield return FlowNear(flow,flow.ContinuingCore);AimAtWorld(flow.ContinuingCore.InteractionAnchor.position-Vector3.up*.9f,true);yield return WeaponsWait(()=>!flow.ContinuingCore.IsAlive,4);MouseTo(.2f,.8f);yield return Frames(3);
            Check("Real core death commits while other fate stays open",flow.Narrative.CoreDisposition==TowerNarrativeState.CoreDecision.Destroy&&flow.Narrative.CeoDisposition==TowerNarrativeState.CeoDecision.Unresolved&&flow.TowerCompletionCount==0,"no corpse F or ending freeze");
            Check("DEV tests leave isolated Archive empty",PoliceArchive.Current.RunRecords.Count==0,"normal save untouched");
        }
        finally{StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }
}
#endif
