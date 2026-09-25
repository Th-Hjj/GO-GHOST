#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Editor-only phase fixtures. No formal scene/prefab references and no player controls.</summary>
public partial class TopDownSmokeTest
{
    [MenuItem("GO GHOST/Development/Boss/Run phase foundation smoke")]
    private static void RunPhase3Smoke() => EnterPlayModeAndLaunch("phase3-boss");

    [MenuItem("GO GHOST/Development/Boss/Damage to phase threshold")]
    private static void DebugPhaseThreshold()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        if (!Application.isPlaying || flow == null || flow.State != GameSessionFlow.FlowState.Combat) return;
        Phase3DamageToThreshold(flow.ActiveFight);
    }

    private static void Phase3DamageToThreshold(BossFight fight)
    {
        int hp = Mathf.FloorToInt(fight.BossHealth.MaxHealth * fight.Boss.Phase2HealthThreshold);
        fight.BossHealth.TakeDamage(Mathf.Max(0, fight.BossHealth.CurrentHealth - hp), fight.Player.gameObject);
    }

    private void Phase3Fresh(GameSessionFlow flow, string label)
    {
        var f = flow.ActiveFight;
        var b = f.Boss;
        Check(label + " fresh Boss", b.Phase == CyberpsychoBoss.BossPhase.Phase1 && b.GunEquipped && !b.BladeEquipped
            && !b.TransitionPending && b.PhaseTransitionsStarted == 0 && b.PhaseTimeRemaining == 0
            && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth && f.BossHealth.MaxHealth == 800
            && b.State == CyberpsychoBoss.AttackState.Arrival && b.StateTimeRemaining > 0 && b.ShotsFired == 0
            && b.SimulationRate == 1 && Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x) < .01f,
            $"HP={f.BossHealth.CurrentHealth}; phase={b.Phase}; state={b.State}; timer={b.StateTimeRemaining:0.000}; gun={b.GunEquipped}; blade={b.BladeEquipped}");
        var so = new SerializedObject(b);
        Check(label + " fresh target/clock", so.FindProperty("player").objectReferenceValue == f.Player
            && f.Player.GetComponent<PlayerTimeSlow>().WorldRate == 1, "serialized target belongs to current attempt");
    }

    private IEnumerator Phase3WaitAction(CyberpsychoBoss b, CyberpsychoBoss.AttackState action)
    {
        float end = Time.time + 12;
        while (b.State != action && Time.time < end) yield return null;
        if (b.State != action) throw new InvalidOperationException("Phase3 expected action " + action + "; got " + b.State);
    }

    private IEnumerator Phase3WaitPhase(CyberpsychoBoss b, CyberpsychoBoss.BossPhase phase)
    {
        float end = Time.time + 8;
        while (b.Phase != phase && Time.time < end) yield return null;
        if (b.Phase != phase) throw new InvalidOperationException("Phase3 expected " + phase + "; got " + b.Phase);
    }

    private IEnumerator Phase3Progress(BossFight f, string label, bool capture = false)
    {
        var b = f.Boss;
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.PhaseTransition);
        int shots = b.ShotsFired;
        var oldBullets = f.GetComponentsInChildren<Projectile>();
        Check(label + " safe transition", b.PhaseTransitionsStarted == 1 && !b.TransitionPending
            && b.State == CyberpsychoBoss.AttackState.Stopped && b.StateTimeRemaining == 0
            && !b.GunEquipped && !b.BladeEquipped && Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x) < .01f,
            "one phase request; no action timer/movement; gun discarded; existing bullets=" + oldBullets.Length);
        if (capture)
        {
            Check("in-flight P1 bullet survives transition entry", oldBullets.Length > 0, "actual P1 shot, no synthetic projectile or forced removal");
            yield return PolishScreen("phase3-discard-gun");
        }
        // Additional below-threshold damage cannot restart the transition or its timer.
        f.BossHealth.TakeDamage(1, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        Check(label + " P2 blade placeholder", b.PhaseTransitionsStarted == 1 && b.BladeEquipped && !b.GunEquipped
            && b.ShotsFired == shots && b.State == CyberpsychoBoss.AttackState.Stopped && b.PhaseTimeRemaining == 0,
            "gun scheduler stopped; blade visible; no old state resumed");
        b.BeginCombat(); // Regression: accidental restart may not rewind this actor to P1.
        yield return Seconds(capture ? 3.3f : .15f);
        Check(label + " P2 cannot restart P1", b.Phase == CyberpsychoBoss.BossPhase.Phase2
            && b.PhaseTransitionsStarted == 1 && b.ShotsFired == shots
            && Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x) < .01f, "no re-entry, projectile scheduler or stuck dash velocity");
        if (capture)
        {
            Check("P1 bullets finish naturally in P2", oldBullets.All(p => p == null) && f.GetComponentsInChildren<Projectile>().Length == 0,
                "contact/lifetime only; no transition bullet purge");
            yield return PolishScreen("phase3-blade-placeholder");
            yield return Frames(3); // CaptureScreenshot writes at frame end, before the next death fixture.
        }
    }

    private IEnumerator Phase3DefeatRedeploy(GameSessionFlow flow, string label, bool clearLoadout = false)
    {
        var old = flow.ActiveFight;
        var oldBoss = old.Boss;
        var oldRig = oldBoss.transform.Find("WeaponRig_TEST");
        int outcomes = 0;
        old.OnResolved += _ => outcomes++;
        v2ProtectedPlayer = null;
        old.Player.IsInvulnerable = false;
        Keys(); MouseTo(.5f, .5f);
        old.Player.TakeDamage(old.Player.MaxHealth, old.Boss.gameObject);
        yield return Frames(3);
        Check(label + " attempt stopped once", outcomes == 1 && flow.State == GameSessionFlow.FlowState.DefeatTransition
            && oldBoss.State == CyberpsychoBoss.AttackState.Stopped && oldBoss.StateTimeRemaining == 0
            && oldBoss.PhaseTimeRemaining == 0 && !oldBoss.TransitionPending && !oldBoss.GunEquipped && !oldBoss.BladeEquipped
            && oldBoss.SimulationRate == 1 && oldBoss.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero
            && old.GetComponentsInChildren<Projectile>().Length == 0, "old weapons, timers, velocity, clock and projectiles cleared");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        Check(label + " old hierarchy disposed", old == null && oldBoss == null && oldRig == null, "no orphan weapon objects");
        if (clearLoadout) ClearDraftV2(flow);
        yield return DeployAndFocus(flow);
        Phase3Fresh(flow, label + " redeploy");
    }

    private IEnumerator Phase3VictoryNewGame(GameSessionFlow flow, string label, int transitions)
    {
        var f = flow.ActiveFight;
        var b = f.Boss;
        int count = 0;
        f.OnResolved += _ => count++;
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth, f.Player.gameObject);
        f.BossHealth.TakeDamage(1, f.Player.gameObject);
        Check(label + " death synchronous", b.Phase == CyberpsychoBoss.BossPhase.Dead && !b.GunEquipped && !b.BladeEquipped
            && !b.TransitionPending && b.PhaseTimeRemaining == 0 && b.StateTimeRemaining == 0
            && b.PhaseTransitionsStarted == transitions && f.BossHealth.CurrentHealth == 0, "Enemy.OnDied owns death; no delayed draw or negative HP");
        yield return Seconds(.55f);
        Check(label + " one victory", count == 1 && f.Result == BossFight.Outcome.Won
            && flow.State == GameSessionFlow.FlowState.VictoryTransition && b.Phase == CyberpsychoBoss.BossPhase.Dead
            && !b.BladeEquipped, "waited past weapon-change time; no duplicate outcome");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check(label + " winning snapshot", flow.Victory != null && flow.Victory.BodyModificationPercent == f.Deployment.BodyModificationPercent,
            "existing Result, no ending routing");
        v2ProtectedPlayer = null;
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        Check(label + " old Boss destroyed by New Game", b == null && f == null, "fresh prefab, no combat state in Archive");
        Phase3Fresh(flow, label + " New Game");
    }

    private IEnumerator Phase3Boss()
    {
        Record("Phase3: actual Play Mode physics/Input System/uGUI. Forced damage and invulnerability are isolated flow/FSM fixtures, not human winnability evidence.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        Phase3Fresh(flow, "initial");
        var f = flow.ActiveFight;
        var b = f.Boss;
        Check("configurable normalized threshold", Mathf.Approximately(b.Phase2HealthThreshold, .5f), "serialized TEST default, no scattered 400 HP constant");
        v2ProtectedPlayer = f.Player;
        yield return PolishScreen("phase3-gun-equipped");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.ShotRecovery);
        Check("initial P1 real projectile", b.ShotsFired == 1 && f.GetComponentsInChildren<Projectile>().Length > 0, "actual old directed-shot branch");
        Phase3DamageToThreshold(f);
        yield return new WaitForFixedUpdate(); yield return null;
        Check("threshold request latched before recovery completes", b.Phase == CyberpsychoBoss.BossPhase.Phase1 && b.TransitionPending,
            "HP=" + f.BossHealth.CurrentHealth + "; state=" + b.State);
        yield return Phase3Progress(f, "normal", true);
        yield return VerifyOneBullet(flow, 12, "P2 remains damageable on open ground");
        yield return Phase3VictoryNewGame(flow, "P2 fatal", 1);
        // A lethal hit from above the threshold must not briefly draw a blade.
        yield return Phase3VictoryNewGame(flow, "P1 fatal above threshold", 0);

        yield return Phase3DefeatRedeploy(flow, "player death P1");
        // Preserve the real four-attack loop for long enough to prove it repeats.
        yield return PressureV2(flow);
        Check("old loop remains P1 above threshold", flow.ActiveFight.Boss.Phase == CyberpsychoBoss.BossPhase.Phase1
            && flow.ActiveFight.Boss.PhaseTransitionsStarted == 0 && flow.ActiveFight.BossHealth.CurrentHealth == 800,
            "25 seconds, no phase request without damage");
        flow.ActiveFight.Boss.BeginCombat();

        // Every reachable action state is a boundary probe; running attacks reach their own safe end.
        var actions = new[] { CyberpsychoBoss.AttackState.Arrival, CyberpsychoBoss.AttackState.ShotWarning,
            CyberpsychoBoss.AttackState.ShotRecovery, CyberpsychoBoss.AttackState.ChargeWarning,
            CyberpsychoBoss.AttackState.Charge, CyberpsychoBoss.AttackState.ChargeRecovery,
            CyberpsychoBoss.AttackState.SpreadWarning, CyberpsychoBoss.AttackState.SpreadBurst,
            CyberpsychoBoss.AttackState.SpreadRecovery, CyberpsychoBoss.AttackState.ComboWarning,
            CyberpsychoBoss.AttackState.ComboCharge, CyberpsychoBoss.AttackState.ComboGap,
            CyberpsychoBoss.AttackState.ComboRecovery };
        foreach (var action in actions)
        {
            f = flow.ActiveFight; b = f.Boss; v2ProtectedPlayer = f.Player;
            yield return Phase3WaitAction(b, action);
            if (action == CyberpsychoBoss.AttackState.Charge || action == CyberpsychoBoss.AttackState.ComboCharge)
            {
                yield return Seconds(.06f);
                Check(action + " executing before crossing", Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x) > 15,
                    "real dash velocity=" + b.GetComponent<Rigidbody2D>().linearVelocity.x);
            }
            f.BossHealth.TakeDamage(action == CyberpsychoBoss.AttackState.Arrival ? 700 : 405, f.Player.gameObject);
            yield return new WaitForFixedUpdate(); yield return null;
            Check(action + " request exactly once", b.TransitionPending && b.PhaseTransitionsStarted == 0,
                "HP=" + f.BossHealth.CurrentHealth + "; phase=" + b.Phase + "; remaining=" + b.StateTimeRemaining);
            yield return Phase3Progress(f, action.ToString());
            yield return Phase3DefeatRedeploy(flow, "player death P2 after " + action);
        }

        // Fatal damage while a dash has requested, but not yet entered, transition.
        f = flow.ActiveFight; b = f.Boss; v2ProtectedPlayer = f.Player;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.Charge);
        Phase3DamageToThreshold(f); yield return new WaitForFixedUpdate(); yield return null;
        Check("pending lethal setup", b.TransitionPending, "dash still owns execution");
        yield return Phase3VictoryNewGame(flow, "pending-transition fatal", 0);

        f = flow.ActiveFight; b = f.Boss; v2ProtectedPlayer = f.Player;
        Phase3DamageToThreshold(f); yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.PhaseTransition);
        yield return Phase3VictoryNewGame(flow, "transition fatal", 1);

        // Mutual-death arbitration in both callback orders remains failure, once per frame.
        for (int order = 0; order < 2; order++)
        {
            f = flow.ActiveFight; b = f.Boss; v2ProtectedPlayer = f.Player;
            Phase3DamageToThreshold(f); yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.PhaseTransition);
            int outcomes = 0; f.OnResolved += _ => outcomes++;
            v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
            if (order == 0) f.BossHealth.TakeDamage(800, f.Player.gameObject);
            f.Player.TakeDamage(f.Player.MaxHealth, f.Boss.gameObject);
            if (order == 1) f.BossHealth.TakeDamage(800, f.Player.gameObject);
            yield return Seconds(.5f);
            Check("transition mutual death order " + order, outcomes == 1 && f.Result == BossFight.Outcome.Died
                && flow.State == GameSessionFlow.FlowState.DefeatTransition && flow.Victory == null
                && b.Phase == CyberpsychoBoss.BossPhase.Dead && !b.GunEquipped && !b.BladeEquipped && b.PhaseTimeRemaining == 0,
                "existing LateUpdate arbitration: failure priority");
            yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout); yield return DeployAndFocus(flow);
            Phase3Fresh(flow, "mutual death redeploy " + order);
        }

        // P2 death during an active Torso clock exercises phase + augmentation cleanup together.
        yield return Phase3DefeatRedeploy(flow, "pre-augmentation setup");
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("TorsoButton"); yield return ClickSessionButton("BrainButton");
        yield return Phase2Select(flow, LoadoutSlot.LeftLeg, LoadoutRules.LegDoubleJump);
        yield return Phase2Select(flow, LoadoutSlot.RightLeg, LoadoutRules.LegHover);
        yield return DeployAndFocus(flow); Phase3Fresh(flow, "80% mixed deployment");
        f = flow.ActiveFight; b = f.Boss; v2ProtectedPlayer = f.Player;
        Phase3DamageToThreshold(f); yield return Phase3Progress(f, "mixed build phase change");
        Keys(Key.E); yield return Frames(3); Keys();
        Check("P2 active Torso clock", f.Player.GetComponent<PlayerTimeSlow>().IsActive && b.SimulationRate == .25f,
            "existing E; P2 presentation uses the same attempt clock");
        var rig = b.transform.Find("WeaponRig_TEST");
        Check("P2 facing and no weapon collider", rig.localScale.x < 0 && rig.GetComponentsInChildren<Collider2D>(true).Length == 0,
            "faces player; temporary blade cannot cause accidental contact damage");
        yield return Phase3DefeatRedeploy(flow, "P2 slow + AI + mixed legs", true);
        Check("phase redeploy Human snapshot", flow.Deployment.BodyModificationPercent == 0 && !flow.Deployment.HasDoubleJump
            && !flow.Deployment.HasHover && !flow.Deployment.HasTimeSlow && flow.Deployment.Aim == AimControl.Manual,
            "no stale phase or augmentation flags");
        flow.ActiveFight.Boss.StopCombat();
    }
}
#endif
