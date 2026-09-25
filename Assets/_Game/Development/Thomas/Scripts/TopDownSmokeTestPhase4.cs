#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>One MSP happy-path cycle, targeted pressure samples, phase change and core flow.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Phase4Msp()
    {
        Record("MSP: virtual input/uGUI, protected observation, temporary player positions for height-band hits, controlled outcomes. Not a balance test.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        var f = flow.ActiveFight; var b = f.Boss; var playerBody = f.Player.GetComponent<Rigidbody2D>();
        v2ProtectedPlayer = f.Player;
        Check("fresh P1 gun", b.GunEquipped && b.Phase == CyberpsychoBoss.BossPhase.Phase1, "Phase 3 foundation retained");

        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstWarning);
        bool burstWarning = b.transform.Find("ShotWarning_TEST").GetComponent<SpriteRenderer>().enabled;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstFire);
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase4-burst.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstRecovery);
        Check("Burst Fire", burstWarning && b.ShotsFired == 3, "short visible warning then three real projectiles");

        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.MarkedTracking);
        bool markedWarning = b.transform.Find("ShotWarning_TEST").GetComponent<SpriteRenderer>().enabled;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.MarkedLock);
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase4-marked.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.MarkedRecovery);
        Check("Marked Shot", markedWarning && b.ShotsFired == 4, "tracking line -> lock -> one stronger shot");

        // Clear the ground path for the movement sample; no Boss state, speed or collider overrides.
        playerBody.position = new Vector2(0, 2.11f); playerBody.linearVelocity = Vector2.zero;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.GhostMove);
        float x = b.transform.position.x;
        float alpha = b.GetComponent<SpriteRenderer>().color.a;
        yield return Seconds(.15f);
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase4-ghost.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.GhostRecovery);
        bool ghostMoved = Mathf.Abs(b.transform.position.x - x) > 3 && alpha >= .2f && alpha < .8f
            && b.GetComponent<SpriteRenderer>().color.a == 1;
        float moved = b.transform.position.x - x;

        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SweepWarning);
        bool lowWarning = !b.IsHighSweep && b.transform.Find("ChargeWarning_TEST").GetComponent<SpriteRenderer>().enabled;
        playerBody.position = new Vector2(0, -1.69f); playerBody.linearVelocity = Vector2.zero;
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        int hp = f.Player.CurrentHealth;
        float end = Time.time + 3;
        while (f.Player.CurrentHealth == hp && Time.time < end) yield return null;
        Check("Low Sweep", lowWarning && f.Player.CurrentHealth < hp, $"real horizontal projectile at ground height: HP {hp}->{f.Player.CurrentHealth}");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        Check("Ghost Reposition", ghostMoved && b.ShotsFired > 4, $"alpha={alpha}; moved x={moved:0.00}; opaque and firing again");
        // Move to the real High C surface before the high warning; high bullets use normal collision.
        playerBody.position = new Vector2(0, 2.11f); playerBody.linearVelocity = Vector2.zero;
        end = Time.time + 5;
        while (!(b.State == CyberpsychoBoss.AttackState.SweepWarning && b.IsHighSweep) && Time.time < end) yield return null;
        bool highWarning = b.State == CyberpsychoBoss.AttackState.SweepWarning && b.IsHighSweep;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase4-high-sweep.png");
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        hp = f.Player.CurrentHealth; end = Time.time + 3;
        while (f.Player.CurrentHealth == hp && Time.time < end) yield return null;
        Check("High Sweep", highWarning && f.Player.CurrentHealth < hp && playerBody.position.y > 1.8f,
            $"real hit on stationary High C player: HP {hp}->{f.Player.CurrentHealth}; y={playerBody.position.y:0.00}");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;

        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstWarning);
        Check("P1 Attack Loop", b.ShotsFired == 14 && b.Phase == CyberpsychoBoss.BossPhase.Phase1,
            "Burst3 -> Marked1 -> Ghost -> Low5 -> High5 -> next Burst");
        f.BossHealth.TakeDamage(410, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.PhaseTransition);
        bool discarded = !b.GunEquipped && !b.BladeEquipped;
        int shots = b.ShotsFired;
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        yield return Seconds(.4f);
        Check("P1 -> P2", discarded && b.BladeEquipped && !b.GunEquipped && b.ShotsFired == shots
            && b.PhaseTransitionsStarted == 1 && b.GetComponent<SpriteRenderer>().color.a == 1,
            "no P1 firing/camouflage left; existing gun-to-blade transition");

        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        f.Player.TakeDamage(f.Player.MaxHealth, b.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        yield return DeployAndFocus(flow);
        bool fresh = flow.ActiveFight != f && flow.ActiveFight.Boss.GunEquipped
            && flow.ActiveFight.Boss.Phase == CyberpsychoBoss.BossPhase.Phase1 && flow.ActiveFight.BossHealth.CurrentHealth == 800;
        flow.ActiveFight.BossHealth.TakeDamage(800, flow.ActiveFight.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("Core Flow Still Works", fresh && flow.State == GameSessionFlow.FlowState.Result,
            "combat -> new moves -> P2 -> controlled defeat -> Continue/Loadout -> fresh redeploy -> controlled victory -> Result");
    }
}
#endif
