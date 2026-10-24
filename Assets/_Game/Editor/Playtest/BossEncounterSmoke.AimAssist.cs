#if UNITY_EDITOR
using System.Collections;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Aim Assist and Auto Aim")]
    public static void RunAimAssist() => Start("aim-assist", false);

    static Vector2 AimRotate(Vector2 direction,float degrees)
        =>Quaternion.Euler(0,0,degrees)*direction;

    static IEnumerator AimAssist()
    {
        Check("shared catalog and rule expressions use the three new names",
            ConditionalCatalog.Response("Q01").Label=="Auto Aim at Nearest Enemy"
            &&ConditionalCatalog.Response("Q02").Label=="Auto Aim at Lowest-Health Enemy"
            &&ConditionalCatalog.RuleActionLabel(new ConditionalRule{Action="Q06"})=="Aim Assist");
        yield return AlphaFresh(3,null);
        AlphaPlace(-10,2);
        var far=game.Bosses[0];var near=game.Bosses[1];
        var farBody=far.GetComponent<Rigidbody2D>();var nearBody=near.GetComponent<Rigidbody2D>();
        farBody.interpolation=nearBody.interpolation=RigidbodyInterpolation2D.None;
        farBody.constraints=nearBody.constraints=RigidbodyConstraints2D.FreezeAll;
        Position(near.gameObject,game.Rifle.AimOrigin+new Vector2(4,1.2f)-(Vector2)(near.Body.bounds.center-near.transform.position));
        far.Health.TakeDamage(far.Health.MaxHealth/2,game.Player.gameObject);
        yield return Seconds(.12f);
        // Resolve real controllers against current public body contacts, independent of mouse pixels.
        foreach(string action in new[]{"Q01","Q02"})
        {
            var loadout=new ConditionalLoadout();V2Rule(loadout,new ConditionalRule{Condition=17,Action=action});
            using(var controller=new ConditionalController(game.Player,game.Bosses,LayoutSupports(),game.ArenaBounds,loadout))
            {
                var raw=new RawPlayerInput{Aim=Vector2.left,CommittedAim=Vector2.left,Fire=true};
                var target=action=="Q01"?near:far;
                var result=controller.Resolve(raw);
                Vector2 direct=((Vector2)target.Body.bounds.center-game.Rifle.AimOrigin).normalized;
                Check(action+" selects its strategy and snaps from opposite mouse direction immediately",
                    result.AimOwned&&controller.CurrentTargetId==target.ActorId&&Vector2.Angle(result.Aim,direct)<.05f);
                var targetBody=target.GetComponent<Rigidbody2D>();
                targetBody.constraints=RigidbodyConstraints2D.FreezeRotation;
                targetBody.linearVelocity=new Vector2(0,8);
                target.TryGetPlayerRuleContact(out var contact);
                result=controller.Resolve(raw);
                Check(action+" moving/airborne contact aims at current body without lead",
                    contact.VelocityKnown&&contact.Velocity.y>7&&Vector2.Angle(result.Aim,direct)<.05f,
                    "velocity="+contact.Velocity+" error="+Vector2.Angle(result.Aim,direct));
                targetBody.linearVelocity=Vector2.zero;targetBody.constraints=RigidbodyConstraints2D.FreezeAll;
                target.Body.enabled=false;
                result=controller.Resolve(raw);
                Check(action+" immediately switches to remaining visible target",
                    result.AimOwned&&controller.CurrentTargetId!=(target.ActorId));
                target.Body.enabled=true;
            }
        }
        var assistLoadout=new ConditionalLoadout();V2Rule(assistLoadout,new ConditionalRule{Condition=17,Action="Q06"});
        var assist=new ConditionalController(game.Player,game.Bosses,LayoutSupports(),game.ArenaBounds,assistLoadout);
        using(assist)
        {
            Vector2 direct=((Vector2)far.Body.bounds.center-game.Rifle.AimOrigin).normalized;
            var raw=new RawPlayerInput{Aim=AimRotate(direct,-8),Fire=true};
            var result=assist.Resolve(raw);
            float firstError=Vector2.Angle(result.Aim,direct);
            Check("Aim Assist acquires by raw angle and starts softly, not a snap",
                result.AimOwned&&assist.CurrentTargetId==far.ActorId&&firstError>.2f&&firstError<8,
                "error="+firstError+" dt="+Time.deltaTime);
            float started=Time.time;float lastError=firstError;bool smooth=true;
            while(Time.time-started<.09f)
            {
                yield return null;result=assist.Resolve(raw);
                float error=Vector2.Angle(result.Aim,direct);smooth&=error<=lastError+.02f;lastError=error;
            }
            Check("stationary assistance reaches seventy-percent correction within .09 combat seconds without locking",
                smooth&&lastError>=2.39f&&lastError<2.8f,"error="+lastError);
            raw.Aim=AimRotate(direct,-9);var moved=assist.Resolve(raw);
            Check("mouse movement updates immediately while additive correction stays bounded",
                Vector2.SignedAngle(result.Aim,moved.Aim)<-.3f&&Vector2.Angle(raw.Aim,moved.Aim)<=6.31f);
            raw.Aim=AimRotate(direct,2);result=assist.Resolve(raw);
            Check("crossing target never leaves assistance pulling away from it",
                Vector2.Angle(result.Aim,direct)<=2.01f&&Vector2.SignedAngle(raw.Aim,result.Aim)<=.01f);
            raw.Aim=AimRotate(direct,-21);result=assist.Resolve(raw);
            Check("leaving twenty-degree cone immediately restores raw aim",!result.AimOwned&&Vector2.Angle(result.Aim,raw.Aim)<.01f);
            raw.Aim=AimRotate(direct,-8);result=assist.Resolve(raw);
            Check("reacquisition starts with fresh soft correction",result.AimOwned&&Vector2.Angle(result.Aim,direct)>.2f);
            checkingPause=true;game.SetPause(true);yield return Frames(2);
            result=assist.Resolve(raw);Vector2 paused=result.Aim;
            yield return RealSeconds(.12);result=assist.Resolve(raw);
            Check("pause freezes assistance convergence",Time.deltaTime==0&&Vector2.Angle(paused,result.Aim)<.01f);
            game.SetPause(false);checkingPause=false;
            raw.Fire=false;result=assist.Resolve(raw);
            Check("condition loss clears Aim ownership",!result.AimOwned);
            far.Body.enabled=false;near.Body.enabled=false;raw.Fire=true;result=assist.Resolve(raw);
            Check("unobservable targets release assistance",!result.AimOwned);
            far.Body.enabled=near.Body.enabled=true;
        }
        Check("disposed controller has no residual aim",!assist.Resolve(new RawPlayerInput{Fire=true,Aim=Vector2.right}).AimOwned);

        // Real input, physical gun and HUD path, then the normal Training -> W1 continuation.
        yield return TrainingFresh(d=>{
            V2Rule(d,new ConditionalRule{Condition=18,Action="Q06"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F03"});
        });
        var trainer=game.Bosses[0];trainer.StopCombat();AlphaPlace(-4,5);yield return Seconds(.12f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Vector2 toward=((Vector2)trainer.Body.bounds.center-game.Rifle.AimOrigin).normalized;
        MouseAt(game.Rifle.AimOrigin+AimRotate(toward,-6)*10,false);yield return Seconds(.25f);
        Check("live Training input adopts soft assistance",reader.ConditionalFrame.AimOwned
            &&Vector2.Angle(reader.AimDirection,toward)>1.5f&&Vector2.Angle(reader.AimDirection,toward)<2.2f
            &&Vector2.Angle(reader.RawInput.Aim,toward)>3);
        int health=trainer.Health.CurrentHealth;mouseHeld=true;yield return Seconds(.65f);mouseHeld=false;
        Check("assisted real shots spend ammo and hit current body",game.Rifle.ShotsFired>0&&trainer.Health.CurrentHealth<health);
        game.ContinueFromTraining();game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        int startFrame=Time.frameCount;yield return Seconds(.6f);
        Check("normal Continue enters an advancing W1 with retained Aim Assist",game.Phase==DuelPhase.Fight
            &&game.Wave==1&&Time.frameCount>startFrame+3&&game.Rules.RuleAt(ControlOperation.Aim,0).Action=="Q06");
        var oldController=game.Rules;game.MainMenu();yield return Frames(2);
        Check("menu cleanup clears old assistance",!oldController.Resolve(new RawPlayerInput{Fire=true,Aim=Vector2.right}).AimOwned);
        Record("NOT TESTED: physical mouse feel, full-fight balance, standalone/build. Controlled Editor fixtures only.");
    }
}
#endif
