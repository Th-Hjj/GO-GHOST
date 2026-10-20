#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Player Turning")]
    public static void RunPlayerTurning()=>Start("player-turning",false);
    static void TurningCursor(int side,bool held)
    {
        // Fixed Game View positions avoid sampling a temporary Editor render camera matrix.
        // These sides remain within the arena in both manual and rule-HUD layouts.
        mousePosition=new Vector2(Screen.width*(side<0?.18f:.65f),Screen.height*.25f);
        mouseHeld=held;
    }
    static IEnumerator PlayerTurning()
    {
        yield return AlphaFresh(1,null);
        AlphaPlace(0,10);yield return Seconds(.15f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        var movement=game.Player.GetComponent<PlayerMovement>();
        foreach(int side in new[]{1,-1,1})
        {
            TurningCursor(side,false);
            yield return Seconds(.15f);
            Check("Mouse turns rifle without turning idle body "+side,reader.AimDirection.x*side>.2f&&movement.FacingDirection==1,
                "screen="+reader.AimScreenPosition+" raw="+reader.RawInput.Aim+" aim="+reader.AimDirection+" facing="+movement.FacingDirection);
        }
        heldKeys=new[]{Key.A};yield return Seconds(.15f);
        Check("A turns body left while aiming right",game.Player.GetComponent<Rigidbody2D>().linearVelocity.x< -1
            &&movement.FacingDirection==-1&&reader.AimDirection.x>.2f&&Private<Transform>(game.Rifle,"firePoint").right.x>.2f);
        yield return CardsCapture("turn-body-left-aim-right");
        TurningCursor(-1,false);
        heldKeys=new[]{Key.D};yield return Seconds(.15f);
        Check("D turns body right while aiming left",game.Player.GetComponent<Rigidbody2D>().linearVelocity.x>1
            &&movement.FacingDirection==1&&reader.AimDirection.x<-.2f&&Private<Transform>(game.Rifle,"firePoint").right.x<-.2f);
        yield return CardsCapture("turn-body-right-aim-left");
        heldKeys=Array.Empty<Key>();yield return Seconds(.1f);
        Check("Idle retains body facing",movement.FacingDirection==1&&reader.AimDirection.x<-.2f);
        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="Q01"}));
        AlphaPlace(0,10);TurningCursor(-1,false);yield return Seconds(.15f);
        reader=game.Player.GetComponent<PlayerInputReader>();movement=game.Player.GetComponent<PlayerMovement>();
        heldKeys=new[]{Key.A};mouseHeld=true;yield return Seconds(.15f);
        Check("Automatic Aim remains independent of A body turn",reader.ConditionalFrame.AimOwned
            &&reader.AimDirection.x>.2f&&movement.FacingDirection==-1);
        heldKeys=Array.Empty<Key>();mouseHeld=false;yield return Seconds(.15f);
        Check("Released Aim rule restores mouse aim and keeps body facing",
            !reader.ConditionalFrame.AimOwned&&reader.AimDirection.x<-.2f&&movement.FacingDirection==-1,
            "owned="+reader.ConditionalFrame.AimOwned+" held="+reader.RawInput.Fire+" raw="+reader.RawInput.Aim+" aim="+reader.AimDirection+" facing="+movement.FacingDirection);
        foreach(var direction in new[]{ShortMoveDirectionOption.Forward,ShortMoveDirectionOption.Back})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,new ConditionalRule{Condition=17,Action="M04",ShortMoveDirection=direction});
                V2Rule(d,V2Ground("F06"));
            });
            AlphaPlace(0,10);TurningCursor(1,false);
            heldKeys=new[]{Key.A};yield return Seconds(.12f);
            heldKeys=Array.Empty<Key>();yield return Seconds(.1f);
            movement=game.Player.GetComponent<PlayerMovement>();
            float begin=game.Player.transform.position.x;var finished=new HashSet<long>();
            mouseHeld=true;yield return BehaviorSample(61,finished,.55f);
            float actual=game.Player.transform.position.x-begin;
            Check("Move "+direction+" uses and retains left body facing",
                movement.FacingDirection==-1&&Mathf.Abs(actual-(direction==ShortMoveDirectionOption.Forward?-1.5f:1.5f))<.09f&&finished.Count==1,
                "distance="+actual+" facing="+movement.FacingDirection+" completions="+finished.Count);
        }
        checkingPause=true;game.SetPause(true);heldKeys=new[]{Key.D};yield return RealSeconds(.15);
        Check("Pause freezes body facing",game.Player.GetComponent<PlayerMovement>().FacingDirection==-1);
        game.SetPause(false);checkingPause=false;mouseHeld=false;yield return Seconds(.15f);
        Check("Resume permits body turning",game.Player.GetComponent<PlayerMovement>().FacingDirection==1);
        heldKeys=Array.Empty<Key>();
        game.MainMenu();yield return Frames(3);
    }
    [MenuItem("GO GHOST/Playtest/Condition Action Behavior Corrections")]
    public static void RunBehaviorCorrections()=>Start("behavior-corrections",false);
    [MenuItem("GO GHOST/Playtest/Behavior Public Facts")]
    public static void RunBehaviorPublicFacts()=>Start("behavior-public-facts",false);
    [MenuItem("GO GHOST/Playtest/Behavior Platforms")]
    public static void RunBehaviorPlatforms()=>Start("behavior-platforms",false);
    [MenuItem("GO GHOST/Playtest/Behavior Catalog")]
    public static void RunBehaviorCatalog()=>Start("behavior-catalog",false);
    static void BehaviorNotices(ConditionalController controller,int condition,HashSet<long> found)
    {
        foreach(object notice in Private<IList>(controller,"events"))
        {
            var type=notice.GetType();
            if((int)type.GetField("Condition").GetValue(notice)==condition)
                found.Add(((ConditionalController.TriggerContext)type.GetField("Context").GetValue(notice)).Occurrence);
        }
    }
    static IEnumerator BehaviorSample(int condition,HashSet<long> found,float seconds)
    {
        float end=Time.time+seconds;
        do {BehaviorNotices(game.Rules,condition,found);yield return null;}while(Time.time<end);
        BehaviorNotices(game.Rules,condition,found);
    }
    static float BehaviorDeadline(int op)
    {
        object r=Private<IList>(game.Rules,"running")[op];
        return (float)r.GetType().GetField("Deadline").GetValue(r);
    }
    static IEnumerator BehaviorCorrections()
    {
        Record("Focused behavior correction mechanisms: real executors, virtual input, controlled placements/health. No outcome or balance claim.");
        foreach(var direction in new[]{ShortMoveDirectionOption.Forward,ShortMoveDirectionOption.Back})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,new ConditionalRule{Condition=17,Action="M04",ShortMoveDirection=direction});
                V2Rule(d,V2Ground("F06"));
            });
            AlphaPlace(-5,10);MouseAt(game.Rifle.AimOrigin+Vector2.right*12,false);yield return Seconds(.12f);
            float begin=game.Player.transform.position.x;var finished=new HashSet<long>();
            mouseHeld=true;yield return Until(()=>game.Rules.Ownership.MovementOwned,.3f);
            MouseAt(game.Rifle.AimOrigin+Vector2.left*12,true);
            yield return BehaviorSample(61,finished,.6f);
            float actual=game.Player.transform.position.x-begin;
            Check("Move "+direction+" freezes visual facing and completes exactly once",
                Mathf.Abs(actual-(direction==ShortMoveDirectionOption.Forward?1.5f:-1.5f))<.09f&&finished.Count==1,
                "distance="+actual+" events="+finished.Count);
            yield return Seconds(.2f);
            Check("A held state does not repeat a completed Move",Mathf.Abs(game.Player.transform.position.x-begin-actual)<.03f&&!game.Rules.Ownership.MovementOwned);
        }
        yield return AlphaFresh(1,d=>{V2Rule(d,new ConditionalRule{Condition=17,Action="M04"});V2Rule(d,V2Ground("F06"));});
        AlphaPlace(-5,10);MouseAt(game.Rifle.AimOrigin+Vector2.right*10,false);yield return Seconds(.1f);
        mouseHeld=true;yield return Until(()=>game.Rules.Ownership.MovementOwned,.3f);yield return Frames(2);mouseHeld=false;
        var cancelled=new HashSet<long>();yield return BehaviorSample(61,cancelled,.45f);
        Check("State cancellation stops Move without completion",cancelled.Count==0&&!game.Rules.Ownership.MovementOwned);

        // Slow only this fixture's instance so its ordinary 1.5-unit move exceeds the event window.
        foreach(bool interrupt in new[]{true,false})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,new ConditionalRule{Condition=6,AdditionalCondition=17,Action="M04"});
                V2Rule(d,V2Ground("F06"));
            });
            AlphaPlace(-5,10);game.Player.GetComponent<PlayerMovement>().SetRuntimeMovement(1,12);
            MouseAt(game.Rifle.AimOrigin+Vector2.right*12,true);yield return Seconds(.1f);
            game.Player.TakeDamage(1,null);var done=new HashSet<long>();
            yield return BehaviorSample(61,done,.65f);
            Check("Accepted event Move survives event expiry while additional state holds",game.Rules.Ownership.MovementOwned);
            if(interrupt)mouseHeld=false;
            yield return BehaviorSample(61,done,1.3f);
            Check(interrupt?"Event Move still stops when additional state ends":"Event Move completes its distance beyond .5 seconds",
                done.Count==(interrupt?0:1)&&!game.Rules.Ownership.MovementOwned,"events="+done.Count);
        }
        // A real damage event waits for a later optional state, then gets a fresh .5-second response.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=6,AdditionalCondition=17,Action="M10"});
            V2Rule(d,new ConditionalRule{Condition=6,AdditionalCondition=17,Action="F06"});
        });
        AlphaPlace(-5,10);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,false);yield return Seconds(.1f);
        float eventAt=Time.time;game.Player.TakeDamage(1,null);yield return Seconds(.28f);mouseHeld=true;
        yield return Until(()=>game.Rules.Ownership.MovementOwned&&float.IsFinite(BehaviorDeadline(0)),.2f);float accepted=Time.time;
        float movementDeadline=BehaviorDeadline(0),fireDeadline=BehaviorDeadline(3);
        Check("Hold zero outputs accept independently and time from acceptance",
            game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move==0&&game.Rules.Ownership.FireOwned&&!game.Rules.Ownership.Fire
            &&Mathf.Abs(movementDeadline-accepted-.5f)<.06f&&Mathf.Abs(fireDeadline-movementDeadline)<.04f);
        yield return Until(()=>Time.time>eventAt+.54f,.4f);
        var expired=new HashSet<long>();BehaviorNotices(game.Rules,6,expired);
        Check("Damage occurrence expires at .5 while accepted response continues",expired.Count==0&&game.Rules.Ownership.MovementOwned);
        yield return Until(()=>Time.time>movementDeadline+.04f,.6f);
        Check("Response ends at its own .5 deadline",!game.Rules.Ownership.MovementOwned&&!game.Rules.Ownership.FireOwned);
        mouseHeld=false;

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=6,Action="M10"}));
        game.Player.TakeDamage(1,null);yield return Until(()=>game.Rules.Ownership.MovementOwned&&float.IsFinite(BehaviorDeadline(0)),.2f);float originalDeadline=BehaviorDeadline(0);
        checkingPause=true;game.SetPause(true);float pauseTime=Time.time;yield return RealSeconds(.15f);
        Check("Pause freezes occurrence and response clocks",Time.time==pauseTime&&BehaviorDeadline(0)==originalDeadline);
        game.SetPause(false);checkingPause=false;
        yield return Seconds(.15f);game.Player.TakeDamage(1,null);yield return Frames(3);
        Check("A later real event never refreshes the active response deadline",BehaviorDeadline(0)==originalDeadline);
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=6,AdditionalCondition=17,Action="M10"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        game.Player.TakeDamage(1,null);yield return Seconds(.56f);mouseHeld=true;yield return Frames(4);
        Check("An expired event cannot start when its additional state arrives late",!game.Rules.Ownership.MovementOwned);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=4,Action="F06"});
            V2Rule(d,new ConditionalRule{Condition=17,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Grounded,Action="F02"});
        });
        AlphaPlace(-8,10);MouseAt(game.Rifle.AimOrigin+Vector2.up*12,false);yield return Frames(3);
        using(var burst=new RuleCardsBurstProbe(game.Rules,game.Rifle))
        {
            mouseHeld=true;yield return Until(()=>game.Rifle.ShotsFired==1,.4f);burst.Sample();mouseHeld=false;
            PlatformTrigger();heldKeys=new[]{Key.Space};yield return Frames(3);heldKeys=Array.Empty<Key>();
            yield return RuleCardsSample(burst,2.5f,()=>game.Rifle.IsReloading);
            Check("Burst survives primary/additional loss and higher Hold Fire",game.Rifle.ShotsFired==8&&burst.AcceptedBudget==8
                &&burst.Bursts.Count==1&&burst.BurstPresentAtReload&&burst.Reloads==1,
                "shots="+game.Rifle.ShotsFired+" burst="+burst.Bursts.Count+" budget="+burst.AcceptedBudget);
            Check("Burst keeps normal paid-shot cadence",burst.ShotTimes.Zip(burst.ShotTimes.Skip(1),(a,b)=>b-a).All(t=>t>=game.Rifle.FireInterval-.01f));
            yield return Seconds(1.4f);
            Check("Burst never crosses reload; higher Hold Fire then owns Fire",game.Rifle.ShotsFired==8&&game.Rifle.AmmoInMagazine==8
                &&game.Rules.Ownership.FireOwned&&!game.Rules.Ownership.Fire);
        }
        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=59,Action="F01",AmmoUse=AmmoUseOption.KeepTwoRounds}));
        MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);yield return Until(()=>game.Rifle.ShotsFired>0,.4f);mouseHeld=false;
        yield return Seconds(1.6f);
        Check("Real Shot Fired -> Fire 1 Shot loop retains independent occurrences and cadence",game.Rifle.ShotsFired==6&&game.Rules.AutomaticShots==5&&game.Rifle.AmmoInMagazine==2);

        yield return AlphaFresh(3,d=>V2Rule(d,V2Ground("Q06")));
        AlphaPlace(-10,2);
        var far=game.Bosses[0];var near=game.Bosses[1];
        var nearBody=near.GetComponent<Rigidbody2D>();nearBody.constraints=RigidbodyConstraints2D.FreezeAll;
        Position(near.gameObject,game.Rifle.AimOrigin+new Vector2(4,.8f)-(Vector2)(near.Body.bounds.center-near.transform.position));
        yield return Seconds(.12f);MouseAt(far.Body.bounds.center,false);yield return Seconds(.15f);
        var input=game.Player.GetComponent<PlayerInputReader>();
        Check("Fine-Tune prefers raw aim intent over a nearer off-axis enemy",game.Rules.CurrentTargetId==far.ActorId,
            "target="+game.Rules.CurrentTargetId+" far="+far.ActorId+" visible="+Private<int>(game.Rules,"allVisible")
            +" raw="+input.RawInput.Aim+" aim="+input.AimDirection+" body="+far.Body.bounds.center);
        Check("Fine-Tune correction stays inside its original small cone",Vector2.Angle(input.RawInput.Aim,input.AimDirection)<=ConditionalDefaults.MouseEnter+.01f);
        MouseAt(near.Body.bounds.center,false);yield return Seconds(.15f);
        Check("Fine-Tune releases old target when player aims at the other enemy",game.Rules.CurrentTargetId==near.ActorId);
        MouseAt(game.Rifle.AimOrigin+Vector2.up*10,false);yield return Seconds(.12f);
        Check("Fine-Tune releases outside the raw intent cone",!game.Rules.Ownership.AimOwned);mouseHeld=false;
        // Evaluate a complete real controller against live body geometry without submitting its
        // output: all operations must use the same already-adopted muzzle snapshot.
        var alignment=new ConditionalLoadout();
        V2Rule(alignment,new ConditionalRule{Condition=40,Action="M10"});
        V2Rule(alignment,new ConditionalRule{Condition=40,Action="F06"});
        V2Rule(alignment,V2Ground("Q01"));
        using(var alignmentController=new ConditionalController(game.Player,game.Bosses,LayoutSupports(),game.ArenaBounds,alignment))
        {
            var adopted=new RawPlayerInput{Aim=Vector2.up,CommittedAim=Vector2.up,Cursor=game.Rifle.AimOrigin+Vector2.up*10};
            var next=alignmentController.Resolve(adopted);
            Check("Pending automatic Aim cannot make its same-frame Firing Line true",next.AimOwned&&!next.MovementOwned&&!next.FireOwned);
            adopted.CommittedAim=((Vector2)near.Body.bounds.center-game.Rifle.AimOrigin).normalized;
            next=alignmentController.Resolve(adopted);
            Check("All operations use the same adopted muzzle/body intersection",next.MovementOwned&&next.FireOwned);
        }

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="M01"}));
        AlphaPlace(-5,10);
        var high=LayoutSupports().Skip(1).OrderBy(c=>c.bounds.max.y).First();
        PlatformPlace(high,high.bounds.center.x);
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,high.bounds.center.x);
        yield return Seconds(.12f);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);
        float alignedX=game.Player.transform.position.x;yield return Seconds(.6f);
        Check("Approach holds zero without horizontal jitter across different heights",Mathf.Abs(game.Player.transform.position.x-alignedX)<.05f
            &&game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move==0);
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,alignedX+1);
        yield return Seconds(.1f);Check("Approach resumes after target moves beyond the exit tolerance",game.Rules.Ownership.Move>0);
        mouseHeld=false;

        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>{
                V2Rule(d,new ConditionalRule{Condition=17,Action="Q06"});
                V2Rule(d,new ConditionalRule{Condition=57,Action="F02"});
            },false);
            heldKeys=new[]{Key.D,Key.Space};MouseAt(game.Bosses[0].Position,true);yield return Frames(3);heldKeys=new[]{Key.D};
            int startFrame=Time.frameCount;float start=Time.time;yield return Seconds(2);
            Check("Short W"+wave+" normal policy/physics flow advances",Time.frameCount>startFrame+5&&Time.time>=start+2
                &&game.Phase==DuelPhase.Fight&&game.Bosses.Count==(wave==3?2:1));
            yield return CardsCapture("behavior-wave"+wave);
        }
        heldKeys=Array.Empty<Key>();mouseHeld=false;game.MainMenu();yield return Frames(4);
        Check("Behavior suite menu cleanup releases actors/controller",game.Player==null&&game.Rules==null);
    }
}
#endif
