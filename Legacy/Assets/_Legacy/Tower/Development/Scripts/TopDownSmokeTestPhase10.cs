#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private IEnumerator Phase10Msp()
    {
        Record("NOT RUN: Phase 10 UI scenario retired by the full presentation reset. Use bare-gameplay.");
        yield break;
    }

    private IEnumerator BareGameplay()
    {
        Record("Bare gameplay smoke: virtual input/physics; controlled death and Boss HP for flow, isolated Archive. Not balance or natural victory.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        int sampledFrames = 0, uiFrames = 0;
        UnityEditor.EditorApplication.CallbackFunction audit = () => { sampledFrames++; if (BarePresentationCount() != 0) uiFrames++; };
        UnityEditor.EditorApplication.update += audit;
        try
        {
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "automatic launch", 3);
            Check("Immediate gameplay", flow.OfficerIndex == 1 && flow.Deployment.BodyModificationPercent == 0 && !flow.CanContinue,
                "new mission automatically deploys 0% officer; no UI or confirmation gate");
            FocusV2(flow); yield return Frames(4);
            var f = flow.ActiveFight;
            var rb = f.Player.GetComponent<Rigidbody2D>();
            float x = rb.position.x;
            Keys(Key.D); yield return Seconds(.2f); Keys();
            bool moved = rb.position.x > x + .5f;
            AimAtWorld(f.Boss.transform.position, true); yield return Seconds(.3f); MouseTo(.5f, .5f);
            bool fired = f.Player.GetComponent<PlayerShooting>().ShotsFired > 0;
            Keys(Key.Space); yield return Frames(2); Keys();
            bool jumped = rb.linearVelocity.y > 1;
            yield return Phase8Until(() => f.Player.GetComponent<PlayerMovement>().IsGrounded, "jump landing", 4);
            Keys(Key.LeftShift); yield return Frames(2); Keys();
            bool dodged = f.Player.GetComponent<PlayerDodge>().IsDodging;
            Check("Movement / fire / jump / dodge", moved && fired && jumped && dodged, $"move={moved}, fire={fired}, jump={jumped}, dodge={dodged}");
            yield return Seconds(.5f);
            Check("Live P1", f.Boss.ShotsFired > 0 && f.BossHealth.CurrentHealth < f.BossHealth.MaxHealth,
                $"Boss shots={f.Boss.ShotsFired}, real projectile Boss HP={f.BossHealth.CurrentHealth}");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/bare-p1.png");

            var previous = f;
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.OfficerIndex == 2, "next officer", 3);
            if (flow.CanDeploy) flow.RequestDeploy(); // Existing developer API if manual restart was selected.
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "restart", 3);
            f = flow.ActiveFight;
            Check("Death / reset", f != previous && f.Player.CurrentHealth == f.Player.MaxHealth && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth
                && f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase1 && FindObjectsByType<BossFight>().Length == 1,
                "new actor pair, full HP, fresh P1, no death or handoff screen");
            FocusV2(flow); yield return Frames(4);
            v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
            f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - 390, f.Player.gameObject);
            yield return Phase8Until(() => f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2 && f.Boss.SlashesStarted > 0, "P2 combat", 10);
            Check("Boss phase / blade combat", f.Boss.BladeEquipped && !f.Boss.GunEquipped && flow.State == GameSessionFlow.FlowState.Combat,
                "combat phase transition preserved without announcement UI");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/bare-p2.png");
            yield return Seconds(.3f);
            f.BossHealth.TakeDamage(10000, f.Player.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "underlying victory", 3);
            Check("Victory without presentation", flow.Victory != null && f.Result == BossFight.Outcome.Won && !f.Player.GetComponent<PlayerInputReader>().GameplayInputEnabled
                && PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory), "outcome, winning snapshot and persistent reward retained; no result screen");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/bare-completed.png");
            yield return Frames(3);
            Check("Existing New Game API", flow.RequestNewGame(), "developer reset API remains usable without replacement UI");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "new game restart", 3);
            Check("Fresh New Game", flow.OfficerIndex == 1 && flow.Deployment.BodyModificationPercent == 0 && flow.Victory == null,
                "fresh human combat starts automatically");
            Check("Zero UI throughout", sampledFrames > 0 && uiFrames == 0 && BarePresentationCount() == 0,
                $"{sampledFrames} editor frames sampled; {uiFrames} frames containing any scene UI/text, including inactive objects");
            flow.ActiveFight.CancelCombat(); // Stop combat before normal Archive bindings return.
        }
        finally
        {
            UnityEditor.EditorApplication.update -= audit;
            v2ProtectedPlayer = null;
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }

    private static int BarePresentationCount()
    {
        return Resources.FindObjectsOfTypeAll<Component>().Count(c => c != null && c.gameObject.scene.IsValid()
            && (c is Canvas || c is TMPro.TMP_Text || c is UnityEngine.UI.Text || c is TextMesh
                || c is UnityEngine.UIElements.UIDocument || c is UnityEngine.UI.Graphic || c is UnityEngine.EventSystems.EventSystem));
    }
}
#endif
