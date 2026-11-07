#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Rules")]
    public static void RunFinalBossRules() => Start("final-boss-rules", false);

    // Final Boss stage 4: fixed Boss rules ahead of his AI, shown in the right sidebar.
    static IEnumerator FinalBossRuleList()
    {
        Record("Final Boss rules: developer Wave 3, one player rule, normal dialogue/countdown. The Boss AI's own decisions are suspended so each rule is observed alone; placement, cooldown resets and raised player HP are controlled. No balance claim.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        game.EditLoadout(d => d.TryAddRule((int)ControlOperation.Aim, new ConditionalRule { Condition = 13, Action = "Q01" }));
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f); yield return Frames(4);
        var actor = game.Bosses.Single(); var axe = actor.Axe; var rules = axe.Rules;
        game.Player.SetRuntimeHealth(5000, 5000);
        var aiClock = typeof(FinalBossAxe).GetField("nextDecisionAt", PrivateInstance);
        void Suspend() { axe.Cancel(); aiClock.SetValue(axe, float.MaxValue); System.Array.Clear(Private<float[]>(axe, "readyAt"), 0, 5); }
        Suspend();
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
        {
            SetAlphaGameSize(size.x, size.y); yield return Until(() => Screen.width == size.x, 2f); yield return Frames(6);
            var left = TrainingRect(CardsRect("BattleRules")); var right = TrainingRect(CardsRect("BossRules")); var view = Camera.main.pixelRect;
            Check(size.x + " player rules left, Boss rules right, camera between", Mathf.Abs(left.xMin) <= 1.5f && Mathf.Abs(right.xMax - Screen.width) <= 1.5f
                && Mathf.Abs(view.xMin - left.xMax) <= 2f && Mathf.Abs(view.xMax - right.xMin) <= 2f,
                $"left={left.xMin:F0}..{left.xMax:F0} camera={view.xMin:F0}..{view.xMax:F0} boss={right.xMin:F0}..{right.xMax:F0}");
            ScreenCapture.CaptureScreenshot(Output + "final-boss-rules-" + size.x + ".png"); yield return Frames(3);
        }
        int[] executed = rules.Rows.Select(r => r.Executed).ToArray();
        bool Ran(int row) => rules.Rows[row].Executed > executed[row];
        // Boss perception is delayed by design: hold him idle until his public facts reach the new placement.
        IEnumerator Settle(float playerX, float bossX)
        {
            AlphaPlace(playerX, bossX);
            // Placement lands on the next physics step; then wait for the delayed facts to reach it.
            for (int i = 0; i < 4; i++) { Suspend(); yield return null; }
            float until = Time.time + 1.5f;
            while (Time.time < until && Mathf.Abs(actor.Perception.Facts.Position.x - playerX) > .3f) { Suspend(); yield return null; }
            for (int i = 0; i < 3; i++) { Suspend(); yield return null; }
            // Snapshot in the same frame as the last reset, before the Boss's next tick can act.
            Suspend();
            executed = rules.Rows.Select(r => r.Executed).ToArray();
            Record($"SETTLE facts={actor.Perception.Facts.Position} conf={actor.Perception.Facts.Confidence:F2} player={game.Player.transform.position} boss={actor.Position} grounded={actor.Motor.Grounded} matched={string.Join(",", rules.Rows.Select(r => r.Matched ? 1 : 0))} current={axe.Current} sealed={rules.Sealed}");
        }
        string State() => $"current={axe.Current} matched={string.Join(",", rules.Rows.Select(r => r.Matched ? 1 : 0))} executed={string.Join(",", rules.Rows.Select(r => r.Executed))} grounded={actor.Motor.Grounded} conf={actor.Perception.Facts.Confidence:F2}";

        // Far -> Boomerang (rule 5).
        yield return Settle(-8, 3);
        yield return Until(() => Ran(4), 1.5f);
        Check("Far matches and rule 5 throws the Boomerang", Ran(4) && axe.Current == FinalBossAxe.Move.Boomerang, State());
        yield return Seconds(.1f); ScreenCapture.CaptureScreenshot(Output + "final-boss-rules-far.png");
        yield return Until(() => axe.Current == FinalBossAxe.Move.None, 4f);

        // Close -> Spin (rule 3).
        yield return Settle(0, 3);
        yield return Until(() => Ran(2), 1.5f);
        Check("Close matches and rule 3 spins", Ran(2) && axe.Current == FinalBossAxe.Move.Spin, State());
        yield return Until(() => axe.Current == FinalBossAxe.Move.None, 4f);

        // Incoming Attack (Ranged) -> Jump, then Landed -> Chop, Took Damage -> Throw & Recall.
        yield return Settle(-3, 3);
        Check("Mid distance leaves both distance rules unmatched", !rules.Rows[2].Matched && !rules.Rows[4].Matched, State());
        mousePosition = Camera.main.WorldToScreenPoint(actor.Position); mouseHeld = true;
        yield return Until(() => Ran(0), 2f);
        yield return Until(() => !actor.Motor.Grounded, .5f);
        Check("Incoming player bullet matches and rule 1 jumps", Ran(0) && !actor.Motor.Grounded, State());
        mouseHeld = false;
        yield return Until(() => Ran(1), 2.5f);
        Check("Landing triggers rule 2 Chop", Ran(1), State());
        yield return Until(() => axe.Current == FinalBossAxe.Move.None, 4f);

        // Took Damage -> Throw & Recall (rule 4), from a direct hit while idle and grounded.
        yield return Settle(-3, 3);
        actor.Health.TakeDamage(10, game.Player.gameObject);
        yield return Until(() => Ran(3), 1f);
        Check("Taking damage while free triggers rule 4 Throw & Recall", Ran(3) && axe.Current == FinalBossAxe.Move.Throw, State());
        yield return Until(() => axe.Current == FinalBossAxe.Move.None, 4f);

        game.Player.IsInvulnerable = true;
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return CompleteFinalBossTransition(); yield return Frames(4);
        var bossPanel = CardsUI().GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == "BossRules");
        Check("Phase two seals the Boss rules, removes both sidebars and widens the camera", rules.Sealed && bossPanel == null
            && rules.Rows.All(r => !r.Matched) && Mathf.Abs(Camera.main.pixelRect.width - Screen.width) <= 1f && Camera.main.pixelRect.xMin <= 1f);
        ScreenCapture.CaptureScreenshot(Output + "final-boss-rules-phase-two.png"); yield return Frames(3);
        game.MainMenu();
        Record("NOT TESTED: rule priority against unsuspended AI timing, invasions (stage 5), scripted transition (stage 7), balance.");
    }
}
#endif
