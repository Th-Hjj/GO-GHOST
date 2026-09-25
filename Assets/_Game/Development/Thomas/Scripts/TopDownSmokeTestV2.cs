#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private PlayerHealth v2ProtectedPlayer;
    // Input focus cancellation can clear dodge i-frames. Reapply this explicitly labelled
    // test-only protection before physics, rather than relying on a one-time flag write.
    private void FixedUpdate()
    {
        if (v2ProtectedPlayer != null) v2ProtectedPlayer.IsInvulnerable = true;
    }
    private IEnumerator CombatV2()
    {
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        FocusV2(flow);
        Record("TEST-v2: real virtual keyboard/mouse, physics and projectile hits. Fixture overrides explicitly labelled; no claim of winnability.");
        Check("fresh domain-reload-off session", flow.OfficerIndex == 1 && !flow.AugmentationsUnlocked && flow.Victory == null, "Officer 1 / locked / no old record");
        flow.ActiveFight.Boss.StopCombat();
        VisualV2(flow, "human");
        yield return RateAndMovementV2(flow, "Human");
        yield return VerifyOneBullet(flow, 12, "Human");
        yield return VerifyManualAim(flow);
        yield return MeasureDodgeInterval(flow, .8f, "Human");
        yield return VerifyNoAirDodge(flow);

        // Normal combat: no protection, HP changes or direct damage. Let the harder boss kill this officer.
        var fight = flow.ActiveFight;
        fight.Player.GetComponent<Rigidbody2D>().position = new Vector2(-6, -1.69f);
        fight.Boss.BeginCombat(); Keys(); MouseTo(.5f, .5f);
        int outcomes = 0; fight.OnResolved += _ => outcomes++;
        float began = Time.time, deadline = began + 40;
        var damageSteps = new List<int>(); int previousHP = fight.Player.CurrentHealth;
        while (flow.State == GameSessionFlow.FlowState.Combat && Time.time < deadline)
        {
            if (fight.Player.CurrentHealth != previousHP) { damageSteps.Add(previousHP - fight.Player.CurrentHealth); previousHP = fight.Player.CurrentHealth; }
            yield return null;
        }
        Check("normal Boss defeat once", flow.State == GameSessionFlow.FlowState.DefeatTransition && outcomes == 1,
            $"seconds={Time.time-began:0.00}, damage steps={string.Join(",", damageSteps)}, outcomes={outcomes}");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        Check("death unlocks all six slots", flow.OfficerIndex == 2 && flow.AugmentationsUnlocked && FindObjectsByType<Projectile>().Length == 0, "new identity / no bullets");

        // Deploy every single body slot via real UI clicks. Stop the boss for measurements, never change shared stats.
        foreach (LoadoutSlot slot in new[] { LoadoutSlot.LeftArm, LoadoutSlot.RightArm, LoadoutSlot.LeftLeg, LoadoutSlot.RightLeg, LoadoutSlot.Brain, LoadoutSlot.Spine })
        {
            ClearDraftV2(flow);
            yield return ClickSessionButton(slot + "Button");
            Check("slot UI contribution " + slot, flow.Draft.BodyModificationPercent == LoadoutRules.MaximumContribution(slot), $"body={flow.Draft.BodyModificationPercent}");
            yield return DeployAndFocus(flow);
            flow.ActiveFight.Boss.StopCombat();
            VisualV2(flow, slot.ToString());
            if (slot == LoadoutSlot.LeftArm || slot == LoadoutSlot.RightArm || slot == LoadoutSlot.LeftLeg || slot == LoadoutSlot.RightLeg)
                yield return RateAndMovementV2(flow, slot.ToString());
            if (slot == LoadoutSlot.LeftArm || slot == LoadoutSlot.RightArm) yield return VerifyOneBullet(flow, 12, slot.ToString());
            if (slot == LoadoutSlot.Brain) yield return BrainV2(flow);
            if (slot == LoadoutSlot.Spine) yield return SpineV2(flow);
            yield return FixtureNextOfficer(flow);
            Check("attempt disposal " + slot, FindObjectsByType<Projectile>().Length == 0 && FindObjectsByType<PlayerHealth>().Length == 0 && Time.timeScale == 1, "actors/bullets gone; global clock unchanged");
        }
        ClearDraftV2(flow);
        for (int i=0; i<6; i++) yield return ClickSessionButton(((LoadoutSlot)i) + "Button");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/v2-loadout-all.png");
        yield return Frames(2);
        yield return DeployAndFocus(flow);
        flow.ActiveFight.Boss.StopCombat(); VisualV2(flow, "all100");
        Check("all slots = 100; weapon zero", flow.Deployment.BodyModificationPercent == 100 && flow.Deployment.GetSlot(LoadoutSlot.Weapon).BodyContributionPercent == 0, "six independent contributions");
        yield return RateAndMovementV2(flow, "all100");
        Check("snapshot cannot change in combat", !flow.TrySetDraftSlot(LoadoutSlot.Brain, "") && !flow.Draft.TrySetSlot(LoadoutSlot.Spine, ""), "both edit entry points reject");
        Keys(Key.LeftShift); yield return Frames(3); Keys();
        var oldSlow = flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>();
        Check("combined ability/aim", oldSlow.IsActive && flow.ActiveFight.Player.GetComponent<PlayerInputReader>().AimMode == AimControl.AI, "all modifiers coexist");
        yield return FixtureNextOfficer(flow);
        ClearDraftV2(flow); yield return DeployAndFocus(flow);
        flow.ActiveFight.Boss.StopCombat(); VisualV2(flow, "removed");
        Check("next officer clears slow", oldSlow == null && !flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>().IsActive && flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>().CooldownRemaining == 0, "new component instance");
        yield return RateAndMovementV2(flow, "all removed");
        yield return VerifyManualAim(flow);
        yield return MeasureDodgeInterval(flow, .8f, "Spine removed");

        // Observe several full attack cycles with explicit fixture protection, not a balance/win test.
        yield return PressureV2(flow);
        yield return FixtureNextOfficer(flow);
        for (int i=0; i<6; i++) flow.TrySetDraftSlot((LoadoutSlot)i, LoadoutRules.ModuleForSlot((LoadoutSlot)i));
        yield return DeployAndFocus(flow);
        Keys(Key.LeftShift); yield return Frames(3); Keys();
        var ending = flow.ActiveFight; var endingSlow = ending.Player.GetComponent<PlayerTimeSlow>();
        int resolved=0; ending.OnResolved += _ => resolved++;
        Record("FIXTURE direct Boss death only to verify slow cleanup, immutable record and NEW GAME; not a normal victory claim.");
        ending.BossHealth.TakeDamage(ending.BossHealth.MaxHealth, ending.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        var record = flow.Victory;
        Check("outcome cancels active slow once", !endingSlow.IsActive && endingSlow.WorldRate == 1 && resolved == 1
            && ending.Boss.State == CyberpsychoBoss.AttackState.Stopped && FindObjectsByType<Projectile>().Length == 0, "no corpse attacks or remaining projectiles");
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        flow.ActiveFight.Boss.StopCombat(); VisualV2(flow, "newgame");
        Check("NEW GAME clears v2 modifiers/session", flow.OfficerIndex == 1 && flow.Deployment.BodyModificationPercent == 0
            && flow.Deployment.Aim == AimControl.Manual && !flow.AugmentationsUnlocked && flow.Victory == null && flow.Deployment.WeaponId == LoadoutRules.BasicGun,
            "Officer1 / 0 / manual / locked / Basic Gun / no old victory");
        Check("old winning snapshot immutable", record.BodyModificationPercent == 100 && record.WinningDeployment.HasTimeSlow && record.WinningDeployment.ShotsPerSecond == 10, "New Game cannot mutate old record");
        yield return RateAndMovementV2(flow, "New Game");
        Check("one actor pair remains", FindObjectsByType<PlayerHealth>().Length == 1 && FindObjectsByType<CyberpsychoBoss>().Length == 1
            && FindObjectsByType<BossFight>().Length == 1, "no duplicated actors after repeated deployments");
        Keys(); MouseTo(.5f,.5f);
    }

    private void FocusV2(GameSessionFlow flow)
    {
        flow.ActiveFight.Player.GetComponent<PlayerInputReader>().SendMessage("OnApplicationFocus", true);
        Keys(); MouseTo(.5f,.5f);
    }

    private IEnumerator TimeRecoveryV2()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);
        flow.ActiveFight.Boss.StopCombat();yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("SpineButton");yield return DeployAndFocus(flow);
        var f=flow.ActiveFight;var slow=f.Player.GetComponent<PlayerTimeSlow>();
        float deadline=Time.time+3;
        while(f.Boss.State!=CyberpsychoBoss.AttackState.ChargeWarning && Time.time<deadline)yield return null;
        Keys(Key.LeftShift);yield return Frames(3);Keys();
        Record("FIXTURE protection after input activation; verify actual velocities, ability HUD and simultaneous-death cleanup.");
        v2ProtectedPlayer=f.Player;
        Check("follow-up activated",slow.IsActive,"real Shift input");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/v2-slow-active.png");
        while(slow.IsActive)yield return null;
        yield return new WaitForFixedUpdate();yield return Frames(2);
        float restored=Mathf.Abs(f.Boss.GetComponent<Rigidbody2D>().linearVelocity.x);
        Check("actual Boss speed restores after slow",restored>15.5f && restored<16.1f,$"charge velocity={restored:0.000}");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/v2-slow-cooldown.png");
        bool normalBullet=false;deadline=Time.time+3;
        while(!normalBullet && Time.time<deadline)
        {
            if(f.Boss.State==CyberpsychoBoss.AttackState.SpreadWarning)ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/v2-spread-open.png");
            foreach(var p in FindObjectsByType<Projectile>())if(p.OwnerLayer==f.Boss.gameObject.layer && p.GetComponent<Rigidbody2D>().linearVelocity.magnitude>=11.9f)normalBullet=true;
            yield return null;
        }
        Check("actual hostile projectile speed restores",normalBullet,"post-slow spread projectile speed >=11.9");
        while(slow.CooldownRemaining>0)yield return null;
        FocusV2(flow);yield return Frames(3);
        Record($"Air setup: grounded={f.Player.GetComponent<PlayerMovement>().IsGrounded}, position={f.Player.transform.position}, velocity={f.Player.GetComponent<Rigidbody2D>().linearVelocity}");
        Keys(Key.Space);yield return Seconds(.1f);Keys(Key.LeftShift);yield return Frames(3);Keys();
        Check("Spine works in air without dodge",slow.IsActive && !f.Player.GetComponent<PlayerMovement>().IsGrounded
            && !f.Player.GetComponent<PlayerDodge>().IsDodging,$"active={slow.IsActive}, grounded={f.Player.GetComponent<PlayerMovement>().IsGrounded}, dodge={f.Player.GetComponent<PlayerDodge>().IsDodging}, velocity={f.Player.GetComponent<Rigidbody2D>().linearVelocity}");
        int outcomes=0;f.OnResolved+=_=>outcomes++;
        v2ProtectedPlayer=null;f.Player.IsInvulnerable=false;
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth,f.Player.gameObject);
        f.Player.TakeDamage(f.Player.MaxHealth,f.Boss.gameObject);
        yield return Frames(3);
        Check("same-step mutual death cancels slow and resolves once",outcomes==1 && flow.State==GameSessionFlow.FlowState.DefeatTransition
            && !slow.IsActive && slow.WorldRate==1 && FindObjectsByType<Projectile>().Length==0,"TEST failure priority; no remaining ability/bullets");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Loadout);
    }

    private void ClearDraftV2(GameSessionFlow flow)
    {
        for (int i=0; i<6; i++) flow.TrySetDraftSlot((LoadoutSlot)i, "");
    }

    private void VisualV2(GameSessionFlow flow, string label)
    {
        var player=flow.ActiveFight.Player;var visual=player.GetComponent<OfficerVisual>(); bool correct=true;
        for(int i=0;i<6;i++)
        {
            var part=visual.Part((LoadoutSlot)i);
            correct &= part != null && part.enabled && part.sprite != null
                && part.color == (flow.Deployment.HasModule((LoadoutSlot)i) ? OfficerVisual.AugmentedColor : Color.white);
        }
        Check("stickman " + label, correct && player.transform.Find("FirePoint/AimMarker").GetComponent<SpriteRenderer>().enabled,
            $"six distinct parts; body={flow.Deployment.BodyModificationPercent}; weapon visible");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/v2-"+label+".png");
    }

    private IEnumerator RateAndMovementV2(GameSessionFlow flow, string label)
    {
        var f=flow.ActiveFight; var player=f.Player; var rb=player.GetComponent<Rigidbody2D>();
        var shooting=player.GetComponent<PlayerShooting>();
        Record("FIXTURE stationary Boss for measured real shots/physics: " + label);
        Keys(); MouseTo(.5f,.5f); yield return Seconds(.3f);
        var times=new List<float>(); int old=shooting.ShotsFired; float end=Time.time+1.15f;
        while(Time.time<end)
        {
            AimAtWorld(player.transform.position + Vector3.left*5, true);
            yield return null;
            if(shooting.ShotsFired!=old){times.Add(Time.time);old=shooting.ShotsFired;}
        }
        MouseTo(.5f,.5f); float interval=times.Count>1 ? (times[times.Count-1]-times[0])/(times.Count-1) : 99;
        Check("real fire cadence " + label, times.Count>=4 && Mathf.Abs(interval-flow.Deployment.FireInterval)<.055f,
            $"shots={times.Count}; measured interval={interval:0.000}; target={flow.Deployment.FireInterval:0.000}; damage={shooting.BulletDamage}");
        yield return Seconds(.3f); // clear the final shot before the one-hit damage probe
        float start=rb.position.x; Keys(Key.D); yield return Seconds(.25f);
        float right=rb.linearVelocity.x, distance=rb.position.x-start;
        Keys(Key.A); yield return Seconds(.16f); float left=rb.linearVelocity.x;Keys();
        Check("real move speed " + label, right>flow.Deployment.MoveSpeed-.4f && left < -flow.Deployment.MoveSpeed+.4f && distance>.8f,
            $"left={left:0.00}, right={right:0.00}, distance={distance:0.00}, target={flow.Deployment.MoveSpeed}");
        yield return Frames(3);
    }

    private IEnumerator BrainV2(GameSessionFlow flow)
    {
        var f=flow.ActiveFight;var reader=f.Player.GetComponent<PlayerInputReader>();var gun=f.Player.GetComponent<PlayerShooting>();
        var target=f.Boss.GetComponent<Rigidbody2D>(); target.simulated=false; int shots=gun.ShotsFired;
        float dot=1, muzzleDot=1;
        for(int i=0;i<90;i++)
        {
            float side=i<45 ? -1 : 1;
            target.position=(Vector2)f.Player.transform.position + new Vector2(side*2, Mathf.Sin(i*.1f));
            AimAtWorld(f.Player.transform.position-Vector3.right*side*5,false);
            yield return Frames(2);
            Vector2 expected=((Vector2)f.Boss.GetComponent<Collider2D>().bounds.center-(Vector2)f.Player.transform.position).normalized;
            dot=Mathf.Min(dot,Vector2.Dot(reader.AimDirection,expected));
            muzzleDot=Mathf.Min(muzzleDot,Vector2.Dot(f.Player.transform.Find("FirePoint").right,expected));
        }
        Check("Brain exclusive moving target / opposite mouse", dot>.995f && muzzleDot>.995f && gun.ShotsFired==shots,
            $"min aim dot={dot}, muzzle={muzzleDot}; no automatic fire");
        Check("Brain cannot toggle during combat", !flow.TrySetDraftSlot(LoadoutSlot.Brain, ""), "snapshot stays AI");
        target.position=new Vector2(6,-1.7f);target.simulated=true;
        // Let physics update the collider center after the moving-target fixture.
        yield return new WaitForFixedUpdate();yield return Frames(3);
        yield return VerifyOneBullet(flow,12,"Brain fire still requires click");
    }

    private IEnumerator SpineV2(GameSessionFlow flow)
    {
        var f=flow.ActiveFight;var slow=f.Player.GetComponent<PlayerTimeSlow>();var dodge=f.Player.GetComponent<PlayerDodge>();
        var rb=f.Player.GetComponent<Rigidbody2D>();f.Boss.BeginCombat();
        Projectile hostile=null; float deadline=Time.time+3;
        while(hostile==null && Time.time<deadline){foreach(var p in FindObjectsByType<Projectile>())if(p.OwnerLayer==f.Boss.gameObject.layer)hostile=p;yield return null;}
        while(f.Boss.State!=CyberpsychoBoss.AttackState.ChargeWarning && Time.time<deadline) yield return null;
        float normal=hostile!=null ? hostile.GetComponent<Rigidbody2D>().linearVelocity.magnitude : 0;
        float started=Time.unscaledTime;Keys(Key.LeftShift);yield return Frames(3);Keys();
        Check("Spine replaces dodge, no free invulnerability", slow.IsActive && !dodge.IsDodging && !f.Player.IsInvulnerable && Time.timeScale==1,
            $"active={slow.IsActive}; dodge={dodge.IsDodging}; global={Time.timeScale}");
        yield return new WaitForFixedUpdate();
        Check("existing hostile bullet slows immediately", hostile!=null && Mathf.Abs(hostile.GetComponent<Rigidbody2D>().linearVelocity.magnitude-normal*.25f)<.1f,
            $"normal={normal}; slowed={(hostile!=null?hostile.GetComponent<Rigidbody2D>().linearVelocity.magnitude:0)}");
        Record("FIXTURE protection for full cooldown/attack timing observation only."); v2ProtectedPlayer=f.Player; f.Player.IsInvulnerable=true;
        float stateTimer=f.Boss.StateTimeRemaining; yield return Seconds(.12f);
        Check("Boss timer slows", stateTimer-f.Boss.StateTimeRemaining>.015f && stateTimer-f.Boss.StateTimeRemaining<.055f, $"timer delta={stateTimer-f.Boss.StateTimeRemaining:0.000} in ~0.12 real seconds");
        Keys(Key.D,Key.Space); AimAtWorld(f.Player.transform.position+Vector3.up*5,true);yield return Seconds(.12f);Keys();MouseTo(.5f,.5f);
        bool fastPlayerBullet=false; foreach(var p in FindObjectsByType<Projectile>())if(p.OwnerLayer==f.Player.gameObject.layer && p.GetComponent<Rigidbody2D>().linearVelocity.magnitude>17.5f)fastPlayerBullet=true;
        Check("Spine player move/jump/fire remains full speed", rb.linearVelocity.y>5 && fastPlayerBullet, $"velocity={rb.linearVelocity}; normal player bullet={fastPlayerBullet}");
        bool slowCharge=false;
        while(slow.IsActive){if(f.Boss.State==CyberpsychoBoss.AttackState.Charge && Mathf.Abs(f.Boss.GetComponent<Rigidbody2D>().linearVelocity.x-(-4))<.3f)slowCharge=true;yield return null;}
        float duration=Time.unscaledTime-started;
        Check("Spine 3 real seconds then restores", duration>=2.98f && duration<3.15f && f.Boss.SimulationRate==1 && slowCharge, $"duration={duration:0.000}; charge at 4 units/s={slowCharge}");
        Keys(Key.LeftShift);yield return Frames(3);Keys();
        Check("Spine cooldown rejects both abilities", !slow.IsActive && !dodge.IsDodging && slow.CooldownRemaining>6, $"remaining={slow.CooldownRemaining:0.00}");
        while(slow.CooldownRemaining>0)yield return null;
        float ready=Time.unscaledTime-started;Keys(Key.LeftShift);yield return Frames(3);Keys();
        Check("Spine ready at 10 seconds and can repeat", slow.IsActive && ready>=9.98f && ready<10.15f, $"start interval={ready:0.000}");
        v2ProtectedPlayer=null;f.Player.IsInvulnerable=false; // caller immediately kills this officer to test active-slow disposal
    }

    private IEnumerator PressureV2(GameSessionFlow flow)
    {
        var f=flow.ActiveFight;f.Player.GetComponent<Rigidbody2D>().position=new Vector2(-6,-1.69f);
        v2ProtectedPlayer=f.Player;f.Player.IsInvulnerable=true;f.Boss.BeginCombat();
        Record("FIXTURE protection: observe 25s of actual Boss FSM/physics, not winnability.");
        var bullets=new HashSet<Projectile>();var states=new HashSet<CyberpsychoBoss.AttackState>();
        int maxAlive=0,comboCharges=0,spreadEntries=0;var last=CyberpsychoBoss.AttackState.Stopped;
        float end=Time.time+25; bool warnings=true;
        while(Time.time<end)
        {
            var state=f.Boss.State; states.Add(state);
            if(state!=last){Record("Boss state " + state);if(state==CyberpsychoBoss.AttackState.ComboCharge)comboCharges++;if(state==CyberpsychoBoss.AttackState.SpreadWarning){spreadEntries++;ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/v2-boss-spread.png");}last=state;}
            if(state==CyberpsychoBoss.AttackState.SpreadWarning){int rays=0;foreach(var sr in f.Boss.GetComponentsInChildren<SpriteRenderer>())if(sr.name.StartsWith("SpreadRay")&&sr.enabled)rays++;warnings &= rays==3;}
            var live=FindObjectsByType<Projectile>();maxAlive=Mathf.Max(maxAlive,live.Length);foreach(var p in live)bullets.Add(p);
            yield return null;
        }
        Check("harder Boss four skills repeat without stall", states.Contains(CyberpsychoBoss.AttackState.ShotWarning) && states.Contains(CyberpsychoBoss.AttackState.Charge)
            && spreadEntries>=3 && comboCharges>=6 && warnings, $"spread attacks={spreadEntries}; combo dashes={comboCharges}; visible fan rays={warnings}");
        Check("finite pressure / no projectile leak", f.Boss.ShotsFired>=40 && maxAlive<=12 && !f.Player.IsDead,
            $"actual spawned={f.Boss.ShotsFired} /25s; max simultaneous={maxAlive}; HP={f.BossHealth.MaxHealth}");
        f.Boss.StopCombat();yield return Seconds(3.2f);
        Check("projectiles expire without manual cleanup", FindObjectsByType<Projectile>().Length==0, "walls/hits/local lifetime remove all bullets");
        v2ProtectedPlayer=null;f.Player.IsInvulnerable=false;
    }
}
#endif
