#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private IEnumerator ClassDemo()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();
        Record("Class demo smoke: actual Play Mode virtual input/uGUI; controlled victory is only flow evidence.");
        var previous=flow.State;float entered=Time.unscaledTime-flow.StateAge;bool first=true;
        System.Action<GameSessionFlow.FlowState> timing=state=>
        {
            if(previous!=GameSessionFlow.FlowState.Combat)
            {
                float elapsed=Time.unscaledTime-entered;
                bool automatic=previous!=GameSessionFlow.FlowState.Loadout && previous!=GameSessionFlow.FlowState.Result;
                Check("8s "+previous,elapsed>=7.97f && (!automatic || first || elapsed<8.5f),$"actual={elapsed:0.000}s; manual pages may remain longer");
            }
            first=false;previous=state;entered=Time.unscaledTime;
        };
        flow.OnStateChanged+=timing;
        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);yield return Frames(3);
        var f=flow.ActiveFight;f.Boss.StopCombat();
        CheckPolishArena(flow);CheckPolishColors(flow,"demo Human");yield return PolishScreen("demo-start");
        yield return RateAndMovementV2(flow,"demo Human");yield return VerifyManualAim(flow);
        var rb=f.Player.GetComponent<Rigidbody2D>();
        Keys(Key.Space);yield return Seconds(.12f);Keys();
        Check("demo Space jump",rb.linearVelocity.y>5,"actual upward velocity="+rb.linearVelocity.y);
        yield return Seconds(.85f);yield return MeasureDodgeInterval(flow,.8f,"demo grounded Shift");
        yield return VerifyOneBullet(flow,12,"demo LMB");
        yield return PressureV2(flow);
        Check("demo actors remain inside enlarged arena",Mathf.Abs(f.Player.transform.position.x)<15 && Mathf.Abs(f.Boss.transform.position.x)<15,"after four-skill cycles and physical collisions");
        FocusV2(flow);f.Boss.BeginCombat();int outcomes=0;f.OnResolved+=_=>outcomes++;
        float deadline=Time.time+45;
        while(flow.State==GameSessionFlow.FlowState.Combat && Time.time<deadline)yield return null;
        Check("demo real Boss kill",flow.State==GameSessionFlow.FlowState.DefeatTransition && f.Player.IsDead && outcomes==1,"no forced Player damage; one outcome");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Loadout);
        Check("demo Officer 2 clean Loadout",flow.OfficerIndex==2 && flow.ActiveFight==null && FindObjectsByType<Projectile>().Length==0,"old actors/projectiles disposed");
        while(!flow.CanDeploy)yield return null;
        foreach(var button in new[]{"LeftArmButton","RightLegButton","SpineButton","BrainButton"})yield return ClickSessionButton(button);
        Check("demo one Arm one Leg Spine Brain",flow.Draft.BodyModificationPercent==80,"10+10+20+40=80%");
        yield return PolishScreen("demo-loadout");yield return DeployAndFocus(flow);
        f=flow.ActiveFight;f.Boss.StopCombat();CheckPolishColors(flow,"demo 80%");
        Check("demo fresh full-health deployment",f.Player.CurrentHealth==f.Player.MaxHealth && f.BossHealth.CurrentHealth==800 && flow.Deployment.ShotsPerSecond==7 && flow.Deployment.MoveSpeed==8,"fresh combat; snapshot 7 shots/s, move 8");
        yield return RateAndMovementV2(flow,"demo one Arm one Leg");yield return BrainV2(flow);
        rb=f.Player.GetComponent<Rigidbody2D>();rb.position=new Vector2(-9,-1.69f);f.Boss.GetComponent<Rigidbody2D>().position=new Vector2(9,-1.7f);
        FocusV2(flow);yield return Frames(3);yield return SpineV2(flow);
        Record("CONTROLLED VICTORY: existing health API to validate result/New Game only; not a human win.");
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth,f.Player.gameObject);
        yield return WaitForState(flow,GameSessionFlow.FlowState.Result);yield return PolishScreen("demo-result");
        Check("demo victory record",flow.Victory.OfficerIndex==2 && flow.Victory.BodyModificationPercent==80,"winning deployment copied");
        while(!flow.CanNewGame)yield return null;yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);flow.ActiveFight.Boss.StopCombat();
        CheckPolishColors(flow,"demo New Game");
        Check("demo New Game reset",flow.OfficerIndex==1 && flow.Deployment.BodyModificationPercent==0 && flow.Deployment.Aim==AimControl.Manual
            && !flow.AugmentationsUnlocked && flow.Victory==null && flow.ActiveFight.Player.CurrentHealth==100 && flow.ActiveFight.BossHealth.CurrentHealth==800
            && !flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>().IsActive,"Human/manual/full health, no old session or ability state");
        yield return PolishScreen("demo-reset");flow.OnStateChanged-=timing;
    }
}
#endif
