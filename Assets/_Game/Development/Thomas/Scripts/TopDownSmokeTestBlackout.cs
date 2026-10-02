#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// New BLACKOUT in the real arena. Phase health is forced (controlled trigger); during BLACKOUT the officer is a plain
/// 0% Human, not invulnerable, steered only by virtual A/D reacting to the visible red lines. Isolated Archive.
/// </summary>
public partial class TopDownSmokeTest
{
    // Walk away from the nearest visible red line at the officer's own height. Uses only what is drawn on screen.
    private IEnumerator WalkOutOfVisibleLines(BossFight f, System.Func<bool> done)
    {
        var body = f.Player.GetComponent<Collider2D>();
        while (!done())
        {
            Vector2 c = body.bounds.center;
            float push = 0f, nearest = float.MaxValue;
            foreach (var line in f.GetComponentsInChildren<LineRenderer>())
            {
                if (!line.enabled || line.positionCount != 2 || !line.transform.parent.name.StartsWith("BlackoutLine_")) continue;
                Vector3 a = line.GetPosition(0), b = line.GetPosition(1);
                if (Mathf.Abs(b.y - a.y) < .05f) continue; // the horizontal final band: stay on the ground below it
                float dx = c.x - (a.x + (c.y - a.y) * (b.x - a.x) / (b.y - a.y));
                if (Mathf.Abs(dx) < nearest) { nearest = Mathf.Abs(dx); push = dx >= 0 ? 1f : -1f; }
            }
            if (nearest < 2.2f)
            {
                if (push > 0 && c.x > 13.8f) push = -1f; else if (push < 0 && c.x < -13.8f) push = 1f;
                Keys(push > 0 ? Key.D : Key.A);
            }
            else Keys();
            yield return null;
        }
        Keys();
    }

    private IEnumerator ReachBlackout(GameSessionFlow flow)
    {
        Keys(Key.D);
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
        Keys(); yield return Frames(3);
        var f = flow.ActiveFight;
        f.Player.IsInvulnerable = true; // only until BLACKOUT starts
        f.BossHealth.TakeDamage(100000, f.Player.gameObject);
        yield return WeaponsWait(() => f.BossHealth.PhaseIndex == 2, 8);
        f.BossHealth.TakeDamage(400, f.Player.gameObject); // 200 of 600: under the 35% threshold
        yield return WeaponsWait(() => f.Boss.BlackoutUsed, 14);
        f.Player.IsInvulnerable = false;
    }

    private IEnumerator Blackout()
    {
        Record("BLACKOUT smoke: forced phase health, 0% Human with virtual A/D only during BLACKOUT, isolated Archive.");
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
            Check("BLACKOUT starts in Phase 2 with the arena bound", f.Boss.BlackoutUsed && f.Boss.BlackoutsStarted == 1 && f.BossHealth.PhaseIndex == 2
                && high.All(p => p.activeInHierarchy) && flow.Deployment.BodyModificationPercent == 0 && !flow.Deployment.HasAirDash, "three high platforms intact");
            int hpBefore = f.Player.CurrentHealth;
            bool brokeEarly = false;
            int cutsAtBreak = -1; float clockAtBreak = -1f;
            var watch = StartCoroutine(WalkOutOfVisibleLines(f, () => f.Boss.State == CyberpsychoBoss.AttackState.BlackoutRecovery || f.Player.IsDead));
            float limit = Time.realtimeSinceStartup + 14f;
            while (f.Boss.State != CyberpsychoBoss.AttackState.BlackoutRecovery && !f.Player.IsDead && Time.realtimeSinceStartup < limit)
            {
                if (cutsAtBreak < 0 && high.Any(p => !p.activeInHierarchy)) { cutsAtBreak = f.Boss.BlackoutCutsExecuted; clockAtBreak = f.Boss.BlackoutElapsed; }
                if (f.Boss.BlackoutCutsExecuted < 7 && high.Any(p => !p.activeInHierarchy)) brokeEarly = true;
                yield return null;
            }
            StopCoroutine(watch); Keys();
            yield return WeaponsWait(() => high.All(p => !p.activeInHierarchy), 2);
            if (cutsAtBreak < 0 && high.Any(p => !p.activeInHierarchy)) { cutsAtBreak = f.Boss.BlackoutCutsExecuted; clockAtBreak = f.Boss.BlackoutElapsed; }
            int lost = hpBefore - f.Player.CurrentHealth;
            Check("0% Human avoids the whole sequence by walking (no dash, no Air Dash, not invulnerable)", lost == 0 && !f.Player.IsDead && f.Boss.BlackoutCutsExecuted == 7,
                $"HP lost={lost}, cuts executed={f.Boss.BlackoutCutsExecuted}");
            Check("Three high platforms break only after the final cut; ground and low platforms stay", !brokeEarly && cutsAtBreak == 7 && high.All(p => !p.activeInHierarchy)
                && permanent.All(p => p.activeInHierarchy), $"break at {clockAtBreak:0.00}s with {cutsAtBreak} cuts done");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/blackout-after-break.png");
            f.Player.IsInvulnerable = true;
            int slashes = f.Boss.SlashesStarted;
            yield return WeaponsWait(() => f.Boss.SlashesStarted > slashes, 8);
            var bossBody = f.Boss.GetComponent<SpriteRenderer>();
            Check("After BLACKOUT the Boss is visible again and continues Phase 2", f.Boss.SlashesStarted > slashes && f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2
                && bossBody.color.a > .3f && !f.BossHealth.IsDead && FindObjectsByType<LineRenderer>().All(l => !l.enabled || !l.transform.parent.name.StartsWith("BlackoutLine_")),
                $"body alpha={bossBody.color.a:0.00}, slashes={f.Boss.SlashesStarted}");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return DefeatToMenu(flow);
            int records = PoliceArchive.Current.RunRecords.Count;

            // ---------- Run 2: standing still gets hit; death mid-sequence cancels the rest ----------
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
            yield return Seconds(6f);
            Check("Death mid-sequence: no later cut or break, no leftover lines, one record", high.All(p => p.activeInHierarchy) && flow.State == GameSessionFlow.FlowState.Result
                && FindObjectsByType<LineRenderer>().All(l => !l.enabled || !l.transform.parent.name.StartsWith("Blackout"))
                && PoliceArchive.Current.RunRecords.Count == records + 1, "platforms still intact 6 s later");
            yield return ClickSessionButton("ResultContinue");
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
