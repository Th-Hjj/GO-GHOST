#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    static IEnumerator SwordIntelligence()
    {
        Record("CONTROLLED initial placements, ordinary player movement/dodge and real boss/projectile clocks. Mechanisms and short flow only; no balance/win-rate proof.");
        yield return Fresh(2); StopPolicies();
        var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-11);
        MouseAt(sword.Position,false);yield return Seconds(.3f);
        sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy,.5f);sword.Tactics.Enabled=false;
        Check("close opening chooses Rhythm before flank/dash",sword.Actions.Ability==BossAbility.Rhythm);
        int hp=game.Player.CurrentHealth;
        yield return Until(()=>game.Player.CurrentHealth<hp,1);
        Check("chosen close attack makes real contact",game.Player.CurrentHealth<hp);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-11.5f);
        yield return Seconds(.25f);heldKeys=new[]{Key.D};sword.Motor.SetMove(1);
        yield return Until(()=>sword.Perception.Facts.Velocity.x>6,.5f);
        Check("retreat is observed through delayed public velocity",sword.Perception.Facts.Velocity.x>6);
        sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy,.5f);sword.Tactics.Enabled=false;
        Check("retreat selects reachable intercept dash",sword.Actions.Ability==BossAbility.DrawDash,
            "ability="+sword.Actions.Ability+" sword="+sword.Position+" player="+game.Player.transform.position);
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,.7f);
        Vector2 lockedEnd=sword.Actions.End,lockedAim=sword.Actions.Aim;
        float lockedAt=Time.time;
        Vector2 shortPrediction=sword.Tactics.PredictBody(sword.Perception.Facts,.16f);
        Check("dash lock leads beyond old short prediction",sword.Actions.Ability==BossAbility.DrawDash
            &&sword.Actions.Phase==BossActionPhase.Locked&&lockedEnd.x>shortPrediction.x+.6f,
            "end="+lockedEnd+" short="+shortPrediction);
        hp=game.Player.CurrentHealth;
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Active,.5f);
        Check("full dash lock remains",sword.Actions.Ability==BossAbility.DrawDash&&sword.Actions.Phase==BossActionPhase.Active
            &&Time.time-lockedAt>=sword.Profile.DrawDash.Lock-.04f);
        Check("committed dash never homes",sword.Actions.Ability==BossAbility.DrawDash
            &&sword.Actions.Phase==BossActionPhase.Active&&sword.Actions.End==lockedEnd&&sword.Actions.Aim==lockedAim);
        yield return Until(()=>game.Player.CurrentHealth<hp||!sword.Actions.Busy,1.5f);
        Check("real moving retreat is intercepted",game.Player.CurrentHealth<hp,
            "sword="+sword.Position+" player="+game.Player.transform.position+" hits="+sword.Actions.ConnectedHits);
        heldKeys=Array.Empty<Key>();
        yield return Until(()=>!sword.Actions.Busy,2);
        Check("dash finishes without stuck action",!sword.Actions.Busy);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-11);
        yield return Seconds(.25f);heldKeys=new[]{Key.D};
        yield return Until(()=>sword.Perception.Facts.Velocity.x>6,.5f);
        float beforeGap=Vector2.Distance(sword.Position,game.Player.transform.position);
        sword.Tactics.Enabled=true;yield return Until(()=>sword.Actions.Busy,.4f);sword.Tactics.Enabled=false;
        Check("retreat beyond hit prediction still permits useful closing dash",sword.Actions.Ability==BossAbility.DrawDash);
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Active,.8f);
        yield return Until(()=>!sword.Motor.DashActive,.8f);
        Check("closing dash reduces actual separation",Vector2.Distance(sword.Position,game.Player.transform.position)<beforeGap-.5f,
            "before="+beforeGap+" after="+Vector2.Distance(sword.Position,game.Player.transform.position));
        heldKeys=Array.Empty<Key>();

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-11);
        yield return Seconds(.3f);heldKeys=new[]{Key.Space};yield return Seconds(.08f);heldKeys=Array.Empty<Key>();
        yield return Until(()=>sword.Perception.Facts.Velocity.y>3,.4f);
        Check("air target uses an ordinary observed jump",sword.Perception.Facts.Velocity.y>3&&sword.Motor.Grounded);
        Check("ground-start dash against jumping target accepted",sword.Actions.TryStart(BossAbility.DrawDash,sword.Perception.Facts.Position));
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked,.6f);
        Check("jump interception retains two-dimensional aim",sword.Actions.Ability==BossAbility.DrawDash
            &&sword.Actions.Phase==BossActionPhase.Locked&&sword.Actions.Aim.y>.1f,"aim="+sword.Actions.Aim);
        yield return Until(()=>!sword.Actions.Busy,2);
        yield return Until(()=>sword.Motor.Grounded,2);
        Check("diagonal attack ends and resumes grounded locomotion",!sword.Actions.Busy&&sword.Motor.Grounded);

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-6);
        yield return Seconds(.3f);var facts=sword.Perception.Facts;
        var miss=GuardShot(sword,sword.Position+new Vector2(6,2),Vector2.left);
        Check("guard ignores nearby fly-by",!sword.Tactics.GuardOpportunity(sword.Position,facts,out _));miss.Retire();
        var late=GuardShot(sword,sword.Position+Vector2.right*2,Vector2.left);
        Check("guard rejects projectile arriving before preparation",!sword.Tactics.GuardOpportunity(sword.Position,facts,out _));late.Retire();
        var leaving=GuardShot(sword,sword.Position+Vector2.right*6,Vector2.right);
        Check("guard ignores outgoing shot",!sword.Tactics.GuardOpportunity(sword.Position,facts,out _));leaving.Retire();
        var arriving=GuardShot(sword,sword.Position+Vector2.right*6,Vector2.left);
        Check("guard recognizes frontal timed body collision",sword.Tactics.GuardOpportunity(sword.Position,facts,out _));
        float guardX=sword.Position.x;hp=sword.Health.CurrentHealth;sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy,.2f);sword.Tactics.Enabled=false;
        Check("normal policy chooses timely reflection",sword.Actions.Ability==BossAbility.Guard);
        yield return Until(()=>arriving==null||arriving.Deflected,.6f);
        Check("selected guard reflects real bullet",sword.Actions.Guarded==1&&sword.Health.CurrentHealth==hp);
        Check("selected guard does not walk",Mathf.Abs(sword.Position.x-guardX)<.02f);
        if(arriving!=null)arriving.Retire();
        yield return Until(()=>!sword.Actions.Busy,1.5f);
        Check("no incoming shot means no stationary guard",!sword.Tactics.GuardOpportunity(sword.Position,sword.Perception.Facts,out _));

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-9);
        yield return Seconds(.3f);
        Check("public dodge fixture uses ordinary executor",game.Dodge.TryDodge(1));
        yield return Until(()=>sword.Perception.Facts.Dodging,.5f);
        facts=sword.Perception.Facts;
        Check("dodge recognition is delayed public evidence",facts.Dodging&&sword.Perception.LastDodgeObservedAt>0
            &&Time.time-facts.ObservedAt>=sword.Profile.ReactionSeconds-.04f);
        Vector2 prediction=sword.Tactics.PredictSwordBody(facts,.8f);
        Check("finite dodge is not extrapolated at burst speed",prediction.x-facts.Position.x<5,
            "predictionDelta="+(prediction-facts.Position)+" velocity="+facts.Velocity);
        yield return Seconds(.3f);
        Check("observed dodge window does not alter player cooldown",game.Dodge.CooldownRemaining>0&&!game.Dodge.TryDodge(1));

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-13);OnFloor(game.Player.gameObject,sword.Navigation,-11);
        yield return Seconds(.3f);
        Check("recovery fixture starts ordinary Rhythm",sword.Actions.TryStart(BossAbility.Rhythm,sword.Perception.Facts.Position));
        heldKeys=new[]{Key.D};sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Recovery,2);
        float recoveryAt=Time.time;int attacks=sword.Actions.AttackId;
        Check("recovery anticipates retreat with bounded step",sword.Actions.RecoveryStepping&&sword.Actions.RecoverySteps>0);
        yield return Seconds(.25f);
        Check("planning never cancels full recovery",sword.Actions.Phase==BossActionPhase.Recovery&&sword.Actions.AttackId==attacks);
        yield return Until(()=>sword.Actions.AttackId>attacks||!sword.Actions.Busy,1);
        Check("recovery duration preserved",Time.time-recoveryAt>=sword.Profile.Rhythm.Recovery-.06f);
        heldKeys=Array.Empty<Key>();sword.Tactics.Enabled=false;

        for(int wave=1;wave<=3;wave++)
        {
            yield return Fresh(wave);MouseAt(game.Bosses[0].Position,true);
            heldKeys=new[]{Key.D};yield return Seconds(1);heldKeys=new[]{Key.A,Key.Space};yield return Seconds(.3f);
            heldKeys=new[]{Key.A};yield return Seconds(.7f);heldKeys=Array.Empty<Key>();yield return Seconds(2);
            mouseHeld=false;
            Check("short W"+wave+" combat/input progresses",game.Phase==DuelPhase.Fight&&game.FightClock>3&&game.Rifle.ShotsFired>0);
            foreach(var boss in game.Bosses)Record("W"+wave+" "+boss.Profile.Kind+" starts="+string.Join(",",boss.Actions.Started)
                +" hits="+boss.Actions.ConnectedHits+" recoveryPlans="+boss.Tactics.RecoveryPlansUsed);
        }
        game.Player.TakeDamage(100000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,.5f);
        Check("controlled defeat resolves once",game.Phase==DuelPhase.Failed);
        game.Reconfigure();yield return Frames(2);game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        Check("retry resets observed dodge history",game.Phase==DuelPhase.Fight&&game.Bosses.All(b=>b.Perception.LastDodgeObservedAt<0));
        game.MainMenu();yield return Frames(2);
        Check("menu cleans actors and projectiles",Projectile.Active.Count==0&&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0);
    }
}
#endif
