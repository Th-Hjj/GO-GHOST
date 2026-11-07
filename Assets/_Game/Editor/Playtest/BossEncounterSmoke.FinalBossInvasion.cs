#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using TMPro;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Invasion")]
    public static void RunFinalBossInvasion() => Start("final-boss-invasion", false);

    // Final Boss stage 5: three invasions append random legal rules to the fight only.
    static IEnumerator FinalBossInvasions()
    {
        Record("Final Boss invasion: developer Wave 3 with a full five-rule loadout, normal dialogue/countdown. Boss health is reduced directly to cross thresholds; raised player HP. No balance claim.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        game.EditLoadout(d =>
        {
            foreach (var (condition, action) in new[] { (13, "Q01"), (3, "Q01"), (6, "F03"), (13, "F03"), (3, "F03") })
                d.TryAddRule((int)ConditionalCatalog.Response(action).Operation, new ConditionalRule { Condition = condition, Action = action });
        });
        Check("Five-rule loadout is full", game.DraftLoadout.TotalRules == ConditionalCatalog.PlayerRuleLimit);
        string equipped = JsonUtility.ToJson(game.DraftLoadout);
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        var actor = game.Bosses.Single(); var axe = actor.Axe;
        game.Player.SetRuntimeHealth(5000, 5000);
        int Bound() => Enumerable.Range(0, 5).Sum(op => game.Rules.RuleCount((ControlOperation)op));

        actor.Health.TakeDamage(260, game.Player.gameObject);
        yield return Until(() => axe.Invading, 2.5f);
        int held = actor.Health.CurrentHealth;
        actor.Health.TakeDamage(50, game.Player.gameObject);
        Check("Crossing 75% starts invasion 1 and the Boss is invulnerable", axe.Invading && actor.Health.CurrentHealth == held && Bound() == 5,
            $"hp={actor.Health.CurrentHealth}");
        yield return Until(() => axe.InvasionProgress > .5f, 1f);
        ScreenCapture.CaptureScreenshot(Output + "final-boss-invasion-signal.png");
        yield return Until(() => axe.LastInvasion != null, 2f); yield return Frames(4);
        ScreenCapture.CaptureScreenshot(Output + "final-boss-invasion-card.png");
        var first = axe.LastInvasion;
        int op = (int)ConditionalCatalog.Response(first.Action).Operation;
        Check("Invasion 1 appends one legal rule after the player's last rule, beyond the cap", Bound() == 6
            && ConditionalCatalog.IsAllowed(op, first) && first.DisplayOrder == 5 && game.InvadedRuleCount == 1,
            $"{ConditionalCatalog.Expression(first).Conditions} -> {ConditionalCatalog.Expression(first).Action} order={first.DisplayOrder}");
        Check("Sidebar shows six rules and the equipped loadout is unchanged",
            CardsUI().GetComponentsInChildren<TMP_Text>().Any(t => t.text == "Rules 6") && JsonUtility.ToJson(game.DraftLoadout) == equipped);
        yield return Until(() => !axe.Invading, 1f);
        actor.Health.TakeDamage(50, game.Player.gameObject);
        Check("Invulnerability ends with the invasion", !axe.Invading && actor.Health.CurrentHealth == held - 50 && axe.Invasions == 1);

        // One large hit crosses 50% and 25% together: both invasions run, one after the other.
        actor.Health.TakeDamage(actor.Health.CurrentHealth - 200, game.Player.gameObject);
        yield return Until(() => axe.Invasions == 3, 8f);
        Check("Thresholds crossed together queue two more invasions (eight rules total)", axe.Invasions == 3 && Bound() == 8 && game.InvadedRuleCount == 3,
            $"invasions={axe.Invasions} bound={Bound()}");
        actor.Health.TakeDamage(100, game.Player.gameObject); yield return Seconds(.6f);
        Check("No fourth invasion", axe.Invasions == 3 && !axe.Invading && Bound() == 8);

        game.Player.TakeDamage(100000, null);
        yield return Until(() => game.Phase == DuelPhase.Failed, 1.5f);
        ClickConfig("Retry");
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        Check("Retry discards invaded rules", Bound() == 5 && game.InvadedRuleCount == 0 && game.Bosses.Single().Axe.Invasions == 0
            && JsonUtility.ToJson(game.DraftLoadout) == equipped);
        game.MainMenu();
        Record("NOT TESTED: invasion under natural combat damage, hand play, readability of the injected card and audio audition.");
    }
}
#endif
