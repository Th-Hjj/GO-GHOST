#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Alpha 50 Focused Smoke")]
    public static void RunAlpha50()=>Start("alpha50",false);
    public static void RunAlpha50Edges()=>Start("alpha50-edges",false);
    public static void RunAlpha50Layouts()=>Start("alpha50-layouts",false);
    static void Rule(ConditionalLoadout d,int op,int condition,string response)
    {d.Operations[op].Rules.Add(new ConditionalRule{Condition=condition,Action=response});}
    static IEnumerator AlphaFresh(int wave,Action<ConditionalLoadout> configure,bool stop=true)
    {
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.ConfigurePlaytestWave(wave);configure?.Invoke(game.DraftLoadout);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        if(stop)StopPolicies();yield return Frames(4);
    }
    static ConditionalController.ExecutionFeedback Feedback(int op,int row=0)=>game.Rules.Feedback((ControlOperation)op,row);
    static UnityEngine.UI.Button ConfigButtonNamed(string name,string label=null)
    {
        return Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>()
            .First(b=>b.gameObject.activeInHierarchy&&b.name==name&&(label==null||b.GetComponentInChildren<TMPro.TMP_Text>().text==label));
    }
    static void ClickConfig(string name,string label=null)=>ConfigButtonNamed(name,label).onClick.Invoke();
    static void AlphaPlace(float playerX,float bossX)
    {
        OnFloor(game.Player.gameObject,game.Bosses[0].Navigation,playerX);
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,bossX);
    }
    static IEnumerator Alpha50()
    {
        Record("ALPHA RULE EXPANSION: bounded controlled fixtures, original physical executors and paid shots; virtual input only.");
        game.NewGame();yield return Frames(2);
        Check("empty defaults, no independent toggle",game.DraftLoadout.Operations.All(o=>o.Rules.Count==0)
            &&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Toggle>().Length==0);
        ClickConfig("AddRule");yield return Frames(2);
        Check("Move picker contains exactly its eleven applicable conditions",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name=="Option")==11);
        ClickConfig("Option","Enemy Close");yield return Frames(2);
        game.StartFight();
        Check("incomplete pair remains UI-local and blocks Start Fight",game.DraftLoadout.Operations[0].Rules.Count==0&&game.Phase==DuelPhase.Configure);
        ClickConfig("Option","Retreat from Enemy");yield return Frames(2);
        Check("complete pair equips without a switch",game.DraftLoadout.Operations[0].Rules.Count==1&&ConditionalCatalog.IsValid(game.DraftLoadout));
        Check("one rule leaves shared slots available",ConfigButtonNamed("AddRule")!=null);
        var copy=game.DraftLoadout.Copy();copy.Operations[0].Rules[0].Condition=9;
        Check("snapshot is a deep copy",game.DraftLoadout.Operations[0].Rules[0].Condition==33);
        game.EditLoadout(d=>{for(int i=1;i<5;i++)Rule(d,0,33,i%2==0?"M01":"M02");});yield return Frames(2);
        Check("five rules in one operation validate and render",ConditionalCatalog.IsValid(game.DraftLoadout)&&game.DraftLoadout.TotalRules==5
            &&ConfigButtonNamed("Rule4Condition")!=null&&!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="AddRule"));
        bool sixth=game.DraftLoadout.TryAddRule(ControlOperation.Dodge,new ConditionalRule{Condition=1,Action="D04"});
        Check("sixth rule rejected by the shared budget",!sixth&&game.DraftLoadout.TotalRules==5);
        ClickConfig("RemoveRule4");yield return Frames(2);
        Check("removing a rule immediately frees shared capacity",game.DraftLoadout.TotalRules==4&&ConfigButtonNamed("AddRule")!=null);
        game.EditLoadout(d=>d.Operations[0].Rules.Clear());yield return Frames(2);
        Check("delete last rule restores empty manual list",game.DraftLoadout.Operations[0].Rules.Count==0&&ConfigButtonNamed("AddRule")!=null);
        Rule(game.DraftLoadout,0,33,"M10");game.StartFight();
        Check("Start Fight rejects an unapproved historical action",game.Phase==DuelPhase.Configure);
        var split=new ConditionalLoadout();for(int i=0;i<3;i++)Rule(split,0,33,"M02");for(int i=0;i<2;i++)Rule(split,4,1,"D04");
        Check("3+2 distribution and ordered full copy validate",ConditionalCatalog.IsValid(split)&&split.Copy().TotalRules==5&&split.Copy().Operations[0].Rules.Count==3);
        Rule(split,1,33,"J01");Check("directly injected sixth rule remains invalid after copying",!ConditionalCatalog.IsValid(split)&&!ConditionalCatalog.IsValid(split.Copy()));
        Check("formal catalog has 21 unique A and 19 unique B",ConditionalCatalog.FormalConditions.SelectMany(c=>c).Distinct().Count()==21
            &&Enumerable.Range(0,5).SelectMany(op=>ConditionalCatalog.Responses.Where(b=>(int)b.Operation==op&&ConditionalCatalog.FormalConditions[op].Any(a=>ConditionalCatalog.IsAllowed(op,a,b.Id)))).Select(b=>b.Id).Distinct().Count()==19);
        Check("event and source compatibility is shared and bounded",!ConditionalCatalog.IsAllowed(1,6,"J03")&&!ConditionalCatalog.IsAllowed(3,12,"F05")
            &&ConditionalCatalog.IsAllowed(3,6,"F03")&&ConditionalCatalog.IsAllowed(3,11,"F05")&&!ConditionalCatalog.IsAllowed(2,4,"Q03")
            &&ConditionalCatalog.IsAllowed(2,32,"Q03")&&ConditionalCatalog.IsAllowed(0,6,"M04"));
        yield return AlphaFresh(1,null);yield return CheckInputs();

        yield return AlphaFresh(1,d=>{for(int i=0;i<5;i++)Rule(d,0,33,i==0?"M01":"M02");});AlphaPlace(-5,-2);yield return Frames(3);
        var body=game.Player.GetComponent<Rigidbody2D>();var reader=game.Player.GetComponent<PlayerInputReader>();float x=body.position.x;yield return Seconds(.12f);
        Check("five same-operation rows run highest executable priority",body.position.x>x+.3f&&Feedback(0,0).Executing&&!Feedback(0,1).Executing&&game.Rules.RuleCount(ControlOperation.Movement)==5);
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(3);
        Check("retry retains all five ordered rules",game.DraftLoadout.TotalRules==5&&game.DraftLoadout.Operations[0].Rules[0].Action=="M01");
        ClickConfig("MoveRuleUp1");yield return Frames(2);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,-2);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();x=body.position.x;yield return Seconds(.12f);
        Check("reordered priority changes actual movement",body.position.x<x-.3f&&game.Rules.RuleAt(ControlOperation.Movement,0).Action=="M02"&&Feedback(0,0).Executing);
        foreach(var a in game.Bosses)a.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure,1.2f);
        Check("next wave retains all five rules in changed order",game.Wave==2&&game.DraftLoadout.TotalRules==5
            &&game.DraftLoadout.Operations[0].Rules[0].Action=="M02"&&game.DraftLoadout.Operations[0].Rules[1].Action=="M01");
        game.StartFight();yield return Frames(2);
        Check("next wave copies five fresh rows without old execution feedback",game.Phase==DuelPhase.Prepare&&game.Rules.RuleCount(ControlOperation.Movement)==5&&Feedback(0,0).Glow==0);
        game.NewGame();yield return Frames(2);Check("New Game clears the expanded loadout",game.DraftLoadout.TotalRules==0);

        yield return AlphaFresh(1,d=>{Rule(d,0,45,"M02");Rule(d,0,45,"M09");});AlphaPlace(-14.1f,-11);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();yield return Until(()=>Feedback(0,1).Glow>0,.4f);
        Check("blocked high row yields to physically accepted Center movement",Feedback(0,1).Glow>0,$"x={body.position.x:F2} velocity={body.linearVelocity}");
        yield return AlphaFresh(1,d=>{Rule(d,0,4,"M02");Rule(d,0,33,"M01");});AlphaPlace(-6,-2);yield return Frames(3);
        reader=game.Player.GetComponent<PlayerInputReader>();
        yield return Until(()=>Feedback(0,1).Executing,.3f);
        Check("lower row starts while higher condition is false",reader.MoveInput.x>0&&Feedback(0,1).Executing);
        game.Player.TakeDamage(100,game.Bosses[0].gameObject);yield return Until(()=>Feedback(0,0).Executing,.3f);
        Check("newly executable higher row preempts lower response",reader.MoveInput.x<0&&Feedback(0,0).Executing&&!Feedback(0,1).Executing);

        yield return AlphaFresh(3,d=>Rule(d,0,6,"M04"));
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,2);OnFloor(sword.gameObject,sword.Navigation,-9);yield return Frames(4);
        body=game.Player.GetComponent<Rigidbody2D>();var movement=game.Player.GetComponent<PlayerMovement>();
        game.Player.TakeDamage(1,gunner.gameObject);bool pushed=movement.ApplyHitPush(-1,.45f,.25f);yield return Seconds(.1f);
        Check("damage response waits during real hit-push ownership",pushed&&movement.HitPushActive&&!Feedback(0).Executing);
        yield return Until(()=>!movement.HitPushActive,.5f);x=body.position.x;yield return Seconds(.3f);
        Check("Took Damage Step Back survives bounded hit reaction and uses source",body.position.x<x-.6f&&Feedback(0).Glow>0,$"afterPush={x:F2}->{body.position.x:F2}");
        yield return Seconds(.6f);x=body.position.x;yield return Seconds(.25f);
        Check("finite Step Back is consumed once with no permanent ownership",Mathf.Abs(body.position.x-x)<.08f&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned);

        yield return AlphaFresh(1,d=>Rule(d,0,35,"M03"));AlphaPlace(-8,5);yield return Frames(3);
        game.Bosses[0].Motor.SetMove(-1);yield return Seconds(.1f);
        Check("observed approach starts Keep Mid Range",Feedback(0).Executing&&game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned);
        game.Bosses[0].Motor.Brake();yield return Until(()=>!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned,1.4f);
        Check("stable mid range returns only Move to manual",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned);

        // Stay outside platform shadows so the persistent public Close condition remains true after landing.
        yield return AlphaFresh(1,d=>Rule(d,1,33,"J03"));AlphaPlace(-12,-8);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();reader=game.Player.GetComponent<PlayerInputReader>();movement=game.Player.GetComponent<PlayerMovement>();
        yield return Until(()=>game.Rules.AcceptedJumps>0,.4f);int jumps=game.Rules.AcceptedJumps;x=body.position.x;yield return Seconds(.25f);
        Check("Repeat Jumps makes one accepted takeoff per airtime",jumps==1&&game.Rules.AcceptedJumps==jumps&&body.linearVelocity.y!=0&&!reader.ConditionalFrame.MovementOwned);
        yield return Until(()=>game.Rules.AcceptedJumps>=2,1.4f);
        Check("Repeat Jumps repeats legally after landing without horizontal navigation",game.Rules.AcceptedJumps>=2&&Mathf.Abs(body.position.x-x)<.1f,
            $"jumps={game.Rules.AcceptedJumps} grounded={movement.IsGrounded} position={body.position} velocity={body.linearVelocity}");

        yield return AlphaFresh(3,d=>Rule(d,2,32,"Q03"));gunner=game.Bosses.First(a=>a.ActorId==1);sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);yield return Frames(3);
        bool camo=gunner.Actions.TryStart(BossAbility.Camo,game.Player.transform.position);yield return Frames(5);
        Check("hidden body cannot own Triggering Enemy Aim",camo&&gunner.IsCamo&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        gunner.Actions.StopCombat();yield return Frames(4);
        Check("actual observed reappearance preserves its source over nearer enemy",game.Rules.CurrentTargetId==gunner.ActorId&&Feedback(2).Executing&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned);
        yield return Seconds(1);Check("reappearance Aim has a bounded event response",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);

        yield return AlphaFresh(3,d=>Rule(d,2,4,"Q02"));gunner=game.Bosses.First(a=>a.ActorId==1);sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        gunner.Health.TakeDamage(gunner.Health.MaxHealth/2,game.Player.gameObject);game.Player.TakeDamage(100,sword.gameObject);yield return Frames(4);
        Check("Lowest Health Aim uses visible public fraction over nearest distance",game.Rules.CurrentTargetId==gunner.ActorId&&Feedback(2).Executing&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned);

        yield return AlphaFresh(1,d=>Rule(d,2,18,"Q06"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Bosses[0].Body.bounds.center+Vector3.up*.4f,false);yield return Frames(4);
        Check("near cursor enables actual Assist Mouse Aim without Fire",Feedback(2).Executing&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned&&game.Rifle.TriggerPulls==0);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);yield return Frames(4);
        Check("large mouse intention change releases Assist Aim",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);

        yield return AlphaFresh(1,d=>Rule(d,3,40,"F01"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Bosses[0].Body.bounds.center,false);yield return Until(()=>game.Rifle.TriggerPulls>0,.4f);
        Check("Fire One Shot pays exactly one real round and lights Fire",game.Rifle.TriggerPulls==1&&game.Rifle.AmmoInMagazine==7&&Feedback(3).Glow>0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        yield return Seconds(.8f);Check("same aligned state cannot repeat one-shot action",game.Rifle.TriggerPulls==1&&Feedback(3).Glow==0);
        yield return AlphaFresh(1,d=>{Rule(d,3,6,"F01");Rule(d,3,6,"F01");});AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);game.Player.TakeDamage(1,game.Bosses[0].gameObject);yield return Seconds(.7f);
        Check("duplicate damage rules independently consume the same accepted event",game.Rifle.TriggerPulls==2&&game.Rifle.AmmoInMagazine==6&&Feedback(3,1).LastExecutedAt>Feedback(3,0).LastExecutedAt,$"shots={game.Rifle.TriggerPulls}");
        yield return AlphaFresh(1,d=>Rule(d,3,6,"F03"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);game.Player.TakeDamage(1,game.Bosses[0].gameObject);
        yield return Seconds(.95f);int eventShots=game.Rifle.TriggerPulls;yield return Seconds(.3f);
        Check("event Keep Firing pays shots then expires without indefinite firing",eventShots>0&&eventShots<=4
            &&game.Rifle.TriggerPulls==eventShots&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned,$"shots={eventShots}");
        yield return AlphaFresh(1,d=>Rule(d,3,40,"F05"));AlphaPlace(-6,0);yield return Frames(3);MouseAt(game.Bosses[0].Body.bounds.center,false);
        yield return Until(()=>game.Rifle.AmmoInMagazine<=2,2f);yield return Seconds(.4f);
        Check("Fire Reserve stops at exactly two rounds without reload",game.Rifle.TriggerPulls==6&&game.Rifle.AmmoInMagazine==2&&!game.Rifle.IsReloading);
        heldKeys=new[]{Key.D};MouseAt(game.Player.transform.position+Vector3.up*5,false);yield return Frames(4);
        Check("unmatched Fire preserves independent current manual Move",game.Player.GetComponent<PlayerInputReader>().MoveInput.x>0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned);heldKeys=Array.Empty<Key>();

        yield return AlphaFresh(1,d=>{for(int i=0;i<3;i++)Rule(d,0,12,"M02");Rule(d,4,41,"D03");Rule(d,4,1,"D04");});AlphaPlace(-6,2);yield return Frames(3);
        Check("3+2 distribution creates all five live independent rows",game.Rules.RuleCount(ControlOperation.Movement)==3&&game.Rules.RuleCount(ControlOperation.Dodge)==2);
        var liveController=game.Rules;checkingPause=true;yield return DeveloperKey(Key.F8);
        Check("existing F8 opens and pauses the expanded live controller",game.DeveloperToolsOpen&&game.Paused&&game.Rules==liveController);
        yield return DeveloperKey(Key.F8);checkingPause=false;
        Check("F8 closes and restores combat with the same rule controller",!game.DeveloperToolsOpen&&!game.Paused&&game.Rules==liveController);
        heldKeys=new[]{Key.A};yield return Frames(3);body=game.Player.GetComponent<Rigidbody2D>();x=body.position.x;
        Fire(game.Bosses[0],game.Bosses[0].gameObject,game.Player.GetComponent<Collider2D>().bounds.center+Vector3.right*3,Vector2.left);
        yield return Until(()=>game.Dodge.IsDodging,.25f);yield return Seconds(.06f);
        Check("incoming real bullet Dodge follows final resolved movement",game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0&&game.Dodge.CommittedHorizontalSpeed<0);heldKeys=Array.Empty<Key>();
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D04"));AlphaPlace(-6,2);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();MouseAt(game.Player.transform.position+Vector3.left*5,false);yield return Frames(3);x=body.position.x;
        heldKeys=new[]{Key.Space};yield return Until(()=>game.Dodge.IsDodging,.5f);heldKeys=Array.Empty<Key>();yield return Seconds(.06f);
        Check("airborne Dodge uses actual world cursor horizontal direction",game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0&&game.Dodge.CommittedHorizontalSpeed<0);

        yield return AlphaDodgeCase();
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>{Rule(d,0,9,"M02");Rule(d,2,17,"Q01");Rule(d,1,43,"J01");Rule(d,3,40,"F03");Rule(d,4,41,"D01");},false);
            MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Seconds(3);
            Record($"SHORT W{wave} phase={game.Phase} time={game.FightClock:F2} paidShots={game.Rifle.TriggerPulls} hp={game.Player.CurrentHealth}");mouseHeld=false;
        }
        game.MainMenu();yield return Frames(3);
        Check("Menu cleans controller and restores full camera",game.Rules==null&&Camera.main.rect.width==1&&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0);
        Record("NOT TESTED: physical input, balance, natural completion, exhaustive historical combinations, long sessions and audio audition.");
    }
    static IEnumerator AlphaDodgeCase()
    {
        yield return AlphaFresh(1,d=>Rule(d,4,25,"D01"));
        AlphaPlace(-5,1);yield return Seconds(.1f);
        var actor=game.Bosses[0];var body=game.Player.GetComponent<Rigidbody2D>();var movement=game.Player.GetComponent<PlayerMovement>();
        float x=body.position.x,started=game.Dodge.LastDodgeStartedAt;
        bool prepared=actor.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);yield return Seconds(.06f);
        Check("A25 accepts real Dodge away during public preparation",prepared&&game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0,
            $"x={x:F2}->{body.position.x:F2} phase={actor.Actions.Phase} cooldown={game.Dodge.CooldownRemaining:F2}");
        Check("automatic Dodge uses 2.0 second cooldown",Mathf.Approximately(game.Dodge.DodgeStartInterval,2f)&&game.Dodge.CooldownRemaining>1.8f);
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D02"));AlphaPlace(-5,3);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();movement=game.Player.GetComponent<PlayerMovement>();
        started=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);heldKeys=Array.Empty<Key>();
        Check("manual Dodge starts same 2.0 second cooldown without rule glow",game.Dodge.LastDodgeStartedAt>started
            &&game.Dodge.CooldownRemaining>1.8f&&Feedback(4).Glow==0);
        yield return Until(()=>game.Dodge.CooldownRemaining<=.3f,2f);
        heldKeys=new[]{Key.Space};yield return Until(()=>!movement.IsGrounded&&body.linearVelocity.y>1,.3f);heldKeys=Array.Empty<Key>();
        started=game.Dodge.LastDodgeStartedAt;
        Check("air rule cooldown rejection has no new row glow",game.Dodge.CooldownRemaining>0&&!game.Dodge.TryDodge(1)&&Feedback(4).Glow==0);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.9f);x=body.position.x;yield return Seconds(.06f);
        Check("A01 executes toward public target after full cooldown",game.Dodge.LastDodgeStartedAt>=started+2f&&body.position.x>x+.3f&&Feedback(4).Glow>0&&!movement.IsGrounded,
            $"x={x:F2}->{body.position.x:F2} startInterval={game.Dodge.LastDodgeStartedAt-started:F3}");
        var instance=game.Rules;float glow=Feedback(4).Glow;int accepted=game.Rules.AcceptedDodges;
        checkingPause=true;game.SetPause(true);float clock=Time.time;yield return RealSeconds(.18);
        Check("pause freezes row feedback time and action state",Time.time==clock&&Feedback(4).Glow==glow&&game.Rules==instance);
        game.SetPause(false);checkingPause=false;yield return Seconds(.25f);
        Check("resume rebinds same controller without replay",game.Rules==instance&&game.Rules.AcceptedDodges==accepted);
        yield return Until(()=>movement.IsGrounded,1.2f);yield return Seconds(.6f);
        Check("landing does not replay invalid airborne occurrence",game.Rules.AcceptedDodges==accepted&&Feedback(4).Glow==0);
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(2);
        Check("failure retains single-rule draft, disposes old feedback",game.Phase==DuelPhase.Configure&&game.Rules==null&&game.DraftLoadout.Operations[4].Rules.Count==1&&game.DraftLoadout.Operations[4].Rules[0].Condition==1&&instance.Feedback(ControlOperation.Dodge,0).Glow==0);
        game.StartFight();yield return Frames(2);
        Check("retry preparation starts fresh unlit snapshot",game.Phase==DuelPhase.Prepare&&game.Rules!=instance&&Feedback(4).Glow==0);
    }
    static IEnumerator Alpha50Edges()
    {
        Record("CONTROLLED public preparations, blocked response, one-shot priority and next-wave reset.");
        yield return AlphaFresh(3,d=>Rule(d,4,25,"D01"));
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        // Preserve the shared major-attack budget; do not force an illegal overlapping tell.
        float before=game.Dodge.LastDodgeStartedAt;
        bool s=sword.Actions.TryStart(BossAbility.Rhythm,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>before,.25f);float first=game.Dodge.LastDodgeStartedAt;
        yield return Seconds(.18f);
        bool g=gunner.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.3f);
        Check("major attack mutual exclusion and Dodge cooldown retained",s&&!g&&game.Dodge.LastDodgeStartedAt==first);
        yield return Until(()=>!sword.Actions.Busy&&game.Dodge.CanDodge,4f);
        yield return Seconds(.8f);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);yield return Frames(3);
        var dodgeBody=game.Player.GetComponent<Rigidbody2D>();
        float startX=dodgeBody.position.x, beforeGunner=game.Dodge.LastDodgeStartedAt;
        float away=Mathf.Sign(startX-gunner.Body.bounds.center.x);
        g=gunner.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.08f);
        Check("independent next source accepts a second real Dodge",g&&game.Dodge.LastDodgeStartedAt>beforeGunner&&game.Dodge.IsDodging&&(dodgeBody.position.x-startX)*away>.3f&&Feedback(4,0).Glow>0,
            $"before={beforeGunner:F3} next={game.Dodge.LastDodgeStartedAt:F3} bodyX={startX:F2}->{dodgeBody.position.x:F2} direction={game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.DodgeDirection:F0}");
        yield return Until(()=>!gunner.Actions.Busy,4f);yield return Seconds(.8f);
        bool next=sword.Actions.TryStart(BossAbility.Guard,game.Player.transform.position);
        yield return Frames(3);
        Check("defensive Guard is not an attack preparation trigger",!sword.TryGetContact(out var contact)||!contact.Preparing);
        Record("Sustained overlapping major preparations: NOT OBSERVED (shared major-attack budget retained). No forced cooldown.");

        yield return AlphaFresh(1,d=>{Rule(d,1,33,"J01");Rule(d,4,25,"D01");});
        AlphaPlace(-6,1);yield return Seconds(.1f);var actor=game.Bosses[0];
        actor.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position);
        OnFloor(actor.gameObject,actor.Navigation,-4);yield return Frames(3);
        var body=game.Player.GetComponent<Rigidbody2D>();
        Check("accepted Dodge suppresses simultaneous conditional Jump",game.Dodge.IsDodging&&body.linearVelocity.y<1&&Feedback(4).Glow>0&&Feedback(1).Glow==0);
        yield return Until(()=>body.linearVelocity.y>1,.4f);
        Check("rejected Jump retries while Close remains true",body.linearVelocity.y>1&&Feedback(1).Glow>0);

        yield return AlphaFresh(1,d=>Rule(d,0,33,"M02"));
        AlphaPlace(-14.1f,-11);yield return Seconds(.45f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Check("real wall blockage releases Move",!reader.ConditionalFrame.MovementOwned&&!Feedback(0).Executing);
        foreach(var a in game.Bosses)a.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure,1.2f);
        Check("next wave retains completed rules",game.Wave==2&&game.DraftLoadout.Operations[0].Rules.Count==1);
        game.StartFight();yield return Frames(2);Check("next wave preparation has no old glow",Feedback(0).Glow==0);
        game.NewGame();Check("New Game clears all rules",game.DraftLoadout.Operations.All(o=>o.Rules.Count==0));
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator Alpha50Layouts()
    {
        Record("LAYOUT: real Game View captures, complete snapshots, no image mockups.");
        foreach(int width in new[]{1280,1920})
        {
            foreach(int count in new[]{0,1,5})
            {
                yield return AlphaFresh(3,d=>
                {
                    if(count==1)Rule(d,4,25,"D01");
                    if(count==5)
                    {
                        // Full-health actors keep these rows inactive while mouse conversion is measured.
                        foreach(string action in new[]{"M01","M02","M03","M04","M09"})Rule(d,0,4,action);
                    }
                });
                SetAlphaGameSize(width,width*9/16);yield return Frames(10);
                var camera=Camera.main;var arena=game.ArenaBounds;
                Vector3 min=camera.WorldToViewportPoint(arena.min),max=camera.WorldToViewportPoint(arena.max);
                Check($"{width} count={count}: entire arena inside battle viewport",min.x>=0&&min.y>=0&&max.x<=1&&max.y<=1,$"min={min} max={max}");
                var sidebar=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="BattleRules");
                Check($"{width} count={count}: sidebar visibility",(sidebar!=null)==(count>0));
                if(sidebar!=null)
                {
                    var labels=sidebar.GetComponentsInChildren<TMPro.TMP_Text>();
                    Check($"{width} count={count}: labels fit and remain mouse-transparent",labels.All(t=>!t.isTextOverflowing&&!t.raycastTarget)
                        &&sidebar.GetComponentsInChildren<UnityEngine.UI.Selectable>().Length==0);
                }
                MouseAt(game.Player.transform.position+new Vector3(5,2),false);yield return Frames(4);
                var expected=((Vector2)game.Player.transform.position+new Vector2(5,2)-game.Rifle.AimOrigin).normalized;
                Check($"{width} count={count}: manual mouse world aim",Vector2.Dot(game.Player.GetComponent<PlayerInputReader>().AimDirection,expected)>.999f);
                Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+$"alpha50-rules-{count}-{width}.png");yield return Frames(4);
            }
        }
        foreach(int width in new[]{1280,1920})
        {
            RestoreFixture();game.NewGame();SetAlphaGameSize(width,width*9/16);yield return Frames(5);
            game.EditLoadout(d=>{for(int i=0;i<5;i++)Rule(d,0,33,i%2==0?"M01":"M02");});yield return Frames(5);
            var ui=Object.FindAnyObjectByType<DuelUI>();var editor=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleEditor");
            Check($"{width}: five configuration rows fit with accessible Start Fight",editor.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing)
                &&ConfigButtonNamed("Rule4Condition")!=null&&ConfigButtonNamed("MoveRuleUp4").interactable&&ConfigButtonNamed("StartFight").interactable);
            ScreenCapture.CaptureScreenshot(Output+$"alpha50-config-five-{width}.png");yield return Frames(3);
        }
        RestoreFixture();game.NewGame();yield return Frames(3);
        ClickConfig("Operation4");yield return Frames(1);ClickConfig("AddRule");yield return Frames(2);
        ScreenCapture.CaptureScreenshot(Output+"alpha50-local-picker.png");yield return Frames(3);
        game.MainMenu();yield return Frames(3);
    }
    static void SetAlphaGameSize(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.FlattenHierarchy;
        object sizes=sizesType.GetProperty("instance",flags).GetValue(null);
        object groupType=sizesType.GetProperty("currentGroupType",flags).GetValue(sizes);
        object group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{groupType});
        var groupClass=group.GetType();int builtin=(int)groupClass.GetMethod("GetBuiltinCount",flags).Invoke(group,null);
        int custom=(int)groupClass.GetMethod("GetCustomCount",flags).Invoke(group,null);
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        object size=Activator.CreateInstance(sizeType,flags,null,new[]{Enum.ToObject(kind,1),(object)width,height,"Alpha50 validation"},null);
        groupClass.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selected=view.GetType().GetProperty("selectedSizeIndex",flags);int original=(int)selected.GetValue(view);
        selected.SetValue(view,builtin+custom);view.Repaint();
        restoration.Add(()=>{if(view!=null)selected.SetValue(view,original);groupClass.GetMethod("RemoveCustomSize",flags).Invoke(group,new object[]{builtin+custom});});
    }
}
#endif
