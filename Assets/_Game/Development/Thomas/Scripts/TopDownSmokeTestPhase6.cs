#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>Phase 6 MSP fixtures only: positioned targets/protection/forced outcomes, not balance.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Phase6Msp()
    {
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        Record("MSP: virtual input/uGUI, positioned Ground/High C targets, protected observation and forced outcomes. Not a balance test.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        var f = flow.ActiveFight; var b = f.Boss;
        var p = f.Player.GetComponent<Rigidbody2D>(); var rb = b.GetComponent<Rigidbody2D>();
        v2ProtectedPlayer = f.Player;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstWarning);
        bool freshP1 = b.GunEquipped && b.GetComponent<SpriteRenderer>().color.a == 1;
        f.BossHealth.TakeDamage(410, f.Player.gameObject);
        p.position = new Vector2(4, -1.69f); p.linearVelocity = Vector2.zero;
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        Check("P1 -> P2", freshP1 && !b.GunEquipped && b.BladeEquipped && b.PhaseTransitionsStarted == 1,
            "threshold damage -> gun discarded -> blade/P2");
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        int hp = f.Player.CurrentHealth; int shots = b.ShotsFired;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashExecute);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashRecovery);
        Check("Ghost Slash Regression", b.SlashesStarted == 1 && f.Player.CurrentHealth == hp - 22,
            $"existing rush real damage {hp}->{f.Player.CurrentHealth}; recovery");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;

        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutWarning);
        float firstX = rb.position.x;
        float firstDirection = Phase6Direction(b);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutFirst);
        // Move after lock, onto the other side of the first rush, to prove the second re-samples.
        float lockedEnd = (float)typeof(CyberpsychoBoss).GetField("crosscutEndX", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(b);
        p.position = new Vector2(lockedEnd - firstDirection * 3f, -1.69f); p.linearVelocity = Vector2.zero;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutGap);
        float gapX = rb.position.x; float secondDirection = Phase6Direction(b);
        bool retargeted = secondDirection == Mathf.Sign(p.position.x - rb.position.x) && firstDirection != secondDirection;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase6-crosscut.png");
        // After the second lock, move again; its direction must remain fixed during execution.
        p.position = new Vector2(gapX - secondDirection * 3f, -1.69f); p.linearVelocity = Vector2.zero;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutSecond);
        bool locked = Phase6Direction(b) == secondDirection;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutRecovery);
        float secondX = rb.position.x;
        p.position = new Vector2(8, -1.69f); p.linearVelocity = Vector2.zero;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropWarning);
        Check("Crosscut", b.CrosscutSlashesStarted == 2 && Mathf.Abs(gapX-firstX)>1 && Mathf.Abs(secondX-gapX)>1
            && retargeted && locked, $"two slashes x={firstX:0.00}->{gapX:0.00}->{secondX:0.00}; reacts between locks={retargeted}; no homing={locked}; recovery completed");

        yield return Phase6Drop(f, false);
        Check("P2 Three-Attack Loop", b.SlashesStarted == 1 && b.CrosscutSlashesStarted == 2 && b.DropsStarted == 1
            && b.Phase == CyberpsychoBoss.BossPhase.Phase2 && b.ShotsFired == shots,
            "Ghost Slash -> Crosscut -> Predator Drop, no P1 gunfire");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        p.position = new Vector2(0, 2.12f); p.linearVelocity = Vector2.zero;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropWarning);
        yield return Phase6Drop(f, true);

        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        f.Player.TakeDamage(f.Player.MaxHealth, b.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        yield return DeployAndFocus(flow);
        var fresh = flow.ActiveFight;
        Check("P2 Death / Redeploy Reset", fresh != f && fresh.Boss.Phase == CyberpsychoBoss.BossPhase.Phase1
            && fresh.Boss.GunEquipped && !fresh.Boss.BladeEquipped && fresh.BossHealth.CurrentHealth == 800
            && fresh.Boss.CrosscutSlashesStarted == 0 && fresh.Boss.DropsStarted == 0
            && fresh.Boss.GetComponent<SpriteRenderer>().color.a == 1 && fresh.Boss.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic,
            "fresh P1/full HP/gun/opaque/dynamic body after P2 death");
        fresh.BossHealth.TakeDamage(800, fresh.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("Core Flow Still Works", flow.State == GameSessionFlow.FlowState.Result,
            "Continue/Loadout/redeploy/controlled victory/Result");
    }

    private static float Phase6Direction(CyberpsychoBoss b) =>
        (float)typeof(CyberpsychoBoss).GetField("chargeDirection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(b);

    private IEnumerator Phase6Drop(BossFight f, bool high)
    {
        var b = f.Boss; var rb = b.GetComponent<Rigidbody2D>();
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropTarget);
        yield return Frames(3); // observe the rendered mark after the scripted upward reposition
        float startY = rb.position.y;
        bool setup = startY > f.Player.transform.position.y + 2f && rb.bodyType == RigidbodyType2D.Kinematic;
        var mark = b.transform.Find("ChargeWarning_TEST").GetComponent<SpriteRenderer>();
        bool marker = mark.enabled && Mathf.Abs(mark.transform.position.y - (high ? 1.38f : -2.42f)) < .2f;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase6-drop-" + (high ? "high" : "ground") + ".png");
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        int hp = f.Player.CurrentHealth;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropExecute);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropImpact);
        float impactY = rb.position.y;
        int after = f.Player.CurrentHealth;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropRecovery);
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        Check(high ? "Predator Drop - High C" : "Predator Drop - Ground",
            setup && marker && startY-impactY > 3 && after == hp-26 && rb.bodyType == RigidbodyType2D.Dynamic
            && Mathf.Abs(impactY-(high ? 2.07f : -1.73f)) < .2f,
            $"setup={setup}, marker={marker}; y={startY:0.00} -> impact y={impactY:0.00}; real damage {hp}->{after}; recovery entered; platform intact");
        // Let the recovery complete; confirms it cannot strand the selector.
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
    }
}
#endif
