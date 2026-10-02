#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Batch 03a/03b presentation check only: Sniper muzzle length, Arm-Blade white line, old counter marker hidden.</summary>
public partial class TopDownSmokeTest
{
    // Longest live "ImpactLines" ray right now (muzzle rays start .035 from the origin).
    private static float LongestMuzzleRay(BossFight fight)
    {
        float longest = 0f;
        foreach (var line in fight.GetComponentsInChildren<LineRenderer>())
            if (line.transform.parent.name == "ImpactLines" && line.positionCount == 2)
                longest = Mathf.Max(longest, Vector3.Distance(line.GetPosition(0), line.GetPosition(1)));
        return longest;
    }

    private IEnumerator Polish03()
    {
        Record("Batch 03 presentation check: virtual input, isolated Archive. Not a gameplay regression.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // Launch run: Assault Rifle muzzle, then straight to a controlled defeat.
            yield return WeaponsPracticeStart(flow);
            var f = flow.ActiveFight;
            Mouse(.3f, .9f, true);
            float rifleRay = 0f;
            for (int i = 0; i < 8 && rifleRay <= 0f; i++) { yield return null; rifleRay = LongestMuzzleRay(f); }
            Mouse(.3f, .9f, false);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
            Keys(); yield return Frames(3);
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return DefeatToMenu(flow);

            // Sniper + Arm-Blade.
            yield return ClickSessionButton("StartMission");
            flow.TrySetDraftSlot(LoadoutSlot.Weapon, LoadoutRules.Sniper);
            flow.TrySetDraftSlot(LoadoutSlot.LeftArm, LoadoutRules.ArmBlade);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            f = flow.ActiveFight;
            var arm = f.Player.GetComponent<PlayerCounterArm>();
            Mouse(.3f, .9f, true);
            float sniperRay = 0f;
            for (int i = 0; i < 8 && sniperRay <= 0f; i++) { yield return null; sniperRay = LongestMuzzleRay(f); }
            Mouse(.3f, .9f, false);
            Check("Sniper muzzle rays are longer than the Assault Rifle's", Mathf.Abs(rifleRay - .165f) < .01f && Mathf.Abs(sniperRay - .215f) < .01f,
                $"rifle ray={rifleRay:0.000} sniper ray={sniperRay:0.000}");
            yield return Seconds(.3f);
            Keys(Key.E); yield return Frames(3); Keys();
            var transient = f.transform.Find("AttemptVfx_Transient");
            bool bladeLine = transient != null && transient.Find("ArmBladeLine") != null && transient.Find("BladeArc") != null;
            var marker = f.Player.transform.Find("CounterWindow_TEST");
            bool markerHidden = marker == null || !marker.GetComponent<SpriteRenderer>().enabled;
            Check("Arm-Blade swing draws the white line and arc; old counter marker stays hidden", arm.SlashesStarted == 1 && arm.IsWindowOpen
                && bladeLine && markerHidden, $"line+arc={bladeLine} markerHidden={markerHidden}");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/polish03-blade.png");
            yield return Seconds(.5f);
            Check("Swing visuals expire", f.transform.Find("AttemptVfx_Transient/ArmBladeLine") == null
                && f.GetComponentsInChildren<LineRenderer>().All(l => l.transform.parent.name != "BladeArc"), "no leftover blade lines");
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
