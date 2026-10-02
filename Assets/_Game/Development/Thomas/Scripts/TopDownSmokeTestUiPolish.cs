#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Selected-state look only: screenshots plus three colour checks. Isolated Archive.</summary>
public partial class TopDownSmokeTest
{
    private static bool Inverted(string buttonName)
    {
        var go = GameObject.Find(buttonName);
        return go.GetComponent<Image>().color == Color.white && go.GetComponentInChildren<TMP_Text>().color == Color.black;
    }

    private IEnumerator UiPolish()
    {
        Record("UI selected-state check: screenshots in Logs/GO_GHOST_VS/ui-*.png. Isolated Archive.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            yield return WeaponsPracticeStart(flow);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
            Keys(); yield return Frames(3);
            flow.ActiveFight.Player.IsInvulnerable = false;
            flow.ActiveFight.Player.TakeDamage(10000, flow.ActiveFight.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.MainMenu, "defeat", 3);
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_LEFT_LEG");
            bool humanFirst = Inverted("Choice_Human");
            yield return ClickSessionButton("Choice_" + LoadoutRules.LegAirDash);
            yield return Frames(2);
            Check("Selected leg is filled; equipped choice is inverted", humanFirst && Inverted("Choice_" + LoadoutRules.LegAirDash) && !Inverted("Choice_Human")
                && GameObject.Find("Region_LEFT_LEG").GetComponent<MissionBodyRegion>().Selected
                && !GameObject.Find("Region_RIGHT_LEG").GetComponent<MissionBodyRegion>().Selected, "Left Leg / Air Dash");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/ui-upgrade-leg.png");
            yield return Frames(3);
            yield return ClickSessionButton("Region_WEAPON");
            yield return ClickSessionButton("Choice_" + LoadoutRules.Shotgun);
            yield return Frames(2);
            Check("Weapon region and chosen gun are inverted", Inverted("Region_WEAPON") && Inverted("Choice_" + LoadoutRules.Shotgun)
                && !Inverted("Choice_" + LoadoutRules.AssaultRifle) && !GameObject.Find("Region_LEFT_LEG").GetComponent<MissionBodyRegion>().Selected, "Weapon / Shotgun");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/ui-upgrade-weapon.png");
            yield return Frames(3);
            yield return ClickSessionButton("UpgradeBack");
            yield return ClickSessionButton("PoliceArchive");
            yield return Frames(2);
            bool firstTab = Inverted("ArchiveTab0") && !Inverted("ArchiveTab1");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/ui-archive-records.png");
            yield return Frames(3);
            yield return ClickSessionButton("ArchiveTab2");
            yield return Frames(2);
            Check("Open Archive tab is inverted", firstTab && Inverted("ArchiveTab2") && !Inverted("ArchiveTab0"), "tab state");
            yield return ClickSessionButton("ArchiveBack");
            yield return ClickSessionButton("StartMission");
            yield return Frames(2);
            Check("A new Upgrade visit starts with nothing selected", !Inverted("Region_WEAPON")
                && FindObjectsByType<MissionBodyRegion>().All(r => !r.Selected), "cleared");
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
