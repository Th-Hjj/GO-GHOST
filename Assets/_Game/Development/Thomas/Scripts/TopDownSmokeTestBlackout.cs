#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Concentrated Boss integration smoke in the real arena: phase transition, camouflage tell, new BLACKOUT, platform break,
/// cancellation and restore. Phase health is forced (controlled trigger); during BLACKOUT the officer is a plain 0% Human,
/// not invulnerable, steered only by virtual A/D reacting to the visible red lines. Isolated Archive.
/// </summary>
public partial class TopDownSmokeTest
{
    private float bossTransitionSeconds, bossTellSeconds, bossCamoAlpha;
    private bool bossGunHidden, bossBladeDrawn, bossQuietTransition, bossRedBar;
    private bool blackoutDodgeOnly;
    private int walkRightFrames, walkLeftFrames, walkIdleFrames, walkLinesSeen;

    private IEnumerator BlackoutDodgeOnly() { blackoutDodgeOnly = true; yield return Blackout(); blackoutDodgeOnly = false; }

    // Walk away from the nearest visible red line at the officer's own height. Uses only what is drawn on screen.
    private IEnumerator WalkOutOfVisibleLines(BossFight f, System.Func<bool> done)
    {
        var body = f.Player.GetComponent<Collider2D>();
        float side = 0f; // chosen once per warning and kept, like a person committing to a direction
        while (!done())
        {
            Vector2 c = body.bounds.center;
            float push = 0f, nearest = float.MaxValue;
            foreach (var line in f.GetComponentsInChildren<LineRenderer>())
            {
                if (!line.enabled || line.positionCount != 2 || !line.transform.parent.name.StartsWith("BlackoutLine_")) continue;
                if (line.startWidth < .1f) continue; // judge by the band itself, not its two thin edge lines (they made the walker jitter in place)
                Vector3 a = line.GetPosition(0), b = line.GetPosition(1);
                if (Mathf.Abs(b.y - a.y) < .05f) continue; // the horizontal final band: stay on the ground below it
                float dx = c.x - (a.x + (c.y - a.y) * (b.x - a.x) / (b.y - a.y));
                walkLinesSeen++;
                if (Mathf.Abs(dx) < nearest) { nearest = Mathf.Abs(dx); push = dx >= 0 ? 1f : -1f; }
            }
            // A 1.55-tall officer against a 25 degree, .6-wide band needs about 2.7 units of horizontal clearance from its centre line.
            if (nearest < 3.6f)
            {
                // The hidden Boss still has a solid body: when the line runs through the officer, leave on the side away
                // from where the Boss was last seen; otherwise keep going the way that already leads out.
                if (side == 0f) side = nearest < .5f ? (c.x >= f.Boss.transform.position.x ? 1f : -1f) : push;
                if (side > 0 && c.x > 13.8f) side = -1f; else if (side < 0 && c.x < -13.8f) side = 1f;
                push = side;
                Keys(push > 0 ? Key.D : Key.A);
                if (push > 0) walkRightFrames++; else walkLeftFrames++;
            }
            else { Keys(); walkIdleFrames++; side = 0f; }
            yield return null;
        }
        Keys();
    }

    // Door -> Phase 1 spent -> the Boss's own transition -> Phase 2 at 200 HP -> BLACKOUT begins. Records what it sees.
    private IEnumerator ReachBlackout(GameSessionFlow flow)
    {
        Keys(Key.D);
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
        Keys(); yield return Frames(3);
        var f = flow.ActiveFight;
        var body = f.Boss.GetComponent<SpriteRenderer>();
        f.Player.IsInvulnerable = true; // only until BLACKOUT starts
        yield return WeaponsWait(() => f.Boss.State != CyberpsychoBoss.AttackState.Arrival, 4);
        f.BossHealth.TakeDamage(100000, f.Player.gameObject);
        yield return WeaponsWait(() => f.Boss.Phase == CyberpsychoBoss.BossPhase.PhaseTransition, 3);
        float started = Time.time;
        int shots = f.Boss.ShotsFired;
        Vector2 at = f.Boss.transform.position;
        bossGunHidden = bossBladeDrawn = false;
        while (f.Boss.Phase == CyberpsychoBoss.BossPhase.PhaseTransition && Time.time - started < 6f)
        {
            if (!f.Boss.GunEquipped && !f.Boss.BladeEquipped) bossGunHidden = true;
            if (f.Boss.BladeEquipped) bossBladeDrawn = true;
            yield return null;
        }
        bossTransitionSeconds = Time.time - started;
        bossQuietTransition = f.Boss.ShotsFired == shots && Mathf.Abs(f.Boss.transform.position.x - at.x) < .05f;
        yield return WeaponsWait(() => f.BossHealth.PhaseIndex == 2, 2);
        yield return Frames(2);
        var bossBar = GameObject.Find("BossHealth").transform.Find("Health").GetComponent<Image>();
        bossRedBar = f.BossHealth.CurrentHealth == 600 && bossBar.gameObject.activeSelf && bossBar.color == Color.red;
        // Normal Phase 2: concealed reposition, then the full visible tell before the slash.
        bossCamoAlpha = 1f; bossTellSeconds = 0f;
        float tellStart = -1f, limit = Time.time + 6f;
        while (Time.time < limit && bossTellSeconds <= 0f)
        {
            if (f.Boss.State == CyberpsychoBoss.AttackState.CamoReposition) bossCamoAlpha = Mathf.Min(bossCamoAlpha, body.color.a);
            if (f.Boss.State == CyberpsychoBoss.AttackState.SlashWarning && tellStart < 0f) tellStart = Time.time;
            if (f.Boss.State == CyberpsychoBoss.AttackState.SlashExecute && tellStart >= 0f) bossTellSeconds = Time.time - tellStart;
            yield return null;
        }
        f.BossHealth.TakeDamage(400, f.Player.gameObject); // 200 of 600: under the 35% threshold
        yield return WeaponsWait(() => f.Boss.BlackoutUsed, 14);
        f.Player.IsInvulnerable = false;
    }

    private IEnumerator Blackout()
    {
        Record("Boss integration smoke: forced phase health, 0% Human with virtual A/D only during BLACKOUT, isolated Archive.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        var high = new[] { "HighPlatformC", "HighPlatformD", "HighPlatformE" }.Select(n => GameObject.Find(n)).ToArray();
        var permanent = new[] { "Wall_Bottom", "LowPlatformA", "LowPlatformB" }.Select(n => GameObject.Find(n)).ToArray();
        try
        {
            // ---------- Run 1: reactive 0% Human through the whole sequence ----------
            yield return WeaponsPracticeStart(flow);
            yield return ReachBlackout(flow);
            var f = flow.ActiveFight;
            Check("Transition is about three seconds, quiet, gun away then blade drawn", bossTransitionSeconds > 2.9f && bossTransitionSeconds < 3.3f && bossGunHidden && bossBladeDrawn && bossQuietTransition,
                $"{bossTransitionSeconds:0.00}s gunHidden={bossGunHidden} bladeDrawn={bossBladeDrawn} noShotsOrMove={bossQuietTransition}");
            Check("Phase 2 starts with a full red bar", bossRedBar && f.BossHealth.PhaseIndex == 2, "600 HP, red");
            Check("Normal Phase 2: faded reposition, then a .55 s visible tell before the slash", bossCamoAlpha < .5f && bossTellSeconds > .5f && bossTellSeconds < .65f,
                $"camo alpha={bossCamoAlpha:0.00} tell={bossTellSeconds:0.00}s");
            Check("BLACKOUT starts in Phase 2 with the real arena bound", f.Boss.BlackoutUsed && f.Boss.BlackoutsStarted == 1 && !f.Boss.BlackoutPlacementFailed
                && high.All(p => p.activeInHierarchy) && flow.Deployment.BodyModificationPercent == 0 && !flow.Deployment.HasAirDash, "three high platforms intact");
            int hpBefore = f.Player.CurrentHealth;
            bool brokeEarly = false, sawRedLines = false;
            int cutsAtBreak = -1; float clockAtBreak = -1f;
            int cutsLogged = 0, hpLogged = hpBefore;
            walkRightFrames = walkLeftFrames = walkIdleFrames = walkLinesSeen = 0;
            var watch = StartCoroutine(WalkOutOfVisibleLines(f, () => f.Boss.State == CyberpsychoBoss.AttackState.BlackoutRecovery || f.Player.IsDead));
            float limit = Time.realtimeSinceStartup + 14f;
            while (f.Boss.State != CyberpsychoBoss.AttackState.BlackoutRecovery && !f.Player.IsDead && Time.realtimeSinceStartup < limit)
            {
                if (f.Boss.BlackoutCutsExecuted != cutsLogged || f.Player.CurrentHealth != hpLogged)
                {
                    cutsLogged = f.Boss.BlackoutCutsExecuted; hpLogged = f.Player.CurrentHealth;
                    Record($"  t={f.Boss.BlackoutElapsed:0.00} cuts={cutsLogged} hp={hpLogged} player=({f.Player.transform.position.x:0.00},{f.Player.transform.position.y:0.00}) vx={f.Player.GetComponent<Rigidbody2D>().linearVelocity.x:0.0}");
                }
                if (!sawRedLines) sawRedLines = f.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled && l.transform.parent.name.StartsWith("BlackoutLine_") && l.startColor.r > .9f && l.startColor.g < .3f);
                if (cutsAtBreak < 0 && high.Any(p => !p.activeInHierarchy)) { cutsAtBreak = f.Boss.BlackoutCutsExecuted; clockAtBreak = f.Boss.BlackoutElapsed; }
                if (f.Boss.BlackoutCutsExecuted < 7 && high.Any(p => !p.activeInHierarchy)) brokeEarly = true;
                if (f.Boss.BlackoutElapsed > 4.4f && f.Boss.BlackoutElapsed < 4.5f) ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/blackout-curtain.png");
                yield return null;
            }
            StopCoroutine(watch); Keys();
            Record($"  walker frames: right={walkRightFrames} left={walkLeftFrames} idle={walkIdleFrames} linesSeen={walkLinesSeen} sawRed={sawRedLines}");
            yield return WeaponsWait(() => high.All(p => !p.activeInHierarchy), 2);
            if (cutsAtBreak < 0 && high.Any(p => !p.activeInHierarchy)) { cutsAtBreak = f.Boss.BlackoutCutsExecuted; clockAtBreak = f.Boss.BlackoutElapsed; }
            int lost = hpBefore - f.Player.CurrentHealth;
            Check("Red lines are drawn; a 0% Human avoids the whole sequence by walking (no dash, no Air Dash, not invulnerable)", sawRedLines && lost == 0 && !f.Player.IsDead
                && f.Boss.BlackoutCutsExecuted == 7 && !f.Boss.BlackoutPlacementFailed, $"HP lost={lost}, cuts executed={f.Boss.BlackoutCutsExecuted}, placementFailed={f.Boss.BlackoutPlacementFailed}");
            Check("Three high platforms break only after the final cut; ground and low platforms stay", !brokeEarly && cutsAtBreak == 7 && high.All(p => !p.activeInHierarchy)
                && permanent.All(p => p.activeInHierarchy), $"break at {clockAtBreak:0.00}s with {cutsAtBreak} cuts done");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/blackout-after-break.png");
            if (blackoutDodgeOnly) yield break;
            f.Player.IsInvulnerable = true;
            int slashes = f.Boss.SlashesStarted;
            yield return WeaponsWait(() => f.Boss.SlashesStarted > slashes, 8);
            var bossBody = f.Boss.GetComponent<SpriteRenderer>();
            Check("After BLACKOUT the Boss is visible again and continues Phase 2", f.Boss.SlashesStarted > slashes && f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2
                && !f.BossHealth.IsDead && FindObjectsByType<LineRenderer>().All(l => !l.enabled || !l.transform.parent.name.StartsWith("BlackoutLine_")),
                $"body alpha={bossBody.color.a:0.00}, slashes={f.Boss.SlashesStarted}");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "P2 death", 3);
            yield return Frames(2);
            Check("Player death in Phase 2 -> Failure Result", StrictTexts().Contains("MISSION FAILED") && PoliceArchive.Current.RunRecords.Count == 1, "one record");
            yield return ClickSessionButton("ResultContinue");

            // ---------- Run 2: standing still gets hit; player death mid-sequence cancels the rest ----------
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            bool restored = high.All(p => p.activeInHierarchy);
            yield return ReachBlackout(flow);
            f = flow.ActiveFight;
            hpBefore = f.Player.CurrentHealth;
            yield return WeaponsWait(() => f.Boss.BlackoutCutsExecuted >= 2 || f.Player.IsDead, 6);
            yield return Seconds(.2f);
            int standingLoss = hpBefore - f.Player.CurrentHealth;
            Check("Next deployment restores the platforms; a standing officer is hit by the aimed cuts", restored && standingLoss >= 32, $"HP lost standing still through two cuts={standingLoss}");
            yield return WeaponsWait(() => f.Boss.BlackoutElapsed > 3.6f || f.Player.IsDead, 4); // curtain warning showing
            if (!f.Player.IsDead) f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "death during BLACKOUT", 3);
            yield return Seconds(5.5f);
            Check("Player death mid-BLACKOUT: no later cut or break, no leftover lines", high.All(p => p.activeInHierarchy) && flow.State == GameSessionFlow.FlowState.Result
                && FindObjectsByType<LineRenderer>().All(l => !l.enabled || !l.transform.parent.name.StartsWith("Blackout"))
                && PoliceArchive.Current.RunRecords.Count == 2, "platforms still intact 5.5 s later");
            yield return ClickSessionButton("ResultContinue");

            // ---------- Run 3: Boss death mid-sequence -> Success Result, nothing breaks afterwards ----------
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            yield return ReachBlackout(flow);
            f = flow.ActiveFight;
            f.Player.IsInvulnerable = true;
            yield return WeaponsWait(() => f.Boss.BlackoutElapsed > 3.6f, 5);
            f.BossHealth.TakeDamage(100000, f.Player.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "Boss death during BLACKOUT", 3);
            yield return Seconds(5.5f);
            Check("Boss death mid-BLACKOUT: Success Result, no later break, no leftover lines", StrictTexts().Contains("MISSION SUCCESS") && high.All(p => p.activeInHierarchy)
                && FindObjectsByType<LineRenderer>().All(l => !l.enabled || !l.transform.parent.name.StartsWith("Blackout"))
                && PoliceArchive.Current.RunRecords.Count == 3 && PoliceArchive.Current.LatestRun.Success, "Phase 2 at 0 is the victory");
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(2);
            bool ending = flow.State == GameSessionFlow.FlowState.Ending;
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(2);
            Check("Victory still goes Result -> Ending -> Main Menu", ending && flow.State == GameSessionFlow.FlowState.MainMenu, "0% -> Human Ending");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            foreach (var p in high) if (p != null) p.SetActive(true);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
