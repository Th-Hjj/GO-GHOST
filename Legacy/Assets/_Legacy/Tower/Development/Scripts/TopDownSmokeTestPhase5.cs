#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

/// <summary>Targeted P2 happy path; controlled positioning/outcomes do not prove balance.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Phase5Msp()
    {
        Record("MSP: virtual input/uGUI, ground target fixture, protected repeated observation and forced outcomes. Not a balance test.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        var f = flow.ActiveFight; var b = f.Boss;
        v2ProtectedPlayer = f.Player;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstWarning);
        bool freshP1 = b.GunEquipped && b.GetComponent<SpriteRenderer>().color.a == 1;
        f.BossHealth.TakeDamage(410, f.Player.gameObject);
        var playerBody = f.Player.GetComponent<Rigidbody2D>();
        playerBody.position = new Vector2(4, -1.69f); playerBody.linearVelocity = Vector2.zero;
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        Check("Enter P2", freshP1 && !b.GunEquipped && b.BladeEquipped && b.PhaseTransitionsStarted == 1,
            "real threshold damage -> weapon change -> blade combat");
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        int hp = f.Player.CurrentHealth; int shots = b.ShotsFired;
        bool warning = b.State == CyberpsychoBoss.AttackState.SlashWarning
            && b.transform.Find("ChargeWarning_TEST").GetComponent<SpriteRenderer>().enabled;
        var body = b.GetComponent<SpriteRenderer>();
        float minAlpha = body.color.a, maxAlpha = minAlpha;
        float end = Time.time + .2f;
        while (Time.time < end)
        {
            minAlpha = Mathf.Min(minAlpha, body.color.a); maxAlpha = Mathf.Max(maxAlpha, body.color.a);
            yield return null;
        }
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase5-telegraph.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashExecute);
        float x = b.transform.position.x;
        yield return Seconds(.06f);
        float speed = Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x);
        float executeAlpha = body.color.a;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase5-slash.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashRecovery);
        Check("Ghost Slash", warning && b.SlashesStarted == 1 && speed > 20
            && Mathf.Abs(b.transform.position.x - x) > 1 && f.Player.CurrentHealth == hp - 22,
            $"telegraph -> locked ground rush -> real blade hit HP {hp}->{f.Player.CurrentHealth}; speed={speed:0.0}; recovery entered");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        float recoveryAlpha = body.color.a;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase5-recovery.png");
        yield return Frames(3);
        Check("Persistent Optical Camouflage", minAlpha >= .45f && maxAlpha < .8f && maxAlpha - minAlpha > .02f
            && executeAlpha > .45f && executeAlpha < .8f && recoveryAlpha > .45f && recoveryAlpha < .8f,
            $"warning alpha {minAlpha:0.00}..{maxAlpha:0.00}; execute={executeAlpha:0.00}; recovery={recoveryAlpha:0.00}; cyan/pink flicker");
        bool brightBlade = true;
        foreach (var sprite in b.transform.Find("WeaponRig_TEST/Blade_PLACEHOLDER").GetComponentsInChildren<SpriteRenderer>())
            brightBlade &= sprite.color.a >= .9f;
        Check("Boss Still Readable (render state)", brightBlade && warning && minAlpha >= .45f,
            "body never invisible, blade alpha >= .9, directional telegraph active; captured images require visual review");
        for (int i = 0; i < 2; i++)
        {
            yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
            yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashExecute);
            yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashRecovery);
        }
        Check("Repeated Ghost Slash Loop", b.SlashesStarted == 3 && b.ShotsFired == shots && b.Phase == CyberpsychoBoss.BossPhase.Phase2,
            "three real telegraph/execute/recovery cycles; no P1 projectiles started");

        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        f.Player.TakeDamage(f.Player.MaxHealth, b.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        yield return DeployAndFocus(flow);
        var fresh = flow.ActiveFight;
        Check("P2 Death / Redeploy Reset", fresh != f && fresh.Boss.Phase == CyberpsychoBoss.BossPhase.Phase1
            && fresh.Boss.GunEquipped && !fresh.Boss.BladeEquipped && fresh.BossHealth.CurrentHealth == 800
            && fresh.Boss.SlashesStarted == 0 && fresh.Boss.GetComponent<SpriteRenderer>().color.a == 1,
            "P2 defeat -> Continue/Loadout -> fresh full HP P1, gun and normal opacity");
        fresh.BossHealth.TakeDamage(800, fresh.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("Core Flow Still Works", flow.State == GameSessionFlow.FlowState.Result,
            "combat -> P2 slash -> defeat -> Continue/Loadout -> redeploy -> controlled victory -> Result");
    }
}
#endif
