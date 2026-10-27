#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Rule Conflicts")]
    public static void RunRuleConflicts()=>Start("rule-conflicts",false);

    [MenuItem("GO GHOST/Playtest/Jump Dodge Priority")]
    public static void RunJumpDodgePriority()=>Start("jump-dodge-priority",false);

    static IEnumerator JumpDodgePriority()
    {
        Record("Legal jumps interrupt manual/rule Dodge; real input edges, physics, events and cooldowns.");
        foreach(bool automaticDodge in new[]{false,true})
        foreach(bool automaticJump in new[]{false,true})
        {
            yield return AlphaFresh(1,d=>{
                if(automaticDodge) V2Rule(d,new ConditionalRule{Condition=6,Action="D03"});
                if(automaticJump) V2Rule(d,new ConditionalRule{Condition=17,Action="J01"});
                V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
            });
            AlphaPlace(-4,10);yield return Seconds(.12f);
            var movement=game.Player.GetComponent<PlayerMovement>();
            var body=game.Player.GetComponent<Rigidbody2D>();
            int started=0,finished=0;
            movement.JumpStarted+=()=>started++;
            game.Dodge.DodgeFinished+=()=>finished++;
            if(automaticDodge) game.Player.TakeDamage(1,null); else heldKeys=new[]{Key.LeftShift};
            yield return Until(()=>game.Dodge.IsDodging,.3f);
            string label="auto Dodge="+automaticDodge+", auto Jump="+automaticJump;
            Check("Dodge really starts: "+label,game.Dodge.IsDodging&&game.Player.IsInvulnerable);
            float cooldownDeadline=Time.time+game.Dodge.CooldownRemaining;
            float remaining=game.Dodge.MovementRemaining;
            heldKeys=automaticJump?Array.Empty<Key>():new[]{Key.Space};
            mouseHeld=automaticJump;
            yield return Until(()=>started>0,.12f);
            Check("Jump interrupts before Dodge deadline: "+label,remaining>0&&started==1
                &&!game.Dodge.IsDodging&&!game.Player.IsInvulnerable&&body.linearVelocity.y>0
                &&game.Dodge.CommittedHorizontalSpeed==0&&game.Dodge.MovementRemaining==0);
            Check("Interruption keeps cooldown and honest attribution: "+label,
                Mathf.Abs(Time.time+game.Dodge.CooldownRemaining-cooldownDeadline)<.001f
                &&game.Rules.AcceptedJumps==(automaticJump?1:0)&&game.Rules.AcceptedDodges==(automaticDodge?1:0)
                &&!game.Rules.Ownership.DodgeOwned&&finished==0);
            heldKeys=Array.Empty<Key>();yield return Seconds(.25f);
            Check("Stopped Dodge never resumes or emits completion: "+label,!game.Dodge.IsDodging
                &&Mathf.Abs(body.linearVelocity.x)<.01f&&finished==0&&started==1);
        }

        foreach(int automatic in new[]{0,1,2})
        {
            yield return AlphaFresh(1,d=>{
                if(automatic==1)V2Rule(d,new ConditionalRule{Condition=17,Action="D03"});
                if(automatic==2)V2Rule(d,new ConditionalRule{Condition=17,Action="J01"});
                V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
            });
            AlphaPlace(-4,10);yield return Seconds(.12f);
            int jumps=0,dodges=0;
            game.Player.GetComponent<PlayerMovement>().JumpStarted+=()=>jumps++;
            game.Dodge.DodgeStarted+=()=>dodges++;
            mouseHeld=automatic!=0;
            heldKeys=automatic==0?new[]{Key.Space,Key.LeftShift}:automatic==1?new[]{Key.Space}:new[]{Key.LeftShift};
            yield return Until(()=>jumps>0,.25f);
            Check("Same-frame Jump wins without spending Dodge, source case="+automatic,
                jumps==1&&dodges==0&&game.Dodge.CooldownRemaining==0&&!game.Dodge.IsDodging);
        }

        // Airborne Jump is unavailable: its press must not cancel a legal air Dodge.
        yield return AlphaFresh(1,null);AlphaPlace(-4,10);yield return Seconds(.12f);
        heldKeys=new[]{Key.Space};yield return Seconds(.08f);heldKeys=Array.Empty<Key>();yield return Frames(3);
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.IsDodging,.3f);
        heldKeys=new[]{Key.Space};yield return Frames(2);
        Check("Invalid airborne Jump leaves Dodge active",game.Dodge.IsDodging
            &&!game.Player.GetComponent<PlayerMovement>().IsGrounded&&game.Player.IsInvulnerable);

        // A pause consumes no action; release gates prevent replaying its Space press on resume.
        yield return AlphaFresh(1,null);AlphaPlace(-4,10);yield return Seconds(.12f);
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.IsDodging,.3f);
        checkingPause=true;game.SetPause(true);
        float pauseRemaining=game.Dodge.MovementRemaining;
        heldKeys=new[]{Key.Space};yield return Frames(4);
        Check("Paused Jump cannot interrupt or advance Dodge",game.Dodge.IsDodging
            &&Mathf.Abs(game.Dodge.MovementRemaining-pauseRemaining)<.001f);
        game.SetPause(false);checkingPause=false;yield return Frames(2);
        Check("Paused Space does not replay on resume",game.Player.GetComponent<Rigidbody2D>().linearVelocity.y<=.1f);
        heldKeys=Array.Empty<Key>();yield return Seconds(.3f);

        // End the Dodge while inside a Boss: retain only overlap-safe separation, then restore collisions.
        yield return AlphaFresh(1,null);AlphaPlace(-4,-3.2f);yield return Seconds(.12f);
        var playerShape=game.Player.GetComponent<Collider2D>();var bossShape=game.Bosses[0].GetComponent<Collider2D>();
        heldKeys=new[]{Key.D,Key.LeftShift};
        yield return Until(()=>game.Dodge.IsDodging&&Physics2D.Distance(playerShape,bossShape).isOverlapped,.14f);
        Check("Dodge physically overlaps Boss before interruption",game.Dodge.IsDodging
            &&Physics2D.Distance(playerShape,bossShape).isOverlapped&&Physics2D.GetIgnoreCollision(playerShape,bossShape));
        heldKeys=new[]{Key.Space};yield return Until(()=>!game.Dodge.IsDodging,.12f);
        Check("Overlap jump ends Dodge with real upward velocity",game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>0
            &&!game.Player.IsInvulnerable);
        heldKeys=Array.Empty<Key>();yield return Until(()=>!Physics2D.GetIgnoreCollision(playerShape,bossShape),.6f);
        Check("Boss collision restored after jump separation",!Physics2D.GetIgnoreCollision(playerShape,bossShape));

        yield return AlphaFresh(1,null,false);int firstFrame=Time.frameCount;yield return Seconds(.7f);
        Check("Normal encounter still advances",game.Phase==DuelPhase.Fight&&Time.frameCount>firstFrame+3);
        game.Player.TakeDamage(10000,null);yield return Frames(4);
        Check("Defeat flow clears interrupted movement",game.Phase==DuelPhase.Failed&&!game.Dodge.IsDodging);
        game.Reconfigure();yield return Frames(3);
        Check("Reconfiguration releases controller",game.Phase==DuelPhase.Configure&&game.Rules==null);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        Check("Retry starts a fresh playable attempt",game.Phase==DuelPhase.Fight&&game.Player!=null&&!game.Player.IsDead);
        game.MainMenu();yield return Frames(3);
    }

    static IEnumerator RuleConflicts()
    {
        Record("Focused physical arbitration: ordinary input/physics, stopped Boss policies and controlled starting positions.");
        foreach(bool reverse in new[]{false,true})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Left});
                V2Rule(d,new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Right});
                V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
                // Serialized order need not match the visible order.
                if(reverse){d.Operations[0].Rules[0].DisplayOrder=1;d.Operations[0].Rules[1].DisplayOrder=0;}
            });
            AlphaPlace(-4,10);yield return Seconds(.12f);mouseHeld=true;
            yield return Until(()=>game.Rules.Ownership.MovementOwned,.3f);
            int wrong=0,samples=0;float end=Time.time+.5f;float sign=reverse?1:-1;
            while(Time.time<end)
            {
                if(game.Rules.Ownership.Move*sign<=0)wrong++;
                samples++;yield return null;
            }
            Check("Opposite Move follows DisplayOrder continuously, reversed="+reverse,wrong==0&&samples>0,
                "wrong="+wrong+" samples="+samples+" velocity="+game.Player.GetComponent<Rigidbody2D>().linearVelocity);
            mouseHeld=false;heldKeys=new[]{reverse?Key.A:Key.D};yield return Seconds(.08f);
            Check("Unmatched Move restores manual input without stale ownership, reversed="+reverse,
                !game.Rules.Ownership.MovementOwned&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x*sign<0);
        }
        foreach(bool jumpFirst in new[]{true,false})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,new ConditionalRule{Condition=17,Action=jumpFirst?"J01":"D03"});
                V2Rule(d,new ConditionalRule{Condition=17,Action=jumpFirst?"D03":"J01"});
                V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
            });
            AlphaPlace(-4,10);yield return Seconds(.12f);mouseHeld=true;
            yield return Until(()=>game.Rules.AcceptedJumps+game.Rules.AcceptedDodges>0,.3f);
            Check("Jump wins regardless of displayed Dodge order, Jump first="+jumpFirst,
                game.Rules.AcceptedJumps==1&&game.Rules.AcceptedDodges==0);
            yield return Seconds(.35f);
            Check("Dodge cannot cancel an accepted automatic Jump",game.Rules.AcceptedDodges==0
                &&!game.Player.GetComponent<PlayerMovement>().IsGrounded);
        }
        foreach(bool moveFirst in new[]{true,false})
        {
            yield return AlphaFresh(1,d=>{
                var move=new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Left};
                var dodge=new ConditionalRule{Condition=17,Action="D04"};
                V2Rule(d,moveFirst?move:dodge);V2Rule(d,moveFirst?dodge:move);
                V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
            });
            AlphaPlace(-4,10);TurningCursor(1,false);yield return Seconds(.12f);mouseHeld=true;
            yield return Until(()=>game.Rules.Ownership.MovementOwned||game.Dodge.IsDodging,.3f);
            yield return Seconds(.05f);
            Check("Move/Dodge share horizontal authority, Move first="+moveFirst,
                moveFirst?game.Rules.Ownership.Move<0&&game.Rules.AcceptedDodges==0
                    :game.Dodge.IsDodging&&!game.Rules.Ownership.MovementOwned&&game.Dodge.CommittedHorizontalSpeed>0);
            if(moveFirst)
            {
                yield return Seconds(.3f);
                Check("Lower Dodge never takes over an executable continuous Move",game.Rules.AcceptedDodges==0&&game.Rules.Ownership.Move<0);
            }
            else
            {
                yield return Until(()=>!game.Dodge.IsDodging,.3f);yield return Seconds(.06f);
                Check("Consumed/cooling Dodge falls through to lower Move",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move<0);
            }
        }

        // Opposing finite moves each finish once, without restarting on the same held state.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="M04",ShortMoveDirection=ShortMoveDirectionOption.Forward});
            V2Rule(d,new ConditionalRule{Condition=17,Action="M04",ShortMoveDirection=ShortMoveDirectionOption.Back});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-4,10);yield return Seconds(.12f);
        float startX=game.Player.GetComponent<Rigidbody2D>().position.x;
        mouseHeld=true;var moves=new HashSet<long>();float peak=startX,endMoves=Time.time+.85f;
        while(Time.time<endMoves)
        {
            peak=Mathf.Max(peak,game.Player.GetComponent<Rigidbody2D>().position.x);
            BehaviorNotices(game.Rules,61,moves);yield return null;
        }
        Check("Forward completes before Back; neither restarts while trigger stays held",moves.Count==2
            &&Mathf.Abs(peak-startX-1.5f)<.09f&&Mathf.Abs(game.Player.GetComponent<Rigidbody2D>().position.x-startX)<.09f
            &&!game.Rules.Ownership.MovementOwned,"completions="+moves.Count+" peak="+(peak-startX));

        // An unavailable high Jump permits Dodge; this is a manual jump, with no rule commitment.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="J01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="D03"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-4,10);yield return Seconds(.12f);
        heldKeys=new[]{Key.Space};yield return Seconds(.08f);heldKeys=Array.Empty<Key>();mouseHeld=true;
        yield return Until(()=>game.Dodge.IsDodging,.25f);
        Check("Unavailable airborne Jump falls through to lower Dodge",game.Dodge.IsDodging&&game.Rules.AcceptedJumps==0&&game.Rules.AcceptedDodges==1);

        // A lower physical commitment finishes even when a higher opposite Move becomes true.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Left});
            V2Rule(d,new ConditionalRule{Condition=6,Action="D04"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-4,10);TurningCursor(1,false);yield return Seconds(.12f);
        game.Player.TakeDamage(1,null);yield return Until(()=>game.Dodge.IsDodging,.25f);mouseHeld=true;
        yield return Seconds(.05f);
        Check("Accepted Dodge keeps its direction when a higher Move becomes true",game.Dodge.IsDodging
            &&game.Dodge.CommittedHorizontalSpeed>0&&!game.Rules.Ownership.MovementOwned);
        yield return Until(()=>!game.Dodge.IsDodging,.3f);yield return Seconds(.06f);
        Check("Higher Move resumes after real Dodge completion",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move<0);

        // Independent Move/Aim/Fire and real Jump -> Apex -> Landed events remain live.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Right});
            V2Rule(d,new ConditionalRule{Condition=17,Action="J01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="Q01"});
            V2Rule(d,new ConditionalRule{Condition=58,Action="F01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-6,10);yield return Seconds(.12f);mouseHeld=true;
        yield return Until(()=>game.Rules.AcceptedJumps>0,.3f);yield return Seconds(.05f);
        Check("Move and Aim run alongside committed Jump",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.AimOwned
            &&game.Rules.Ownership.JumpOwned&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x>0);
        yield return Until(()=>game.Rifle.ShotsFired>0,1.2f);
        Check("Real Apex drives one automatic shot while Move and Aim continue",game.Rifle.ShotsFired==1&&game.Rules.AutomaticShots==1
            &&game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.AimOwned);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=3,Action="J01"});
            V2Rule(d,new ConditionalRule{Condition=57,Action="F01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-4,10);yield return Seconds(.12f);
        heldKeys=new[]{Key.Space};yield return Seconds(.06f);heldKeys=Array.Empty<Key>();
        yield return Until(()=>game.Rules.AcceptedJumps>=2,2.5f);
        Check("Landed -> Jump -> Shot chain keeps independent occurrences",game.Rules.AcceptedJumps>=2&&game.Rules.AutomaticShots>=2);

        // Burst survives condition loss and a newly matching higher Hold Fire.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=12,Action="F06"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F02"});
            V2Rule(d,new ConditionalRule{Condition=60,Action="M04",ShortMoveDirection=ShortMoveDirectionOption.Back});
            V2Rule(d,new ConditionalRule{Condition=61,Action="F06"});
        });
        AlphaPlace(-4,10);yield return Seconds(.12f);mouseHeld=true;
        yield return Until(()=>game.Rifle.IsBursting,.3f);mouseHeld=false;
        var bursts=new HashSet<long>();moves.Clear();float chainEnd=Time.time+1.1f;
        while(Time.time<chainEnd)
        {BehaviorNotices(game.Rules,60,bursts);BehaviorNotices(game.Rules,61,moves);yield return null;}
        Check("Committed Burst -> Move Finished chain uses real completion events",game.Rifle.ShotsFired==6
            &&game.Rules.AutomaticShots==6&&bursts.Count==1&&moves.Count==1,
            "shots="+game.Rifle.ShotsFired+" bursts="+bursts.Count+" moves="+moves.Count);

        // A platform route also retains both physical claims ahead of newly matching rows.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="M11",MoveDirection=MoveDirectionOption.Left});
            V2Rule(d,new ConditionalRule{Condition=17,Action="D04"});
            V2Rule(d,PlatformRule(true,6));
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-5,10);TurningCursor(1,false);yield return Seconds(.12f);
        game.Player.TakeDamage(1,null);yield return Until(()=>game.Rules.AcceptedJumps>0,.7f);
        var destination=game.Rules.PlatformDestination;mouseHeld=true;
        yield return Until(()=>game.Rules.PlatformTransitionsCompleted==1,1.5f);
        Check("Accepted platform route lands before higher Move/Dodge can take over",destination!=null
            &&game.Rules.PlatformTransitionsCompleted==1&&game.Rules.AcceptedDodges==0);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=17,Action="D03"});
            V2Rule(d,new ConditionalRule{Condition=6,Action="J01"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-4,10);yield return Seconds(.12f);game.Player.TakeDamage(1,null);
        yield return Until(()=>game.Rules.AcceptedJumps==1,.3f);mouseHeld=true;yield return Seconds(.35f);
        Check("Accepted Jump finishes even when a higher Dodge becomes true",game.Rules.AcceptedDodges==0
            &&game.Rules.Ownership.JumpOwned&&!game.Player.GetComponent<PlayerMovement>().IsGrounded);
        yield return Until(()=>game.Rules.AcceptedDodges==1,1.2f);
        Check("Higher Dodge can start after the actual landing",game.Rules.AcceptedDodges==1);

        // Normal live encounter and defeat/retry lifecycle after the controlled fixtures.
        yield return AlphaFresh(1,null,false);yield return Seconds(.7f);
        Check("Normal Fight remains active",game.Phase==DuelPhase.Fight);
        game.Player.TakeDamage(10000,null);yield return Frames(4);
        Check("Defeat returns to editable configuration",game.Phase==DuelPhase.Failed);
        game.Reconfigure();yield return Frames(3);
        Check("Retry configuration has no stale controller",game.Phase==DuelPhase.Configure&&game.Rules==null);
        mouseHeld=false;heldKeys=Array.Empty<Key>();game.MainMenu();yield return Frames(3);
    }
}
#endif
