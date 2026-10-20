#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>Phase 7 happy path, with forced threshold/outcomes and a High C target fixture.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Phase7Msp()
    {
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        Record("MSP: forced HP threshold/outcomes, positioned High C player, observation protection; actual movement/damage/fall. Not a balance test.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var high = GameObject.Find("HighPlatformC");
        var highCollider = high.GetComponent<Collider2D>();
        var highVisual = high.GetComponent<SpriteRenderer>();
        var lowA = GameObject.Find("LowPlatformA"); var lowB = GameObject.Find("LowPlatformB");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        var f = flow.ActiveFight; var b = f.Boss;
        var p = f.Player.GetComponent<Rigidbody2D>(); var rb = b.GetComponent<Rigidbody2D>();
        v2ProtectedPlayer = f.Player;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstWarning);
        f.BossHealth.TakeDamage(410, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        // At 390/800, P2 starts with its normal move; only crossing 200 triggers BLACKOUT.
        bool normalStart = b.State == CyberpsychoBoss.AttackState.SlashWarning && !b.BlackoutUsed;
        p.position = new Vector2(0, 2.12f); p.linearVelocity = Vector2.zero;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashExecute);
        f.BossHealth.TakeDamage(190, f.Player.gameObject);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutSignal);
        Check("BLACKOUT Trigger", normalStart && b.Phase == CyberpsychoBoss.BossPhase.Phase2
            && b.BlackoutUsed && b.BlackoutsStarted == 1 && f.BossHealth.CurrentHealth == 200,
            "P2 normal attack -> 25% HP -> one BLACKOUT at safe recovery");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutRun);
        float firstX=rb.position.x, minAlpha=1f, maxTravel=0f;
        while (b.State != CyberpsychoBoss.AttackState.BlackoutMark)
        {
            minAlpha=Mathf.Min(minAlpha,b.GetComponent<SpriteRenderer>().color.a);
            maxTravel=Mathf.Max(maxTravel,Mathf.Abs(rb.position.x-firstX));
            yield return null;
        }
        Check("Distortion Runs", b.DistortionRunsStarted == 2 && maxTravel > 20 && minAlpha >= .29f && minAlpha < .5f,
            $"two arena passes; travel={maxTravel:0.0}; minimum visible body alpha={minAlpha:0.00}");
        yield return Frames(3);
        var mark=b.transform.Find("ChargeWarning_TEST").GetComponent<SpriteRenderer>();
        Check("High C Mark", high.activeInHierarchy && mark.enabled && Mathf.Abs(mark.transform.position.x-highCollider.bounds.center.x)<.1f
            && Mathf.Abs(mark.transform.position.y-highCollider.bounds.max.y-.08f)<.1f
            && mark.bounds.size.x >= highCollider.bounds.size.x,
            "bright cut warning covers High C while it is still solid/visible");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase7-high-mark.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutFinalWarning);
        v2ProtectedPlayer=null; f.Player.IsInvulnerable=false;
        int hp=f.Player.CurrentHealth;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutFinalSlash);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutBreakDelay);
        Check("Final Slash", f.Player.CurrentHealth == hp-32 && high.activeInHierarchy,
            $"real swept blade hit {hp}->{f.Player.CurrentHealth}; readable break delay before destruction");
        v2ProtectedPlayer=f.Player; f.Player.IsInvulnerable=true;
        float standingY=p.position.y;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutRecovery);
        bool disabled=!high.activeInHierarchy && !highCollider.isActiveAndEnabled && !highVisual.isVisible;
        yield return Seconds(.6f);
        bool falling=p.position.y < standingY-1f;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase7-high-destroyed.png");
        Check("High C Destroyed", disabled && falling && lowA.activeInHierarchy && lowB.activeInHierarchy,
            $"High C collision/render inactive; player y={standingY:0.00}->{p.position.y:0.00}; Low A/B intact");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutFirst);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropTarget);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
        Check("Post-BLACKOUT Combat", flow.State == GameSessionFlow.FlowState.Combat && b.Phase == CyberpsychoBoss.BossPhase.Phase2
            && b.BlackoutsStarted == 1 && b.DistortionRunsStarted == 2 && !high.activeInHierarchy
            && f.Player.GetComponent<PlayerMovement>().IsGrounded,
            "all three normal P2 attacks resume below threshold, BLACKOUT stays consumed, High C stays absent, player grounded");
        v2ProtectedPlayer=null; f.Player.IsInvulnerable=false;
        f.Player.TakeDamage(f.Player.MaxHealth,b.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        bool absentUntilDeploy=!high.activeInHierarchy;
        yield return DeployAndFocus(flow);
        var fresh=flow.ActiveFight;
        Check("Redeploy Restores High C", absentUntilDeploy && high.activeInHierarchy && highCollider.isActiveAndEnabled && highVisual.enabled
            && fresh!=f && fresh.Boss.Phase==CyberpsychoBoss.BossPhase.Phase1 && fresh.Boss.GunEquipped
            && !fresh.Boss.BlackoutUsed && fresh.Boss.BlackoutsStarted==0 && fresh.BossHealth.CurrentHealth==800
            && fresh.Boss.GetComponent<SpriteRenderer>().color.a==1,
            "High C restored only at new deployment; fresh P1/gun/full HP/normal appearance/unused BLACKOUT");
        fresh.BossHealth.TakeDamage(800,fresh.Player.gameObject);
        yield return WaitForState(flow,GameSessionFlow.FlowState.Result);
        Check("Core Flow Still Works",flow.State==GameSessionFlow.FlowState.Result,"Continue/Loadout/redeploy/controlled victory/Result");
    }
}
#endif
