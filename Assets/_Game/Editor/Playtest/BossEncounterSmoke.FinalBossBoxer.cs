#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Boxer")]
    public static void RunFinalBossBoxer() => Start("final-boss-boxer", false);

    // Final Boss stage 8: the White Boxer. Forced punches prove mechanics and poses only, not balance.
    static IEnumerator FinalBossBoxerPhase()
    {
        Record("Final Boss boxer: developer Wave 3, empty loadout, full scripted transition. Punches forced once each with controlled placement and raised player HP; one unforced window follows. No balance claim.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        var actor = game.Bosses.Single(); var axe = actor.Axe;
        game.Player.IsInvulnerable = true;
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return CompleteFinalBossTransition(1);
        game.Player.IsInvulnerable = false;
        game.Player.SetRuntimeHealth(5000, 5000);
        var boxer = axe.Boxer;
        var view = actor.GetComponentInChildren<BossSilhouetteView>();
        Check("Phase two runs the White Boxer with red eyes and fists", boxer != null && axe.PhaseTwo && view.EyesRed
            && actor.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.EndsWith("Fist") && r.enabled) == 2);
        // The platforms move during phase two; force the punches on the floor at the arena's open centre.
        var punches = new[] { (FinalBossBoxer.Punch.Jab, 1.6f), (FinalBossBoxer.Punch.Uppercut, 1.0f), (FinalBossBoxer.Punch.SlipHook, 1.5f), (FinalBossBoxer.Punch.Haymaker, 2.0f) };
        foreach (var (punch, gap) in punches)
        {
            boxer.Cancel(); System.Array.Clear(Private<float[]>(boxer, "readyAt"), 0, 5);
            // Boss perception is delayed: place once, let his facts catch up, then place exactly again.
            AlphaPlace(1.5f, 1.5f - gap); yield return Seconds(.45f);
            boxer.Cancel(); System.Array.Clear(Private<float[]>(boxer, "readyAt"), 0, 5);
            AlphaPlace(1.5f, 1.5f - gap); yield return Frames(2);
            boxer.Cancel(); System.Array.Clear(Private<float[]>(boxer, "readyAt"), 0, 5);
            int before = game.Player.CurrentHealth, hits = boxer.Hits[(int)punch];
            float side = Mathf.Sign(actor.Position.x - game.Player.transform.position.x);
            Check(punch + " starts", boxer.TryStart(punch));
            yield return Until(() => boxer.Phase == FinalBossBoxer.Stage.Windup, 1f);
            yield return Until(() => boxer.PhaseProgress > .6f, 1f);
            int lines = actor.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled);
            Check(punch + " wind-up draws no tell lines", lines == 0, $"lines={lines}");
            ScreenCapture.CaptureScreenshot(Output + "final-boss-boxer-" + punch.ToString().ToLowerInvariant() + "-windup.png");
            yield return Until(() => boxer.Phase == FinalBossBoxer.Stage.Active, 1.5f);
            Record($"ACTIVE {punch}: boss={actor.Position} facing={boxer.LockedFacing} area={boxer.PunchArea()} player={game.Player.GetComponent<Collider2D>().bounds} invulnerable={game.Player.IsInvulnerable}");
            yield return Frames(2);
            if (punch == FinalBossBoxer.Punch.Haymaker || punch == FinalBossBoxer.Punch.Uppercut)
                ScreenCapture.CaptureScreenshot(Output + "final-boss-boxer-" + punch.ToString().ToLowerInvariant() + "-gold.png");
            yield return Until(() => boxer.Current == FinalBossBoxer.Punch.None, 2.5f);
            Check(punch + " lands once on the standing player", boxer.Hits[(int)punch] == hits + 1 && game.Player.CurrentHealth < before,
                $"hits={boxer.Hits[(int)punch] - hits} hp={before}->{game.Player.CurrentHealth}");
            if (punch == FinalBossBoxer.Punch.SlipHook)
                Check("The slip switches to the player's other side", Mathf.Sign(actor.Position.x - game.Player.transform.position.x) == -side);
        }

        boxer.Cancel(); System.Array.Clear(Private<float[]>(boxer, "readyAt"), 0, 5);
        int[] startedBefore = (int[])boxer.Started.Clone();
        AlphaPlace(-7, 7); yield return Seconds(.2f);
        float startGap = Mathf.Abs(actor.Position.x - game.Player.transform.position.x);
        var trails = actor.GetComponentsInChildren<TrailRenderer>();
        int longestTrail = 0; float minGap = startGap;
        float until = Time.time + 6f;
        while (Time.time < until)
        {
            longestTrail = Mathf.Max(longestTrail, trails.Length == 0 ? 0 : trails.Max(t => t.positionCount));
            minGap = Mathf.Min(minGap, Mathf.Abs(actor.Position.x - game.Player.transform.position.x));
            if (Mathf.Abs(Time.time - (until - 5.6f)) < .02f) ScreenCapture.CaptureScreenshot(Output + "final-boss-boxer-approach.png");
            yield return null;
        }
        string unforced = string.Join(",", Enumerable.Range(1, 4).Select(i => (FinalBossBoxer.Punch)i + "=" + (boxer.Started[i] - startedBefore[i])));
        Check("Unforced, the boxer closes in and throws punches", minGap < 2.5f && Enumerable.Range(1, 4).Sum(i => boxer.Started[i] - startedBefore[i]) >= 2,
            $"gap {startGap:F1}->{minGap:F1} {unforced}");
        Check("Both eyes leave red tail-light trails while he moves", trails.Length == 2 && longestTrail > 3, $"trails={trails.Length} points={longestTrail}");

        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.BonusOffer, 1.5f);
        Check("Defeating the White Boxer opens the Bonus offer", game.Phase == DuelPhase.BonusOffer);
        game.MainMenu();
        Record("NOT TESTED: chasing a player onto moving platforms, hand play, balance and audio cue choice.");
    }
}
#endif
