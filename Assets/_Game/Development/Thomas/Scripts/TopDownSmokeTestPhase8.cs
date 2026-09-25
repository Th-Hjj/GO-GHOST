#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Phase 8 targeted input/contact/telegraph checks. Fixtures do not prove balance.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Phase8Msp()
    {
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        Record("MSP: virtual keyboard/uGUI; forced HP/death/victory, positioned targets and protected observation. Real counters use no invulnerability. Not a balance test.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("LeftArmButton"); // Rapid Fire
        yield return ClickSessionButton("LeftArmButton"); // Counter
        yield return ClickSessionButton("BrainButton"); // Targeting AI
        yield return ClickSessionButton("TorsoButton");
        bool loadout = flow.Draft.GetSlot(LoadoutSlot.LeftArm).ItemId == LoadoutRules.CounterArm
            && flow.Draft.GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.BrainAI
            && flow.Draft.BodyModificationPercent == 70;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase8-loadout.png");
        yield return DeployAndFocus(flow);
        var f = flow.ActiveFight; var b = f.Boss;
        var counter = f.Player.GetComponent<PlayerCounterArm>();
        var slow = f.Player.GetComponent<PlayerTimeSlow>();
        var input = f.Player.GetComponent<PlayerInputReader>();
        var p = f.Player.GetComponent<Rigidbody2D>();
        p.position = new Vector2(4, -1.69f); p.linearVelocity = Vector2.zero;
        int hp = f.Player.CurrentHealth;
        Projectile incoming = null;
        yield return Phase8Until(() => {
            incoming = f.GetComponentsInChildren<Projectile>().FirstOrDefault(x => x.CanBeCountered
                && Vector2.Distance(x.transform.position, p.position) < 2.2f);
            return incoming != null;
        }, "incoming Burst projectile");
        Keys(Key.E); yield return Frames(2); Keys();
        bool eWindow = counter.IsWindowOpen || counter.CountersSucceeded == 1;
        bool eNoSlow = !slow.IsActive;
        yield return Phase8Until(() => counter.CountersSucceeded == 1, "P1 projectile counter", 2);
        Check("Counter P1", loadout && counter.IsEquipped && f.Player.CurrentHealth == hp && !f.Player.IsInvulnerable,
            $"real Burst contact nullified; HP {hp}->{f.Player.CurrentHealth}; Left Arm10/Brain40/Torso20; E opened window");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        yield return Frames(2);
        bool retired = incoming == null;
        Keys(Key.Q); yield return Frames(2); Keys();
        bool qSlow = slow.IsActive;
        yield return Frames(2);
        // Dodge intentionally rejects existing invulnerability; suspend observation protection.
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        Keys(Key.A, Key.LeftShift); yield return Frames(2); Keys();
        bool shifted = f.Player.GetComponent<PlayerDodge>().IsDodging && slow.IsActive && p.linearVelocity.x < -10;
        Check("Shift / E / Q", eWindow && eNoSlow && qSlow && shifted && retired,
            $"E window={eWindow}, E leaves Slow off={eNoSlow}, Q Slow={qSlow}, Shift Dodge={shifted}, projectile retired={retired}");
        v2ProtectedPlayer = f.Player;
        yield return Phase8Until(() => !slow.IsActive, "Time Slow completion", 4);
        foreach (var bullet in f.GetComponentsInChildren<Projectile>()) bullet.Retire();
        p.position = new Vector2(4, -1.69f); p.linearVelocity = Vector2.zero;
        b.GetComponent<Rigidbody2D>().position = new Vector2(8, -1.73f);
        f.BossHealth.TakeDamage(410, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        yield return Phase8Until(() => b.State == CyberpsychoBoss.AttackState.SlashWarning && b.StateTimeRemaining < .1f,
            "Ghost Slash contact setup");
        foreach (var bullet in f.GetComponentsInChildren<Projectile>()) bullet.Retire();
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        hp = f.Player.CurrentHealth;
        Keys(Key.E); yield return Frames(2); Keys();
        yield return Phase8Until(() => b.BladeParries == 1, "normal blade parry", 2);
        bool stagger = b.State == CyberpsychoBoss.AttackState.SlashRecovery && b.StateTimeRemaining > .6f
            && Mathf.Abs(b.GetComponent<Rigidbody2D>().linearVelocity.x) < .01f;
        Check("Counter P2", counter.CountersSucceeded == 2 && f.Player.CurrentHealth == hp && stagger && !f.Player.IsInvulnerable,
            $"real Ghost Slash parry, HP {hp}->{f.Player.CurrentHealth}; Boss stopped in .9s recovery");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase8-counter.png");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        var shooting = f.Player.GetComponent<PlayerShooting>();
        int shots = shooting.ShotsFired;
        var start = p.position;
        AimAtWorld(f.Player.transform.position + Vector3.left * 10, false);
        yield return Seconds(.15f);
        bool noAuto = shooting.ShotsFired == shots && Mathf.Abs(p.position.x-start.x)<.1f;
        Keys(Key.D); yield return Seconds(.18f); Keys();
        bool moved = p.position.x > start.x + .5f;
        Keys(Key.Space); yield return Frames(2); Keys();
        bool jumped = p.linearVelocity.y > 1;
        Mouse(.02f,.9f,true); yield return Seconds(.12f); MouseTo(.02f,.9f);
        bool fired = shooting.ShotsFired > shots;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutFirst);
        yield return Frames(3);
        bool tracksRush = Phase8Tracks(f);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropTarget);
        yield return Frames(3);
        bool tracksVertical = Phase8Tracks(f);
        Check("Targeting AI", input.AimMode == AimControl.AI && noAuto && moved && jumped && fired
            && tracksRush && tracksVertical && !b.PredictionVisible,
            "opposing mouse cannot override aim; tracks P2 rush/elevated Drop; no auto-fire/move; D/Space/LMB remain manual");

        // Replace AI with Cortex and remove Counter through the existing buttons.
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("BrainButton");
        yield return ClickSessionButton("LeftArmButton");
        yield return ClickSessionButton("TorsoButton");
        bool cortexChoice = flow.Draft.GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.PredictiveCortex
            && flow.Draft.GetSlot(LoadoutSlot.LeftArm).ItemId == "" && flow.Draft.BodyModificationPercent == 40;
        yield return DeployAndFocus(flow);
        var previous = f;
        f = flow.ActiveFight; b = f.Boss; input = f.Player.GetComponent<PlayerInputReader>();
        counter = f.Player.GetComponent<PlayerCounterArm>(); p = f.Player.GetComponent<Rigidbody2D>();
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        p.position = new Vector2(4, -1.69f); p.linearVelocity = Vector2.zero;
        AimAtWorld(f.Player.transform.position + Vector3.left * 10, false);
        Keys(Key.E); yield return Frames(3); Keys();
        bool removedCounter = !counter.IsEquipped && !counter.IsWindowOpen && counter.CountersSucceeded == 0;
        bool cortexManual = input.AimMode == AimControl.Manual && input.AimDirection.x < -.95f;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstRecovery);
        yield return Frames(2);
        bool earlyShot = b.PredictionVisible && b.PredictionCue.Contains("MARKED SHOT")
            && !b.transform.Find("ShotWarning_TEST").GetComponent<SpriteRenderer>().enabled;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase8-predict-marked.png");
        Check("Predictive Marked Shot", cortexChoice && cortexManual && earlyShot,
            "cyan projected shot line appears in preceding Burst recovery, before normal Marked tell; manual aim retained");
        f.BossHealth.TakeDamage(410, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        yield return Frames(2);
        bool ghostPath = b.PredictionVisible && b.PredictionCue.Contains("GHOST SLASH")
            && b.transform.Find("Prediction_TEST").GetComponent<SpriteRenderer>().bounds.size.x > 1;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutWarning);
        yield return Frames(2);
        bool crossPath = b.PredictionVisible && b.PredictionCue.Contains("CROSSCUT");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase8-predict-slash.png");
        Check("Predictive Slash", ghostPath && crossPath,
            "extra translucent cyan path for Ghost Slash and Crosscut; normal optical camouflage unchanged");
        f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - 200, f.Player.gameObject);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutSignal);
        yield return Frames(2);
        bool earlyBreak = b.PredictionVisible && b.PredictionCue.Contains("HIGH C")
            && !b.transform.Find("ChargeWarning_TEST").GetComponent<SpriteRenderer>().enabled
            && GameObject.Find("HighPlatformC") != null;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase8-predict-blackout.png");
        Check("Predictive BLACKOUT", earlyBreak,
            "High C failure line visible at BLACKOUT Signal, before two distortion passes and the ordinary break warning");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutRecovery);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
        bool ultimateContinued = b.BlackoutsStarted == 1 && GameObject.Find("HighPlatformC") == null;
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        yield return FixtureNextOfficer(flow);
        bool oldPredictionGone = b == null;
        yield return ClickSessionButton("BrainButton"); // None
        yield return DeployAndFocus(flow);
        f = flow.ActiveFight; b = f.Boss; input = f.Player.GetComponent<PlayerInputReader>();
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        AimAtWorld(f.Player.transform.position + Vector3.up * 8, false);
        yield return Frames(3);
        bool noneManual = input.AimMode == AimControl.Manual && input.AimDirection.y > .95f;
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.MarkedTracking);
        yield return Frames(2);
        Check("Brain Replacement Cleanup", previous == null && cortexChoice && cortexManual && removedCounter
            && oldPredictionGone && noneManual && flow.Deployment.BodyModificationPercent == 0
            && !b.PredictionVisible && b.transform.Find("Prediction_TEST") == null && b.PredictionCue == "",
            "AI -> Cortex -> None replaces 40% Brain item; manual aim restored; no stale line/target; removed Counter E does nothing");
        f.BossHealth.TakeDamage(800, f.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        v2ProtectedPlayer = null;
        Check("Core Flow Still Works", ultimateContinued && flow.State == GameSessionFlow.FlowState.Result,
            "upgraded attempts, BLACKOUT completion, defeat/Continue/Loadout/redeploy and controlled victory/Result");
    }

    private static bool Phase8Tracks(BossFight f)
    {
        Vector2 expected = f.Boss.GetComponent<Collider2D>().bounds.center - f.Player.transform.position;
        return Vector2.Dot(f.Player.GetComponent<PlayerInputReader>().AimDirection, expected.normalized) > .97f;
    }

    private IEnumerator Phase8Until(Func<bool> condition, string label, float seconds = 12)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        if (!condition()) throw new InvalidOperationException("Phase8 timed out: " + label);
    }
}
#endif
