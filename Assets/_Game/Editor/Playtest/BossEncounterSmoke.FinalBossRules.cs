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
        Record("Real player/Boss rule controllers, ordinary axe AI and session-only F8 tuning. Controlled placement and health.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        // A historical Jump variant must have today's single action and keep jumping on a sustained condition.
        var legacyJump = new ConditionalRule { Condition=4, Action="J03", Jump=JumpOption.Repeat };
        Check("Old Jump data displays one parameterless Jump", ConditionalCatalog.IsAllowed((int)ControlOperation.Jump,legacyJump)
            && ConditionalCatalog.RuleActionLabel(legacyJump)=="Jump" && ConditionalCatalog.ActionOptions("J01").Length==0);
        game.EditLoadout(d=>d.TryAddRule(ControlOperation.Jump,new ConditionalRule { Condition=4, Action="J01" }));
        game.StartConfiguredEncounter(); yield return Frames(3);
        game.Player.IsInvulnerable = true;
        game.Player.SetRuntimeHealth(200,40);
        yield return Until(()=>game.Rules.AcceptedJumps>=3,5f);
        Check("One continuous low-health condition starts three physical jumps", game.Rules.AcceptedJumps>=3);
        game.Player.SetRuntimeHealth(200,200);
        int jumps=game.Rules.AcceptedJumps;
        yield return Seconds(1.4f);
        Check("Losing the condition allows landing and stops further jumps", game.Rules.AcceptedJumps==jumps
            && game.Player.GetComponent<PlayerMovement>().IsGrounded);
        game.BackFromTraining();
        game.EditLoadout(d=>d.Operations[(int)ControlOperation.Jump].Rules.Clear());
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
        game.Player.IsInvulnerable = true;
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
        {
            SetAlphaGameSize(size.x, size.y); yield return Until(() => Screen.width == size.x, 2f); yield return Frames(6);
            var left = TrainingRect(CardsRect("BattleRules")); var right = TrainingRect(CardsRect("BossRules")); var view = Camera.main.pixelRect;
            Check(size.x + " player rules left, Boss rules right, camera between", Mathf.Abs(left.xMin) <= 1.5f && Mathf.Abs(right.xMax - Screen.width) <= 1.5f
                && Mathf.Abs(view.xMin - left.xMax) <= 2f && Mathf.Abs(view.xMax - right.xMin) <= 2f,
                $"left={left.xMin:F0}..{left.xMax:F0} camera={view.xMin:F0}..{view.xMax:F0} boss={right.xMin:F0}..{right.xMax:F0}");
            ScreenCapture.CaptureScreenshot(Output + "final-boss-rules-" + size.x + ".png"); yield return Frames(3);
        }
        Check("Boss rows are valid player actions", rules.OrderedRows.Count == 2 && rules.OrderedRows.All(entry =>
            ConditionalCatalog.IsAllowed(entry.Op, rules.RuleAt((ControlOperation)entry.Op,entry.Row))));
        Check("Both panels leave at least 65% width for combat", Camera.main.rect.width >= .65f);
        AlphaPlace(-8, 5);
        yield return Until(() => rules.Ownership.MovementOwned, 2f);
        Check("Far matches the shared Move toward Enemy response", rules.Ownership.MovementOwned && rules.Ownership.Move < 0);
        Suspend();
        AlphaPlace(-4, 4);
        mousePosition = Camera.main.WorldToScreenPoint(actor.Position); mouseHeld = true;
        yield return Until(() => rules.AcceptedJumps > 0, 3f);
        mouseHeld = false;
        Check("A real player bullet triggers the shared Jump rule", rules.AcceptedJumps > 0);
        aiClock.SetValue(axe, 0f);
        yield return Until(() => axe.Current != FinalBossAxe.Move.None, 3f);
        Check("The combat AI initiates its own axe attack", axe.Current != FinalBossAxe.Move.None);

        checkingPause = true;
        game.ToggleDeveloperTools();
        ClickConfig("Final Boss");
        ClickConfig("Dev_BossWindup_Plus");
        Check("F8 edits Chop wind-up", Mathf.Approximately(game.GetBossMoveTuning(false,1).Windup,.47f));
        ClickConfig("75% HP");
        Check("F8 edits the actual phase-one pool", actor.Health.CurrentHealth == 750);
        game.ToggleDeveloperTools(); checkingPause = false;
        yield return Until(() => axe.Invading, 4f);
        Check("75% uses the real invasion trigger", axe.Invading);
        checkingPause = true; game.ToggleDeveloperTools();
        ClickConfig("Waves"); ClickConfig("Dev_FinalPhase2");
        checkingPause = false;
        Check("F8 enters the phase-two countdown without dialogue", game.Phase == DuelPhase.Prepare
            && game.Bosses.Single().Axe.PhaseTwo && game.Bosses.Single().Health.CurrentHealth == 1000);
        yield return Until(() => game.Phase == DuelPhase.Fight,4f);
        game.Player.IsInvulnerable = true;
        checkingPause = true; game.ToggleDeveloperTools(); ClickConfig("Tuning");
        ClickConfig("Boxer"); ClickConfig("Dev_BossDamage_Plus");
        game.SetDeveloperHealth(2,460);
        Check("F8 adjusts Jab damage and phase-two current HP", game.GetBossMoveTuning(true,1).Damage == 13
            && game.Bosses.Single().Health.CurrentHealth == 460);
        var boxer = game.Bosses.Single().Axe.Boxer;
        int jabs = boxer.Started[(int)FinalBossBoxer.Punch.Jab];
        ClickConfig("Try selected move"); checkingPause = false;
        yield return Until(() => boxer.Started[(int)FinalBossBoxer.Punch.Jab] > jabs, 4f);
        Check("F8 resumes into the selected real punch", !game.DeveloperToolsOpen && !game.Paused
            && boxer.Started[(int)FinalBossBoxer.Punch.Jab] > jabs);
        checkingPause = true; game.ToggleDeveloperTools();
        ClickConfig("Restore defaults");
        Check("Restore resets both movesets", game.GetBossMoveTuning(true,1).Damage == 12
            && Mathf.Approximately(game.GetBossMoveTuning(false,1).Windup,.42f));
        ClickConfig("Waves"); ClickConfig("Dev_FinalPhase1"); checkingPause = false;
        Check("F8 enters a fresh phase-one countdown",game.Phase==DuelPhase.Prepare && !game.Bosses.Single().Axe.PhaseTwo);
        game.MainMenu();
        Record("Controlled flow and parameter checks; hand-play balance not tested.");
    }
}
#endif
