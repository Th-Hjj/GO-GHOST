#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Batch 05a look-check: Result, Main Menu and Archive once each, with screenshots. Isolated Archive.</summary>
public partial class TopDownSmokeTest
{
    private static string OverflowingTexts() => string.Join(", ", FindObjectsByType<TMP_Text>()
        .Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text) && (t.isTextOverflowing || t.isTextTruncated)).Select(t => t.name + "=" + t.text));

    private IEnumerator TerminalDefeat(GameSessionFlow flow)
    {
        yield return WeaponsPracticeStart(flow);
        Keys(Key.D);
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "door", 20);
        Keys(); yield return Frames(3);
        flow.ActiveFight.Player.IsInvulnerable = false;
        flow.ActiveFight.Player.TakeDamage(10000, flow.ActiveFight.Boss.gameObject);
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION FAILED", 3);
        yield return Frames(4);
    }

    private IEnumerator TerminalUi()
    {
        Record($"Batch 05a look-check at {Screen.width}x{Screen.height}. Screenshots: Logs/GO_GHOST_VS/terminal-*.png. Isolated Archive.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            yield return TerminalDefeat(flow);
            string overflow = OverflowingTexts();
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/terminal-result.png");
            yield return Frames(3);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(4);
            Check("Result: no overflowing text; CONTINUE returns to Main Menu once", overflow == "" && flow.State == GameSessionFlow.FlowState.MainMenu
                && PoliceArchive.Current.RunRecords.Count == 1, overflow == "" ? "clean" : overflow);
            overflow = OverflowingTexts();
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/terminal-main.png");
            yield return Frames(3);
            yield return ClickSessionButton("StartMission");
            bool upgradeOpened = flow.State == GameSessionFlow.FlowState.Loadout;
            yield return ClickSessionButton("BeginOperation");
            yield return TerminalDefeat(flow);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            Check("Main Menu: no overflowing text; START MISSION works", overflow == "" && upgradeOpened && PoliceArchive.Current.RunRecords.Count == 2, overflow == "" ? "clean" : overflow);
            yield return ClickSessionButton("PoliceArchive");
            yield return Frames(4);
            overflow = OverflowingTexts();
            var scroll = FindObjectsByType<ScrollRect>().FirstOrDefault();
            int cards = GameObject.Find("Records").transform.childCount;
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/terminal-archive-records.png");
            yield return Frames(3);
            float before = scroll != null ? scroll.verticalNormalizedPosition : -1f;
            if (scroll != null) scroll.verticalNormalizedPosition = 0f;
            yield return Frames(2);
            bool scrolled = scroll != null && (scroll.content.rect.height <= scroll.viewport.rect.height || scroll.verticalNormalizedPosition < before);
            yield return ClickSessionButton("ArchiveTab1");
            yield return Frames(3);
            overflow += OverflowingTexts();
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/terminal-archive-intel.png");
            yield return Frames(3);
            yield return ClickSessionButton("ArchiveTab2");
            yield return Frames(3);
            overflow += OverflowingTexts();
            bool tab2 = FindAnyObjectByType<MissionUI>().ArchiveTab == 2;
            yield return ClickSessionButton("ArchiveBack");
            yield return Frames(3);
            Check("Archive: two records, three tabs, scroll and BACK work, no overflowing text", cards == 2 && scrolled && tab2 && overflow == ""
                && flow.State == GameSessionFlow.FlowState.MainMenu, overflow == "" ? $"cards={cards}" : overflow);
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
