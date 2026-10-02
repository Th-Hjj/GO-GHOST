#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Two independent Boss health phases, transition immunity, bar colours. Controlled damage, isolated Archive.</summary>
public partial class TopDownSmokeTest
{
    private static readonly Color DeepBlueTest = new Color(.10f, .28f, .90f);
    private static Image BarFill(string bar) => GameObject.Find(bar).transform.Find("Health").GetComponent<Image>();
    private static bool SameColour(Color a, Color b) => Mathf.Abs(a.r - b.r) < .01f && Mathf.Abs(a.g - b.g) < .01f && Mathf.Abs(a.b - b.b) < .01f;

    private IEnumerator BossPhases()
    {
        Record("Boss phases smoke: controlled damage and outcomes, fixture positions, isolated Archive. TEST values 600 / 600.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // ---------- Run 1 (0% build): colours, then death in Phase 1 ----------
            yield return WeaponsPracticeStart(flow);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
            Keys(); yield return Frames(3);
            var f = flow.ActiveFight;
            var playerBar = BarFill("PlayerHealth");
            var bossBar = BarFill("BossHealth");
            f.Player.TakeDamage(30, f.Boss.gameObject);
            yield return Frames(2);
            Check("0% build: white player bar; losing HP shortens it without recolouring", SameColour(playerBar.color, Color.white)
                && Mathf.Abs(playerBar.transform.localScale.x - 120f / 150f) < .01f && SameColour(bossBar.color, Color.white)
                && f.BossHealth.MaxHealth == 600 && f.BossHealth.PhaseIndex == 1, $"fill={playerBar.transform.localScale.x:0.00}");
            f.BossHealth.TakeDamage(100, f.Player.gameObject);
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "P1 death", 3);
            yield return Frames(2);
            Check("8a Death in Phase 1: Failure Result with Phase 1 HP", StrictTexts().Contains("MISSION FAILED") && StrictTexts().Contains("500")
                && PoliceArchive.Current.LatestRun.BossHealth == 500 && !PoliceArchive.Current.LatestRun.Success, "BOSS HP 500");
            yield return ClickSessionButton("ResultContinue");

            // ---------- Run 2 (50% build, Assault Rifle): transition, immunity, Phase 2, BLACKOUT, death in Phase 2 ----------
            yield return ClickSessionButton("StartMission");
            flow.TrySetDraftSlot(LoadoutSlot.LeftArm, LoadoutRules.ArmBlade);
            flow.TrySetDraftSlot(LoadoutSlot.Brain, LoadoutRules.BrainAI);
            flow.TrySetDraftSlot(LoadoutSlot.Weapon, LoadoutRules.AssaultRifle);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door 2", 20);
            Keys(); yield return Frames(3);
            f = flow.ActiveFight;
            var gun = f.Player.GetComponent<PlayerShooting>();
            var arm = f.Player.GetComponent<PlayerCounterArm>();
            playerBar = BarFill("PlayerHealth"); bossBar = BarFill("BossHealth");
            Color half = Color.Lerp(Color.white, DeepBlueTest, .5f);
            Check("50% build with a Assault Rifle: player bar is the white/blue midpoint", flow.Deployment.BodyModificationPercent == 50 && SameColour(playerBar.color, half), playerBar.color.ToString());
            f.Player.IsInvulnerable = true; // observation protection only
            float combatStart = Time.unscaledTime;
            f.BossHealth.TakeDamage(100000, f.Player.gameObject);
            yield return Frames(2);
            Check("1 Phase 1 HP at 0 is not a victory", flow.State == GameSessionFlow.FlowState.Combat && f.Result == BossFight.Outcome.Fighting && !f.BossHealth.IsDead
                && f.BossHealth.AwaitingSecondPhase && f.BossHealth.CurrentHealth == 0 && !bossBar.gameObject.activeSelf && PoliceArchive.Current.RunRecords.Count == 1,
                "still fighting, bar empty");
            float depletedAt = Time.time;
            yield return WeaponsWait(() => f.Boss.Phase == CyberpsychoBoss.BossPhase.PhaseTransition, 2);
            Check("2 Boss enters its transition", f.Boss.Phase == CyberpsychoBoss.BossPhase.PhaseTransition, $"after {Time.time - depletedAt:0.00}s");
            // Immunity: real rifle fire with the Targeting AI still locked on the Boss, then a real blade swing next to it.
            var reader = f.Player.GetComponent<PlayerInputReader>();
            Vector2 toBoss = (Vector2)f.BossHealth.GetComponent<Collider2D>().bounds.center - (Vector2)f.Player.transform.position;
            bool aiStillTargets = Vector2.Angle(reader.AimDirection, toBoss) < 5f;
            f.PlacePlayer(new Vector2(f.BossHealth.GetComponent<Collider2D>().bounds.center.x - 1.7f + 9f, 0f));
            yield return Frames(3);
            int hitsBefore = gun.ShotsHit;
            yield return WeaponClick(.5f, .5f);
            Keys(Key.E); yield return Frames(3); Keys();
            yield return Seconds(.5f);
            Check("3 Transition rejects gun and blade damage; the Boss stays targetable", f.BossHealth.CurrentHealth == 0 && f.BossHealth.AwaitingSecondPhase && aiStillTargets
                && gun.TriggerPulls >= 1 && arm.SlashesStarted >= 1 && FindObjectsByType<Projectile>().All(p => p.OwnerLayer != f.Boss.gameObject.layer),
                $"pulls={gun.TriggerPulls} pellet contacts={gun.ShotsHit - hitsBefore} swings={arm.SlashesStarted} blade contacts={arm.BladeHits}, HP still 0");
            yield return WeaponsWait(() => f.BossHealth.PhaseIndex == 2, 6);
            yield return Frames(2);
            float transition = Time.time - depletedAt;
            Check("4 About three seconds later Phase 2 starts with full health", f.BossHealth.PhaseIndex == 2 && f.BossHealth.CurrentHealth == 600 && f.BossHealth.MaxHealth == 600
                && f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2 && transition > 2.9f && transition < 4.5f && !f.Boss.BlackoutUsed, $"{transition:0.00}s after Phase 1 ended");
            Check("5 Boss bar is full again and red; player bar unchanged", bossBar.gameObject.activeSelf && Mathf.Abs(bossBar.transform.localScale.x - 1f) < .01f
                && SameColour(bossBar.color, Color.red) && SameColour(playerBar.color, half), "red Boss fill");
            f.BossHealth.TakeDamage(400, f.Player.gameObject); // 200 <= 35% of 600
            yield return WeaponsWait(() => f.Boss.BlackoutUsed, 10);
            Check("6 Phase 2 at 35% triggers BLACKOUT", f.Boss.BlackoutUsed && f.BossHealth.CurrentHealth == 200, $"blackouts={f.Boss.BlackoutsStarted}");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "P2 death", 3);
            yield return Frames(2);
            Check("8b Death in Phase 2: Failure Result with Phase 2 HP, one fight's time", StrictTexts().Contains("MISSION FAILED") && StrictTexts().Contains("200")
                && PoliceArchive.Current.RunRecords.Count == 2 && PoliceArchive.Current.LatestRun.BossHealth == 200
                && PoliceArchive.Current.LatestRun.CombatDuration > transition && PoliceArchive.Current.LatestRun.CombatDuration < Time.unscaledTime - combatStart + 1f,
                $"TIME {PoliceArchive.Current.LatestRun.CombatDuration:0.0}s covers both phases");
            yield return ClickSessionButton("ResultContinue");

            // ---------- Run 3 (100% build): deep blue bar, victory only when Phase 2 reaches 0 ----------
            yield return ClickSessionButton("StartMission");
            flow.TrySetDraftSlot(LoadoutSlot.LeftArm, LoadoutRules.ArmBlade);
            flow.TrySetDraftSlot(LoadoutSlot.RightArm, LoadoutRules.ArmFireRate);
            flow.TrySetDraftSlot(LoadoutSlot.LeftLeg, LoadoutRules.LegDoubleJump);
            flow.TrySetDraftSlot(LoadoutSlot.RightLeg, LoadoutRules.LegHover);
            flow.TrySetDraftSlot(LoadoutSlot.Torso, LoadoutRules.TorsoSlow);
            flow.TrySetDraftSlot(LoadoutSlot.Brain, LoadoutRules.BrainAI);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door 3", 20);
            Keys(); yield return Frames(3);
            f = flow.ActiveFight;
            f.Player.IsInvulnerable = true;
            Check("100% build: deep blue player bar, white Boss bar", flow.Deployment.BodyModificationPercent == 100 && SameColour(BarFill("PlayerHealth").color, DeepBlueTest)
                && SameColour(BarFill("BossHealth").color, Color.white), BarFill("PlayerHealth").color.ToString());
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/boss-phases-p1.png");
            yield return Frames(2);
            yield return ControlledVictory(f);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "victory", 4);
            yield return Frames(2);
            Check("7 Victory only when Phase 2 reaches 0", StrictTexts().Contains("MISSION SUCCESS") && PoliceArchive.Current.RunRecords.Count == 3
                && PoliceArchive.Current.LatestRun.Success && PoliceArchive.Current.LatestRun.BossHealth == 0 && f.BossHealth.PhaseIndex == 2
                && PoliceArchive.Current.LatestRun.CombatDuration > 2.9f, $"TIME {PoliceArchive.Current.LatestRun.CombatDuration:0.0}s");
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(2);
            bool aiEnding = flow.State == GameSessionFlow.FlowState.Ending && StrictTexts().Contains("AI ENDING");
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(2);
            Check("  Victory flow unchanged: Result -> AI Ending -> Main Menu", aiEnding && flow.State == GameSessionFlow.FlowState.MainMenu, "100% build");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
