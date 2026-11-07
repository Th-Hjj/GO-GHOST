#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Axe")]
    public static void RunFinalBossAxe() => Start("final-boss-axe", false);

    // Final Boss stage 3: player-sized rainbow axe boss, four telegraphed moves, published threats
    // and the placeholder phase-two hand-off. Forced moves prove mechanics only, not balance.
    static IEnumerator FinalBossAxeMoves()
    {
        Record("Final Boss axe: developer Wave 3, empty loadout, normal dialogue/countdown. Moves are forced once each with controlled placement and raised player HP; one unforced AI window follows. No balance claim.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        var actor = game.Bosses.Single();
        var axe = actor.Axe;
        var playerSize = game.Player.GetComponent<Collider2D>().bounds.size;
        Check("Final Boss is the player-sized axe actor with 1000 + 1000 HP", actor.IsFinalBoss && axe != null
            && actor.Health.MaxHealth == 1000 && Mathf.Abs(actor.Motor.Size.y - playerSize.y) < .05f && Mathf.Abs(actor.Motor.Size.x - playerSize.x) < .05f,
            $"boss={actor.Motor.Size} player={playerSize}");
        var torso = actor.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.name.Contains("Torso"));
        Check("Phase one body uses the rainbow shader", torso.sharedMaterial != null && torso.sharedMaterial.shader.name == "GO GHOST/Final Boss Rainbow",
            torso.sharedMaterial != null ? torso.sharedMaterial.shader.name : "none");
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        game.Player.SetRuntimeHealth(5000, 5000);
        var moves = new[] { (FinalBossAxe.Move.Chop, 1.6f), (FinalBossAxe.Move.Spin, 1.3f), (FinalBossAxe.Move.Throw, 6f), (FinalBossAxe.Move.Boomerang, 8f) };
        foreach (var (move, gap) in moves)
        {
            axe.Cancel(); System.Array.Clear(Private<float[]>(axe, "readyAt"), 0, 5);
            AlphaPlace(-3 + gap, -3); yield return Seconds(.25f);
            int before = game.Player.CurrentHealth, hits = axe.Hits[(int)move];
            axe.Cancel(); System.Array.Clear(Private<float[]>(axe, "readyAt"), 0, 5);
            Check(move + " starts", axe.TryStart(move));
            yield return Until(() => axe.Phase == FinalBossAxe.Stage.Locked, 1.2f);
            Vector2 standing = game.Player.transform.position, half = playerSize * .5f;
            float until = actor.PublicThreats.Count > 0 ? actor.PublicThreats.Max(t => t.EndsAt) : Time.time;
            bool predicted = actor.PublicThreats.Any(t => t.IntersectsMotion(standing, standing, half, Time.time, until));
            bool ranged = actor.PublicThreats.Count > 0 && actor.PublicThreats.All(t => t.IsRanged);
            Check(move + " publishes a locked threat that reaches the standing player", predicted
                && ranged == (move == FinalBossAxe.Move.Throw || move == FinalBossAxe.Move.Boomerang),
                $"threats={actor.PublicThreats.Count} ranged={ranged}");
            ScreenCapture.CaptureScreenshot(Output + "final-boss-axe-" + move.ToString().ToLowerInvariant() + "-locked.png");
            if (move == FinalBossAxe.Move.Throw || move == FinalBossAxe.Move.Boomerang)
            {
                yield return Until(() => axe.AxeFlying, 1f); yield return Seconds(.2f);
                ScreenCapture.CaptureScreenshot(Output + "final-boss-axe-" + move.ToString().ToLowerInvariant() + "-flying.png");
            }
            yield return Until(() => axe.Current == FinalBossAxe.Move.None, 4f);
            Check(move + " hits the standing player and finishes", axe.Hits[(int)move] > hits && game.Player.CurrentHealth < before,
                $"hits={axe.Hits[(int)move] - hits} hp={before}->{game.Player.CurrentHealth}");
        }
        axe.Cancel(); System.Array.Clear(Private<float[]>(axe, "readyAt"), 0, 5);
        int[] startedBefore = (int[])axe.Started.Clone();
        AlphaPlace(5, -4); yield return Seconds(8f);
        string unforced = string.Join(",", Enumerable.Range(1, 4).Select(i => (FinalBossAxe.Move)i + "=" + (axe.Started[i] - startedBefore[i])));
        Check("Unforced AI selects axe moves on its own", Enumerable.Range(1, 4).Sum(i => axe.Started[i] - startedBefore[i]) >= 2, unforced);
        game.Player.IsInvulnerable = true;
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => actor.Health.PhaseIndex == 2, 3.5f);
        yield return Frames(3);
        Check("Placeholder phase two refills 1000 HP and turns white", axe.PhaseTwo && actor.Health.MaxHealth == 1000
            && torso.sharedMaterial.shader.name != "GO GHOST/Final Boss Rainbow" && game.Phase == DuelPhase.Fight);
        ScreenCapture.CaptureScreenshot(Output + "final-boss-axe-phase-two.png"); yield return Frames(3);
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.BonusOffer, 1.5f);
        Check("Second pool defeat opens the Bonus offer", game.Phase == DuelPhase.BonusOffer);
        game.MainMenu();
        Record("NOT TESTED: rule-driven Boss moves (stage 4), invasions (stage 5), scripted transition (stage 7), balance and audio audition.");
    }
}
#endif
