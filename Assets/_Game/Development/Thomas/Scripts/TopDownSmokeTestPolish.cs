#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public partial class TopDownSmokeTest
{
    private IEnumerator Polish()
    {
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        var flow=FindAnyObjectByType<GameSessionFlow>();
        Record("Polish Play Mode gate: real virtual input/physics/uGUI. Forced outcomes only test presentation branches; no win/balance claim.");
        var previous=flow.State;float entered=Time.unscaledTime-flow.StateAge;bool firstTransition=true;
        System.Action<GameSessionFlow.FlowState> onState=state=>
        {
            float expected=previous switch {
                GameSessionFlow.FlowState.NewGameInitialization=>1.2f, GameSessionFlow.FlowState.Deployment=>1.2f,
                GameSessionFlow.FlowState.DefeatTransition=>1.8f, GameSessionFlow.FlowState.NextOfficerTransition=>1.4f,
                GameSessionFlow.FlowState.VictoryTransition=>1.8f, _=>0f};
            float actual=Time.unscaledTime-entered;
            bool coldEntry=firstTransition && previous==GameSessionFlow.FlowState.NewGameInitialization;
            if(expected>0) Check("hold "+previous,actual>=expected-.03f && (coldEntry || actual<expected+.25f),
                $"{actual:0.000}s / target {expected}"+(coldEntry ? "; initial Editor startup includes first-frame warmup; minimum hold checked" : ""));
            firstTransition=false;
            previous=state;entered=Time.unscaledTime;
            StartCoroutine(PolishScreen(state+"-"+flow.OfficerIndex));
        };
        flow.OnStateChanged+=onState;
        yield return PolishScreen("opening");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Deployment);
        Keys(Key.D,Key.Space,Key.LeftShift);MouseTo(.8f,.5f);
        yield return Seconds(.25f);
        Check("deployment holds combat input and Boss",!flow.ActiveFight.Player.GetComponent<PlayerInputReader>().GameplayInputEnabled
            && flow.ActiveFight.Boss.ShotsFired==0 && FindObjectsByType<Projectile>().Length==0,"actors settle while controls/FSM wait");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);
        Check("held preview input cannot leak",flow.ActiveFight.Player.GetComponent<PlayerInputReader>().MoveInput==Vector2.zero
            && !flow.ActiveFight.Player.GetComponent<PlayerDodge>().IsDodging,"release required after preview");
        FocusV2(flow);yield return Frames(3);flow.ActiveFight.Boss.StopCombat();
        CheckPolishArena(flow);CheckPolishColors(flow,"first Human");
        yield return PolishScreen("human-arena");

        var f=flow.ActiveFight;var rb=f.Player.GetComponent<Rigidbody2D>();
        var bossBody=f.Boss.GetComponent<Rigidbody2D>();
        Record("FIXTURE disable stationary Boss physics during boundary crossing only; restore before combat probes.");
        bossBody.simulated=false;
        Keys(Key.A);yield return Seconds(1.3f);Keys();yield return Frames(2);
        Check("larger arena left solid boundary",rb.position.x>=-14.68f && rb.position.x<-14.3f,$"x={rb.position.x:0.000}");
        Keys(Key.D);yield return Seconds(5.3f);Keys();yield return Frames(2);
        Check("larger arena right solid boundary",rb.position.x<=14.68f && rb.position.x>14.3f,$"x={rb.position.x:0.000}");
        AimAtWorld(f.Player.transform.position+Vector3.right*5,true);yield return Seconds(.055f);MouseTo(.5f,.5f);yield return Seconds(.4f);
        Check("wall projectile cleanup",FindObjectsByType<Projectile>().Length==0,"real shot into new right boundary");
        bossBody.position=new Vector2(9,-1.7f);bossBody.simulated=true;
        Keys(Key.Space);yield return Seconds(.1f);Keys();
        Check("jump after arena resize",rb.linearVelocity.y>5 && !f.Player.GetComponent<PlayerMovement>().IsGrounded,$"velocity={rb.linearVelocity}");
        yield return Seconds(.9f);
        yield return VerifyOneBullet(flow,12,"resized arena Human");
        Record("FIXTURE Boss death: first officer direct victory presentation, no prior Loadout.");
        int outcomes=0;f.OnResolved+=_=>outcomes++;
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth,f.Player.gameObject);
        yield return WaitForState(flow,GameSessionFlow.FlowState.Result);
        Check("first officer record and one outcome",outcomes==1 && flow.Victory.OfficerIndex==1 && flow.Victory.BodyModificationPercent==0 && !flow.AugmentationsUnlocked,"first try path requires no Loadout initialization");
        yield return ResultHoldAndNewGame(flow);

        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);
        f=flow.ActiveFight;outcomes=0;f.OnResolved+=_=>outcomes++;
        float deadline=Time.time+40;float deathStart=Time.time;
        while(flow.State==GameSessionFlow.FlowState.Combat && Time.time<deadline)yield return null;
        Check("real Boss defeat in expanded arena",flow.State==GameSessionFlow.FlowState.DefeatTransition && outcomes==1,$"{Time.time-deathStart:0.00}s; outcome count={outcomes}");
        Check("defeat instantly stops attacks",!f.Player.GetComponent<PlayerInputReader>().GameplayInputEnabled && f.Boss.State==CyberpsychoBoss.AttackState.Stopped,"presentation hold cannot keep combat running");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Loadout);
        Check("next officer identity and clean Loadout",flow.OfficerIndex==2 && flow.AugmentationsUnlocked && flow.ActiveFight==null && FindObjectsByType<Projectile>().Length==0,"new officer, no preceding actors/projectiles");
        Check("Loadout minimum read gate",!flow.CanDeploy && !flow.RequestDeploy(),"runtime rejects early Deploy");
        while(!flow.CanDeploy)yield return null;
        yield return PolishScreen("loadout-human");
        for(int i=0;i<6;i++)yield return ClickSessionButton(((LoadoutSlot)i)+"Button");
        yield return PolishScreen("loadout-rainbow");
        Check("seven slot UI palette",PolishSlotLabels(flow),"shared palette in selected slot names/outlines; purple Weapon label with white Basic Gun");
        yield return DeployAndFocus(flow);f=flow.ActiveFight;f.Boss.StopCombat();
        CheckPolishColors(flow,"all equipped");
        Check("unchanged augmented snapshot",flow.Deployment.BodyModificationPercent==100 && flow.Deployment.ShotsPerSecond==10
            && flow.Deployment.MoveSpeed==10 && flow.Deployment.BulletDamage==12 && flow.Deployment.HasTimeSlow,"all effects still derive from snapshot");
        yield return PolishScreen("rainbow-combat");
        yield return RateAndMovementV2(flow,"polish all equipped");
        yield return BrainV2(flow);
        yield return PressureV2(flow);
        f.Player.GetComponent<Rigidbody2D>().position=new Vector2(-9,-1.69f);
        f.Boss.GetComponent<Rigidbody2D>().position=new Vector2(9,-1.7f);FocusV2(flow);yield return Frames(3);
        yield return TorsoV2(flow);
        yield return PolishScreen("time-slow-active");
        var oldSlow=f.Player.GetComponent<PlayerTimeSlow>();
        Record("FIXTURE augmented Boss death: record/UI plus active-slow shutdown.");
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth,f.Player.gameObject);
        yield return WaitForState(flow,GameSessionFlow.FlowState.Result);
        var winning=flow.Victory;
        Check("augmented victory record freezes exact equipment",winning.BodyModificationPercent==100 && winning.WinningDeployment.HasTimeSlow
            && !oldSlow.IsActive && FindObjectsByType<Projectile>().Length==0,"record copy and no slow/projectiles during result hold");
        yield return PolishScreen("result-rainbow");
        yield return ResultHoldAndNewGame(flow);
        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);flow.ActiveFight.Boss.StopCombat();
        CheckPolishColors(flow,"New Game after augments");
        Check("New Game reset with old immutable record",flow.OfficerIndex==1 && !flow.AugmentationsUnlocked && flow.Victory==null
            && flow.Deployment.BodyModificationPercent==0 && flow.Deployment.Aim==AimControl.Manual && winning.BodyModificationPercent==100,"no old draft/effect/record leak");

        yield return FixtureNextOfficer(flow);while(!flow.CanDeploy)yield return null;
        for(int i=0;i<6;i++)yield return ClickSessionButton(((LoadoutSlot)i)+"Button");
        yield return DeployAndFocus(flow);flow.ActiveFight.Boss.StopCombat();
        Keys(Key.LeftShift);yield return Frames(3);Keys();
        yield return FixtureNextOfficer(flow);while(!flow.CanDeploy)yield return null;
        Check("next officer preserves editable draft only",flow.OfficerIndex==3 && flow.Draft.BodyModificationPercent==100,"Officer #3 after full-modifier death");
        for(int i=0;i<6;i++)yield return ClickSessionButton(((LoadoutSlot)i)+"Button");
        yield return DeployAndFocus(flow);flow.ActiveFight.Boss.StopCombat();
        CheckPolishColors(flow,"next officer fully removed");
        yield return RateAndMovementV2(flow,"polish removed");
        yield return VerifyManualAim(flow);yield return MeasureDodgeInterval(flow,.8f,"polish Torso removed");
        Check("fresh actors and no slow state",FindObjectsByType<PlayerHealth>().Length==1 && FindObjectsByType<CyberpsychoBoss>().Length==1
            && !flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>().IsActive && Time.timeScale==1,"one pair, ordinary dodge restored");
        yield return PolishScreen("removed-final");
        flow.OnStateChanged-=onState;
    }

    private IEnumerator ResultHoldAndNewGame(GameSessionFlow flow)
    {
        if(flow.HoldRemaining>0)Check("Result minimum read gate",!flow.CanNewGame && !flow.RequestNewGame(),"cannot dismiss before read window");
        while(!flow.CanNewGame)yield return null;
        yield return Seconds(1.2f);
        Check("Result persists until click",flow.State==GameSessionFlow.FlowState.Result,"no automatic exit after minimum hold");
        yield return ClickSessionButton("NewGameButton");
    }

    private IEnumerator PolishScreen(string label)
    {
        Record("NOT RUN: Presentation readability check removed by the full presentation reset.");
        yield break;
    }

    private void CheckPolishArena(GameSessionFlow flow)
    {
        var root=GameObject.Find("Arena").transform;
        var left=root.Find("Wall_Left").GetComponent<Collider2D>().bounds;
        var right=root.Find("Wall_RightLower").GetComponent<Collider2D>().bounds;
        var top=root.Find("Wall_Top").GetComponent<Collider2D>().bounds;
        var floor=root.Find("Wall_Bottom").GetComponent<Collider2D>().bounds;
        bool white=true;foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>())white &= sr.color==Color.white;
        Check("black background and white arena",Camera.main.backgroundColor==Color.black && white,"camera black; all four solid boundaries white");
        Check("arena exactly 1.5x inner dimensions",Mathf.Abs(right.min.x-left.max.x-30)<.01f && Mathf.Abs(top.min.y-floor.max.y-11.25f)<.01f,"20x7.5 -> 30x11.25");
        bool camera=true;foreach(float x in new[]{-15.5f,15.5f})foreach(float y in new[]{-3f,9.25f}){var v=Camera.main.WorldToViewportPoint(new Vector3(x,y));camera &= v.x>=0 && v.x<=1 && v.y>=0 && v.y<=1;}
        Check("fixed camera covers entire arena",camera,$"ortho={Camera.main.orthographicSize}; aspect={Camera.main.aspect}");
        Check("new spawns settle on floor",Mathf.Abs(flow.ActiveFight.Player.transform.position.x+9)<.1f && Mathf.Abs(flow.ActiveFight.Boss.transform.position.x-9)<.1f
            && flow.ActiveFight.Player.GetComponent<PlayerMovement>().IsGrounded,"spawns -9 / +9; grounded");
    }

    private void CheckPolishColors(GameSessionFlow flow,string label)
    {
        var visual=flow.ActiveFight.Player.GetComponent<OfficerVisual>();bool match=true;
        for(int i=0;i<7;i++) {var slot=(LoadoutSlot)i;match &= visual.Part(slot)!=null && visual.Part(slot).color==OfficerVisual.EquipmentColor(slot,flow.Deployment.GetSlot(slot).ItemId);}
        Check("rainbow/white visuals "+label,match,$"body={flow.Deployment.BodyModificationPercent}; all seven renderers match equipment palette");
    }

    private bool PolishSlotLabels(GameSessionFlow flow)
    {
        for(int i=0;i<6;i++)
        {
            var slot=(LoadoutSlot)i;var button=GameObject.Find(slot+"Button").GetComponent<UnityEngine.UI.Button>();
            if(button.GetComponent<UnityEngine.UI.Outline>().effectColor!=OfficerVisual.SlotColor(slot)
                || !button.GetComponentInChildren<TMP_Text>().text.Contains(ColorUtility.ToHtmlStringRGB(OfficerVisual.SlotColor(slot))))return false;
        }
        return GameObject.Find("Weapon").GetComponent<TMP_Text>().text.Contains(ColorUtility.ToHtmlStringRGB(OfficerVisual.SlotColor(LoadoutSlot.Weapon)));
    }
}
#endif
