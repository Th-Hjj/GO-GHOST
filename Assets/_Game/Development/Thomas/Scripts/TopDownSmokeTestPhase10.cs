#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One integrated MSP session. Controlled outcomes prove flow, not balance.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Phase10Msp()
    {
        Record("MSP integration: real input/UI/physics and autonomous Boss/Drone; controlled HP/deaths and observation protection; isolated disk Archive. Not a natural victory/balance test.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var view = FindAnyObjectByType<SessionView>();
        var high = GameObject.Find("HighPlatformC");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow); yield return Frames(3);
        var f = flow.ActiveFight;
        var p = f.Player.GetComponent<Rigidbody2D>();
        var input = f.Player.GetComponent<PlayerInputReader>();
        float x = p.position.x;
        Keys(Key.D); yield return Seconds(.18f); Keys();
        bool moved = p.position.x > x + .5f;
        AimAtWorld(f.Boss.transform.position, true); yield return Seconds(.12f);
        MouseTo(.5f, .5f);
        bool fired = f.Player.GetComponent<PlayerShooting>().ShotsFired > 0;
        Keys(Key.Space); yield return Frames(2); Keys();
        bool jumped = p.linearVelocity.y > 1;
        yield return Phase8Until(() => f.Player.GetComponent<PlayerMovement>().IsGrounded, "base jump landing", 4);
        Keys(Key.A, Key.LeftShift); yield return Frames(2); Keys();
        bool dodged = f.Player.GetComponent<PlayerDodge>().IsDodging;
        Check("Base Input / Arena", moved && fired && jumped && dodged && input.AimMode == AimControl.Manual
            && high.activeInHierarchy && GameObject.Find("LowPlatformA") && GameObject.Find("LowPlatformB"),
            $"A/D={moved}, mouse/manual + LMB={fired}, Space={jumped}, Shift={dodged}; all platforms present");
        yield return Seconds(.4f);
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("LeftArmButton"); yield return ClickSessionButton("LeftArmButton");
        yield return ClickSessionButton("BrainButton"); yield return ClickSessionButton("BrainButton");
        yield return ClickSessionButton("TorsoButton");
        yield return Phase2Select(flow, LoadoutSlot.LeftLeg, LoadoutRules.LegDoubleJump);
        yield return Phase2Select(flow, LoadoutSlot.RightLeg, LoadoutRules.LegHover);
        yield return DeployAndFocus(flow);
        f = flow.ActiveFight; p = f.Player.GetComponent<Rigidbody2D>(); var b = f.Boss;
        Check("Death / Continue / Redeploy", flow.OfficerIndex == 2 && f.Player.CurrentHealth == 100
            && b.Phase == CyberpsychoBoss.BossPhase.Phase1 && b.GunEquipped && high.activeInHierarchy,
            "death -> Continue -> next officer -> Loadout -> deployment -> fresh P1 combat");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        Keys(Key.E); yield return Frames(2); Keys();
        bool counter = f.Player.GetComponent<PlayerCounterArm>().IsWindowOpen && !f.Player.GetComponent<PlayerTimeSlow>().IsActive;
        Keys(Key.Q); yield return Frames(2); Keys();
        bool slow = f.Player.GetComponent<PlayerTimeSlow>().IsActive;
        Check("E / Q Mapping", counter && slow, "E opens Counter only; Q activates prototype Time Slow; Shift already exercised");
        p.position = new Vector2(-10, -1.69f); p.linearVelocity = Vector2.zero; yield return Seconds(.15f);
        var movement = f.Player.GetComponent<PlayerMovement>();
        Keys(Key.Space); yield return Seconds(.3f); Keys(); yield return Seconds(.05f);
        Keys(Key.Space); yield return Frames(3);
        bool airJump = p.linearVelocity.y > 8 && movement.AirJumpsRemaining == 0;
        yield return Phase8Until(() => movement.IsHovering, "held Space descent hover", 3);
        bool hover = movement.IsHovering && movement.HoverRemaining < PlayerMovement.HoverDuration;
        Keys(); Check("Double Jump / Hover", airJump && hover, "second Space press produces upward impulse; holding Space then limits descent with finite hover resource");
        yield return Phase8Until(() => !f.Player.GetComponent<PlayerTimeSlow>().IsActive, "Time Slow ends", 4);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BurstRecovery);
        yield return Frames(2);
        bool predictedShot = b.PredictionVisible && b.PredictionCue.Contains("MARKED SHOT");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.MarkedLock);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.GhostMove);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SweepFire);
        Check("P1 / Predictive Marked Shot", b.ShotsFired >= 4 && predictedShot,
            "Burst, Marked Shot, Ghost Reposition and Suppressive Sweep execute; Cortex shows earlier Marked path");
        f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - 390, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        yield return Frames(2);
        bool predictedSlash = b.PredictionVisible && b.PredictionCue.Contains("GHOST SLASH");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashExecute);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutSecond);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.DropImpact);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.SlashWarning);
        Check("P1 to P2 / Three Attacks", b.BladeEquipped && !b.GunEquipped && b.CrosscutSlashesStarted >= 2
            && b.DropsStarted >= 1 && predictedSlash && b.GetComponent<SpriteRenderer>().color.a < 1,
            "gun discarded/blade drawn; readable camouflage; Ghost Slash, both Crosscut strikes, vertical Drop impact and normal-loop recovery");
        p.position = new Vector2(0, 2.12f); p.linearVelocity = Vector2.zero;
        f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - 200, f.Player.gameObject);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutSignal); yield return Frames(2);
        bool predictedBreak = b.PredictionVisible && b.PredictionCue.Contains("HIGH C");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutMark);
        bool marked = high.activeInHierarchy && b.transform.Find("ChargeWarning_TEST").GetComponent<SpriteRenderer>().enabled;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase10-blackout-mark.png");
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutFinalSlash);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.BlackoutRecovery);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutFirst);
        Check("BLACKOUT / Destruction / Continue", predictedBreak && marked && !high.activeInHierarchy
            && !high.GetComponent<Collider2D>().isActiveAndEnabled && b.DistortionRunsStarted == 2 && b.BlackoutsStarted == 1
            && p.position.y < 1 && flow.State == GameSessionFlow.FlowState.Combat,
            "early Cortex High C warning -> two runs -> normal mark -> final slash -> High C removed/player falls -> normal P2 combat resumes");
        f.BossHealth.TakeDamage(800, f.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        string archivePath = ((PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null)).FilePath;
        yield return ClickSessionButton("PoliceArchiveButton");
        string archiveText = GameObject.Find("Details").GetComponent<TMPro.TMP_Text>().text;
        bool archive = view.ArchiveOpen && archiveText.Contains("COMPLETED") && archiveText.Contains("UNLOCKED")
            && archiveText.Contains("No endings recorded.");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase10-archive-victory.png");
        yield return ClickSessionButton("PoliceArchiveButton");
        yield return ClickSessionButton("NewGameButton");
        Phase9UseArchive(archivePath);
        Check("Victory / Archive / Persistent Unlock", archive && File.ReadAllText(archivePath).Contains(PoliceArchive.TempFirstVictory)
            && PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
            && flow.OfficerIndex == 1 && flow.Draft.BodyModificationPercent == 0,
            "real victory resolver -> Result -> Archive completed/reward -> New Game -> fresh disk-profile reload retains achievement and Drone");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow); yield return Frames(3);
        v2ProtectedPlayer = null;
        yield return FixtureNextOfficer(flow);
        for (int i = 0; i < 3; i++) yield return ClickSessionButton("LeftArmButton");
        yield return ClickSessionButton("BrainButton"); // AI plus Drone: independent aim owners.
        yield return DeployAndFocus(flow);
        f = flow.ActiveFight; b = f.Boss;
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        var drone = f.Player.GetComponent<PlayerDetachedDroneArm>();
        int hp = f.BossHealth.CurrentHealth;
        AimAtWorld(f.Player.transform.position + Vector3.left * 20, false);
        yield return Phase8Until(() => drone.ShotsFired >= 2 && f.BossHealth.CurrentHealth < hp, "Drone autonomously damages Boss", 6);
        bool droneCombat = f.Deployment.HasDetachedDrone && drone.Unit != null && Phase9UnitCount() == 1
            && !f.Player.GetComponent<OfficerVisual>().Part(LoadoutSlot.LeftArm).enabled
            && f.Player.GetComponent<PlayerShooting>().ShotsFired == 0;
        f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - 390, f.Player.gameObject);
        yield return Phase3WaitPhase(b, CyberpsychoBoss.BossPhase.Phase2);
        yield return Phase3WaitAction(b, CyberpsychoBoss.AttackState.CrosscutFirst); yield return Frames(3);
        bool ai = Phase8Tracks(f) && f.Player.GetComponent<PlayerInputReader>().AimMode == AimControl.AI;
        Check("New Run / Drone / Targeting AI", droneCombat && ai && high.activeInHierarchy && !b.PredictionVisible,
            $"new run/loadout/equip/deploy; exactly one Drone; autonomous shots={drone.ShotsFired}; player shots=0; AI tracks moving P2 Boss despite opposing mouse; Cortex cleared; High C restored");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase10-drone-combat.png");
        v2ProtectedPlayer = null;
        Check("End-to-End Editor", flow.State == GameSessionFlow.FlowState.Combat && droneCombat && ai,
            "complete chain reached another run's autonomous Drone combat; no flow softlock");
        f.CancelCombat(); // Stop autonomous damage before the harness restores the normal Archive binding.
    }
}
#endif
