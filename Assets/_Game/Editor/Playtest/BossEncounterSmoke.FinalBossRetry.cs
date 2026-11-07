#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Phase 2 Retry")]
    public static void RunFinalBossRetry() => Start("final-boss-retry", false);

    // Final Boss stage 9: phase-two defeat offers a direct phase-two retry or a full restart.
    static IEnumerator FinalBossPhaseTwoRetry()
    {
        Record("Final Boss phase-two retry: developer Wave 3 with two player rules, full transition once. Deaths and pool depletion are controlled; UI buttons are clicked through their callbacks.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        game.EditLoadout(d =>
        {
            d.TryAddRule((int)ControlOperation.Aim, new ConditionalRule { Condition = 13, Action = "Q01" });
            d.TryAddRule((int)ControlOperation.Fire, new ConditionalRule { Condition = 6, Action = "F03" });
        });
        string equipped = JsonUtility.ToJson(game.DraftLoadout);
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        game.Player.TakeDamage(37, null); yield return Frames(2);
        int phaseOneEnd = game.Player.CurrentHealth;
        game.Player.IsInvulnerable = true;
        game.Bosses.Single().Health.TakeDamage(100000, game.Player.gameObject);
        yield return CompleteFinalBossTransition();
        game.Player.IsInvulnerable = false;
        game.Player.TakeDamage(100000, null);
        yield return Until(() => game.Phase == DuelPhase.Failed, 1.5f); yield return Frames(2);
        bool Has(string name) => CardsUI().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == name);
        Check("Phase-two defeat offers Retry Phase 2 and Configuration", game.CanRetryFinalBossPhaseTwo && Has("RetryPhaseTwo") && Has("Reconfigure")
            && !CardsUI().GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Retry"));
        ScreenCapture.CaptureScreenshot(Output + "final-boss-retry-defeat.png"); yield return Frames(3);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            ClickConfig("RetryPhaseTwo"); yield return Frames(3);
            var actor = game.Bosses.Single(); var axe = actor.Axe;
            Check($"Retry {attempt} skips the intro and transition into phase two", game.Phase == DuelPhase.Prepare && !game.FinalBossCutscene
                && actor.Health.PhaseIndex == 2 && actor.Health.CurrentHealth == 1000 && axe.PhaseTwo && axe.Unarmed && axe.Boxer != null);
            Check($"Retry {attempt} keeps the phase-one end health, the antique revolver and no rules", game.Player.CurrentHealth == phaseOneEnd
                && game.Player.GetComponent<OfficerVisual>().AntiqueRevolver
                && Enumerable.Range(0, 5).Sum(op => game.Rules.RuleCount((ControlOperation)op)) == 0 && JsonUtility.ToJson(game.DraftLoadout) == equipped,
                $"hp={game.Player.CurrentHealth} expected={phaseOneEnd}");
            yield return Frames(3);
            Check($"Retry {attempt} starts with moving platforms, full width and no sidebars",
                LayoutSupports().Skip(1).All(c => c.GetComponent<PlatformOscillator>() != null) && Mathf.Abs(Camera.main.pixelRect.width - Screen.width) <= 1
                && !CardsUI().GetComponentsInChildren<RectTransform>().Any(r => r.name == "BattleRules" || r.name == "BossRules"));
            if (attempt == 1) { ScreenCapture.CaptureScreenshot(Output + "final-boss-retry-countdown.png"); yield return Frames(3); }
            yield return Until(() => game.Phase == DuelPhase.Fight, 4f);
            int thrown = axe.Boxer.Started.Sum();
            yield return Until(() => axe.Boxer.Started.Sum() > thrown, 6f);
            Check($"Retry {attempt}: the White Boxer fights after the countdown", game.Phase == DuelPhase.Fight && axe.Boxer.Started.Sum() > thrown);
            game.Player.TakeDamage(100000, null);
            yield return Until(() => game.Phase == DuelPhase.Failed, 1.5f); yield return Frames(2);
            Check($"Retry {attempt} defeat still offers Retry Phase 2", game.CanRetryFinalBossPhaseTwo && Has("RetryPhaseTwo"));
        }

        ClickConfig("Reconfigure"); yield return Frames(3);
        Check("Configuration returns with the equipped rules", game.Phase == DuelPhase.Configure && JsonUtility.ToJson(game.DraftLoadout) == equipped);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        Check("A restart from Configuration replays the intro in phase one", game.Phase == DuelPhase.FinalBoss
            && game.Bosses.Single().Health.PhaseIndex == 1 && !game.FinalBossPhaseTwoReached);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        game.Player.TakeDamage(100000, null);
        yield return Until(() => game.Phase == DuelPhase.Failed, 1.5f); yield return Frames(2);
        Check("Phase-one defeat keeps the plain Retry", !game.CanRetryFinalBossPhaseTwo && !Has("RetryPhaseTwo")
            && CardsUI().GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Retry"));

        game.MainMenu(); yield return Frames(2);
        game.ConfigurePlaytestWave(3); game.QueueFinalBossPhaseTwo(); yield return Frames(3);
        game.StartFight(); yield return Frames(3);
        Check("The developer Wave 3 Phase 2 entry starts phase two at full health", game.Phase == DuelPhase.Prepare
            && game.Bosses.Single().Health.PhaseIndex == 2 && game.Player.CurrentHealth == game.Player.MaxHealth);
        game.MainMenu();
        Record("NOT TESTED: hand play of retries, the F8 developer wave list (unchanged), standalone builds.");
    }
}
#endif
