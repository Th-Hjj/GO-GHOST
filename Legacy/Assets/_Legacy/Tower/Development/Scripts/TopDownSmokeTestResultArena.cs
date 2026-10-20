#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Failure Result page, TEST health values, five platforms (0% route + restore). Isolated Archive, controlled outcomes.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator ResultArena()
    {
        Record("Result / health / arena smoke: virtual input, controlled outcomes, isolated Archive.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var uiSfx = flow.GetComponent<SfxSet>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        string[] high = { "HighPlatformC", "HighPlatformD", "HighPlatformE" };
        var platforms = high.Select(n => GameObject.Find(n)).ToArray();
        try
        {
            // Health sources: first practice, door refill, Boss.
            yield return WeaponsPracticeStart(flow);
            var f = flow.ActiveFight;
            var move = f.Player.GetComponent<PlayerMovement>();
            bool practiceFull = f.Player.MaxHealth == 150 && f.Player.CurrentHealth == 150;
            f.Player.TakeDamage(40, null); // controlled loss in practice
            int afterLoss = f.Player.CurrentHealth;
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
            Keys(); yield return Frames(3);
            Check("Player 150 HP in practice and refilled to 150 at the door", practiceFull && afterLoss == 110 && f.Player.CurrentHealth == 150, $"practice loss -> {afterLoss}, after door {f.Player.CurrentHealth}");
            Check("Boss Phase 1 has 600 HP; five platforms present", f.BossHealth.MaxHealth == 600 && f.BossHealth.CurrentHealth == 600
                && platforms.All(p => p != null && p.activeInHierarchy) && GameObject.Find("LowPlatformA") != null && GameObject.Find("LowPlatformB") != null,
                "High C/D/E + Low A/B");

            // 0% route: ground -> Low A -> High D -> walk off the edge. Observation protection only; no forced movement.
            f.Player.IsInvulnerable = true;
            Keys(Key.D);
            yield return WeaponsWait(() => f.Player.transform.position.x > -5.6f, 4);
            Keys(); yield return Frames(3);
            yield return Tap(Key.Space);
            yield return WeaponsWait(() => move.IsGrounded && f.Player.transform.position.y > 0f, 2);
            Vector2 onLow = f.Player.transform.position;
            yield return Frames(3);
            Keys(Key.A, Key.Space); yield return Frames(2); Keys(Key.A);
            yield return WeaponsWait(() => move.IsGrounded && f.Player.transform.position.y > 1.6f, 2);
            Keys(); yield return Frames(3);
            Vector2 onHigh = f.Player.transform.position;
            bool stoodOnHigh = move.IsGrounded && onHigh.y > 1.6f;
            Keys(Key.A);
            yield return WeaponsWait(() => move.IsGrounded && f.Player.transform.position.y < -1.5f, 3);
            Keys(); yield return Frames(3);
            Vector2 back = f.Player.transform.position;
            Check("0% officer reaches High D from Low A and walks off to the ground", onLow.y > 0f && onLow.y < 1f && stoodOnHigh && onHigh.x < -7.3f && onHigh.x > -10.7f
                && back.y < -1.5f && move.IsGrounded, $"low {onLow} -> high {onHigh} -> ground {back}");

            // Failure Result.
            int defeatCues = uiSfx.Requested(Sfx.Defeat);
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION FAILED", 3);
            yield return Seconds(1.2f);
            var texts = StrictTexts();
            Check("Defeat stays on Mission Result with the four statistics", flow.State == GameSessionFlow.FlowState.Result && texts.Contains("MISSION FAILED")
                && new[] { "TIME", "BOSS HP", "PLAYER HP", "ACCURACY", "CONTINUE", "600", "0" }.All(texts.Contains) && texts.Length == 10
                && PoliceArchive.Current.RunRecords.Count == 1 && flow.ActiveFight != null && uiSfx.Requested(Sfx.Defeat) == defeatCues + 1,
                string.Join(" | ", texts));
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            Check("CONTINUE returns to Main Menu without a second record", flow.State == GameSessionFlow.FlowState.MainMenu && PoliceArchive.Current.RunRecords.Count == 1
                && !PoliceArchive.Current.LatestRun.Success && PoliceArchive.Current.LatestRun.BossHealth == 600 && flow.ActiveFight == null
                && StrictText("Value_STATUS") == "MISSION FAILED", "one frozen record");

            // Broken platforms are restored by the next deployment (break simulated by deactivating the three objects).
            foreach (var p in platforms) p.SetActive(false);
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            f = flow.ActiveFight;
            Check("Next deployment restores the three high platforms and full health", platforms.All(p => p.activeInHierarchy) && f.Player.CurrentHealth == 150, "C/D/E active");
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "second door", 20);
            Keys(); yield return Frames(3);
            yield return ControlledVictory(f);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION SUCCESS", 3);
            yield return Frames(3);
            bool success = StrictTexts().Contains("MISSION SUCCESS") && PoliceArchive.Current.RunRecords.Count == 2;
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            bool ending = flow.State == GameSessionFlow.FlowState.Ending;
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(3);
            Check("Victory: Result -> Ending -> Main Menu, one more record", success && ending && flow.State == GameSessionFlow.FlowState.MainMenu
                && PoliceArchive.Current.RunRecords.Count == 2 && PoliceArchive.Current.LatestRun.Success, "two records total");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            foreach (var p in platforms) if (p != null) p.SetActive(true);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
