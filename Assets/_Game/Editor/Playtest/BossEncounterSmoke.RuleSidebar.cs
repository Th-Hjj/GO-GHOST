#if UNITY_EDITOR
using System.Collections;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Rule Sidebar Left")]
    public static void RunRuleSidebarLeft() => Start("rule-sidebar-left", false);

    // Final Boss stage 2: the player's rules sit on the left and the camera fills the remaining width.
    static IEnumerator RuleSidebarLeft()
    {
        Record("Rule sidebar left: developer Wave 1 configuration, three rules through EditLoadout, normal Start Fight. Layout and capture only.");
        game.ConfigurePlaytestWave(1); yield return Frames(4);
        game.EditLoadout(d =>
        {
            d.TryAddRule((int)ControlOperation.Aim, new ConditionalRule { Condition = 3, Action = "Q01" });
            d.TryAddRule((int)ControlOperation.Fire, new ConditionalRule { Condition = 6, Action = "F03" });
            d.TryAddRule((int)ControlOperation.Aim, new ConditionalRule { Condition = 13, Action = "Q01" });
        });
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.Fight, 4.5f);
        StopPolicies();
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) })
        {
            SetAlphaGameSize(size.x, size.y); yield return Until(() => Screen.width == size.x && Screen.height == size.y, 2f); yield return Frames(6);
            var sidebar = TrainingRect(CardsRect("BattleRules"));
            var player = TrainingRect(Private<UnityEngine.UI.Image>(CardsUI(), "playerFill").rectTransform);
            var view = Camera.main.pixelRect;
            Check(size.x + " sidebar is flush with the left edge", Mathf.Abs(sidebar.xMin) <= 1.5f && sidebar.xMax < Screen.width * .5f,
                $"sidebar={sidebar.xMin:F0}..{sidebar.xMax:F0} screen={Screen.width}");
            Check(size.x + " camera starts at the sidebar and reaches the right edge", Mathf.Abs(view.xMin - sidebar.xMax) <= 2f
                && Mathf.Abs(view.xMax - Screen.width) <= 1f, $"camera={view.xMin:F0}..{view.xMax:F0}");
            Check(size.x + " health HUD is centered on the camera view", Mathf.Abs(player.center.x - view.center.x) <= 2f,
                $"player={player.center.x:F0} camera={view.center.x:F0}");
            ScreenCapture.CaptureScreenshot(Output + "rule-sidebar-left-" + size.x + ".png"); yield return Frames(3);
        }
        Record("NOT TESTED: Final Boss right sidebar (stage 4) and live mouse aim on the moved viewport.");
    }
}
#endif
