#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Two focused checks only: 05b Upgrade page (seven regions, selected / equipped look, BEGIN OPERATION, BACK) and
/// 04c BLACKOUT body collision (walk through the hidden Boss; pair restored at reveal, on death and next deployment).
/// Isolated Archive, forced phase health, fixture positions.
/// </summary>
public partial class TopDownSmokeTest
{
    private static bool ChoiceActive(string name)
    {
        var colors = GameObject.Find(name).GetComponent<Button>().colors;
        return colors.normalColor.b > .35f; // 05b/05a active fill (.075,.26,.43) versus idle (.025,.065,.11)
    }

    private bool upgradeLookOnly;
    private IEnumerator UpgradeLookOnly() { upgradeLookOnly = true; yield return Increments06(); upgradeLookOnly = false; }

    private IEnumerator Increments06()
    {
        Record($"05b Upgrade page + 04c BLACKOUT collision at {Screen.width}x{Screen.height}. Isolated Archive.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var ui = FindAnyObjectByType<MissionUI>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // ---------------- 05b: Upgrade page ----------------
            yield return TerminalDefeat(flow);
            yield return ClickSessionButton("ResultContinue");
            yield return ClickSessionButton("StartMission");
            yield return Frames(3);
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/upgrade-default.png");
            yield return Frames(3);
            string[] regions = { "Region_HEAD", "Region_TORSO", "Region_LEFT_ARM", "Region_RIGHT_ARM", "Region_LEFT_LEG", "Region_RIGHT_LEG", "Region_WEAPON" };
            bool allSelect = true;
            for (int i = 0; i < regions.Length; i++)
            {
                yield return ClickSessionButton(regions[i]);
                allSelect &= ui.SelectedRegion == i && GameObject.Find("UpgradeChoices").activeInHierarchy;
                if (i < 6) allSelect &= GameObject.Find(regions[i]).GetComponent<MissionBodyRegion>().Selected;
            }
            Check("Seven regions each select once with a real pointer click", allSelect && FindObjectsByType<MissionBodyRegion>().Count(r => r.Selected) == 0, "HEAD..WEAPON");
            yield return ClickSessionButton("Region_LEFT_ARM");
            yield return ClickSessionButton("Choice_" + LoadoutRules.ArmBlade);
            yield return ClickSessionButton("Region_TORSO");
            var leftArm = GameObject.Find("Region_LEFT_ARM").GetComponent<MissionBodyRegion>();
            bool keptOutline = leftArm.Equipped && !leftArm.Selected && GameObject.Find("Region_TORSO").GetComponent<MissionBodyRegion>().Selected
                && !GameObject.Find("Region_TORSO").GetComponent<MissionBodyRegion>().Equipped;
            yield return Frames(2);
            // Two frames after the list was rebuilt: no choice button may still be showing the default white tint.
            bool noWhiteFlash = GameObject.Find("UpgradeChoices").GetComponentsInChildren<Button>().All(c => c.targetGraphic.canvasRenderer.GetColor().r < .5f);
            Check("Rebuilt choice buttons start dark (no white flash)", noWhiteFlash, "tint applied immediately");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/upgrade-equipped.png");
            yield return Frames(3);
            if (upgradeLookOnly) yield break;
            yield return ClickSessionButton("Region_LEFT_ARM");
            bool reselect = ChoiceActive("Choice_" + LoadoutRules.ArmBlade) && !ChoiceActive("Choice_Human");
            string overflow = OverflowingTexts();
            yield return ClickSessionButton("Choice_Human");
            yield return ClickSessionButton("Region_HEAD");
            Check("Equipped part keeps its outline; reselect shows the equipped choice; Human removes it", keptOutline && reselect && !leftArm.Equipped
                && flow.Draft.BodyModificationPercent == 0 && overflow == "", overflow == "" ? "Arm-Blade on / off" : overflow);
            yield return ClickSessionButton("Region_LEFT_LEG");
            yield return ClickSessionButton("Choice_" + LoadoutRules.LegHover);
            yield return ClickSessionButton("Region_RIGHT_LEG");
            bool noDuplicate = !ChoiceLabels().Contains("Hover") && ChoiceLabels().Contains("Double Jump");
            yield return ClickSessionButton("UpgradeBack");
            yield return Frames(2);
            bool back = flow.State == GameSessionFlow.FlowState.MainMenu;
            yield return ClickSessionButton("StartMission");
            yield return Frames(2);
            bool fresh = flow.Draft.BodyModificationPercent == 0 && FindObjectsByType<MissionBodyRegion>().All(r => !r.Selected && !r.Equipped);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            Check("Leg duplicate rule kept; BACK and BEGIN OPERATION each fire once; practice text is not restyled", noDuplicate && back && fresh
                && flow.State == GameSessionFlow.FlowState.Practice && GameObject.Find("Controls").GetComponent<TMP_Text>().color == Color.white
                && GameObject.Find("Ammo").GetComponent<TMP_Text>().color == Color.white, "flow intact");

            // ---------------- 04c: BLACKOUT body collision ----------------
            yield return ReachBlackout(flow);
            var f = flow.ActiveFight;
            var playerBody = f.Player.GetComponent<Collider2D>();
            var bossBody = f.Boss.GetComponent<Collider2D>();
            f.Player.IsInvulnerable = true; // movement check only
            float bossX = bossBody.bounds.center.x;
            float side = bossX > 0f ? -1f : 1f; // start on the side with room, walk through to the other
            f.PlacePlayer(new Vector2(bossX + side * 1.6f + 9f, 0f));
            yield return Frames(3);
            bool ignoredDuring = Physics2D.GetIgnoreCollision(playerBody, bossBody);
            Keys(side < 0 ? Key.D : Key.A);
            yield return Seconds(.6f);
            Keys(); yield return Frames(2);
            float past = (f.Player.transform.position.x - bossX) * -side;
            Check("Hidden Boss body no longer blocks: the officer walks straight through", ignoredDuring && past > 1f && bossBody.enabled && bossBody.gameObject.activeInHierarchy,
                $"ended {past:0.00} units past the Boss centre; Boss collider still enabled");
            f.Player.IsInvulnerable = false;
            int hp = f.Player.CurrentHealth;
            int cuts = f.Boss.BlackoutCutsExecuted;
            yield return WeaponsWait(() => f.Boss.BlackoutCutsExecuted > cuts + 1 || f.Player.CurrentHealth < hp, 5);
            yield return Seconds(.2f);
            Check("A red cut still damages a standing officer", f.Player.CurrentHealth < hp, $"HP {hp} -> {f.Player.CurrentHealth}");
            f.Player.IsInvulnerable = true;
            yield return WeaponsWait(() => f.Boss.State == CyberpsychoBoss.AttackState.BlackoutRecovery, 10);
            yield return Seconds(.3f);
            bool restoredAtReveal = f.Boss.State == CyberpsychoBoss.AttackState.BlackoutRecovery && !Physics2D.GetIgnoreCollision(playerBody, bossBody);
            yield return WeaponsWait(() => f.Boss.State < CyberpsychoBoss.AttackState.BlackoutSignal || f.Boss.State > CyberpsychoBoss.AttackState.BlackoutRecovery, 4);
            Check("Body collision is back at the reveal and after BLACKOUT", restoredAtReveal && !Physics2D.GetIgnoreCollision(playerBody, bossBody), "pair restored");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return DefeatToMenu(flow);

            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            yield return ReachBlackout(flow);
            f = flow.ActiveFight;
            playerBody = f.Player.GetComponent<Collider2D>(); bossBody = f.Boss.GetComponent<Collider2D>();
            yield return WeaponsWait(() => f.Boss.BlackoutElapsed > 1f, 3);
            bool ignoredMid = Physics2D.GetIgnoreCollision(playerBody, bossBody);
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "death during BLACKOUT", 3);
            yield return Frames(3);
            bool restoredOnDeath = !Physics2D.GetIgnoreCollision(playerBody, bossBody);
            yield return ClickSessionButton("ResultContinue");
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "next door", 20);
            Keys(); yield return Frames(3);
            f = flow.ActiveFight;
            Check("Death mid-BLACKOUT restores the pair; the next deployment starts with normal collision", ignoredMid && restoredOnDeath
                && !Physics2D.GetIgnoreCollision(f.Player.GetComponent<Collider2D>(), f.Boss.GetComponent<Collider2D>()), "ignored only while hidden");
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
