#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Platforms")]
    public static void RunFinalBossPlatforms() => Start("final-boss-platforms", false);

    // Final Boss stage 6: the phase-two shockwave starts the five platforms moving.
    static IEnumerator FinalBossMovingPlatforms()
    {
        Record("Final Boss platforms: developer Wave 3, empty loadout, normal dialogue/countdown. Phase two is reached by direct Boss damage; the player is placed onto a rising platform. Motion and riding only; no balance claim.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        var actor = game.Bosses.Single();
        var supports = LayoutSupports();
        var platforms = supports.Skip(1).OrderBy(c => c.bounds.center.x).ToArray();
        var home = platforms.Select(c => c.bounds.center.y).ToArray();
        yield return Seconds(.6f);
        Check("Phase one platforms are static", platforms.Select((c, i) => Mathf.Abs(c.bounds.center.y - home[i])).Max() < .001f
            && platforms.All(c => c.GetComponent<PlatformOscillator>() == null));

        game.Player.IsInvulnerable = true;
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        // The shockwave comes after the first transition line.
        yield return Until(() => platforms.All(c => c.GetComponent<PlatformOscillator>() != null), 10f);
        float started = Time.time;
        Check("The phase-two shockwave starts all five platforms; the floor stays", platforms.Length == 5
            && platforms.All(c => c.GetComponent<PlatformOscillator>() != null) && supports[0].GetComponent<PlatformOscillator>() == null);
        yield return Until(() => Time.time - started >= PlatformOscillator.Period / 4, 2f);
        var offsets = platforms.Select(c => c.GetComponent<PlatformOscillator>().Offset).ToArray();
        string offsetText = string.Join(",", offsets.Select(o => o.ToString("F2")));
        Check("A quarter cycle later, alternate platforms sit one tier above and below home",
            offsets.Select((o, i) => Mathf.Abs(o - (i % 2 == 0 ? 1 : -1) * PlatformOscillator.Amplitude)).Max() < .2f, offsetText);
        ScreenCapture.CaptureScreenshot(Output + "final-boss-platforms-quarter.png"); yield return Frames(3);

        // Ride an even platform while it rises (second half of its cycle, from its lowest point).
        var ride = platforms[0].GetComponent<PlatformOscillator>();
        yield return Until(() => ride.VerticalSpeed > 1.5f, 4f);
        var top = platforms[0].bounds;
        var playerBody = game.Player.GetComponent<Rigidbody2D>();
        float half = game.Player.GetComponent<Collider2D>().bounds.extents.y;
        playerBody.position = new Vector2(top.center.x, top.max.y + half + .02f); playerBody.linearVelocity = Vector2.zero;
        yield return Seconds(.15f);
        float startY = playerBody.position.y, platformY = platforms[0].bounds.max.y;
        var movement = game.Player.GetComponent<PlayerMovement>();
        int groundedFrames = 0, frames = 0;
        float until = Time.time + .35f;
        while (Time.time < until) { frames++; if (movement.IsGrounded) groundedFrames++; yield return null; }
        float rose = playerBody.position.y - startY, carried = platforms[0].bounds.max.y - platformY;
        Check("A rising platform carries the player and keeps them grounded", rose > .3f && Mathf.Abs(rose - carried) < .15f && groundedFrames >= frames * .8f,
            $"rose={rose:F2} platform={carried:F2} grounded={groundedFrames}/{frames} vy={ride.VerticalSpeed:F2}");

        checkingPause = true;
        game.SetPause(true); yield return Frames(3);
        float paused = platforms[1].bounds.center.y;
        yield return Frames(30);
        Check("Pause freezes the platforms", Mathf.Abs(platforms[1].bounds.center.y - paused) < .001f);
        game.SetPause(false); checkingPause = false;
        yield return Seconds(.3f);
        Check("Unpause resumes motion", Mathf.Abs(platforms[1].bounds.center.y - paused) > .05f);

        float worst = 0, total = 0; int samples = 0;
        until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until) { worst = Mathf.Max(worst, Time.unscaledDeltaTime); total += Time.unscaledDeltaTime; samples++; yield return null; }
        Record($"INFO Editor frame time while platforms move: mean {total / Mathf.Max(1, samples) * 1000:F1} ms, worst {worst * 1000:F1} ms over {samples} frames.");

        game.Player.IsInvulnerable = false;
        game.Player.TakeDamage(100000, null);
        yield return Until(() => game.Phase == DuelPhase.Failed, 1.5f);
        ClickConfig("Retry"); yield return Frames(3);
        var fresh = LayoutSupports().Skip(1).OrderBy(c => c.bounds.center.x).ToArray();
        Check("Retry rebuilds static platforms at their home heights", fresh.Length == 5 && fresh.All(c => c.GetComponent<PlatformOscillator>() == null)
            && fresh.Select((c, i) => Mathf.Abs(c.bounds.center.y - home[i])).Max() < .01f);
        game.MainMenu();
        Record("NOT TESTED: Boss riding a platform, hand play on moving platforms, player drop-through while a platform moves.");
    }
}
#endif
