#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Stage Platform Layout Smoke")]
    public static void RunStagePlatforms() => Start("stage-platforms", false);

    static Collider2D[] LayoutSupports() => Private<Collider2D[]>(game, "stageSupports") ?? Private<Collider2D[]>(game, "platforms");

    static void CheckStageLayout(string label, int count, int baselineGround)
    {
        var supports = LayoutSupports();
        var nav = game.Bosses[0].Navigation;
        int ground = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
            .Count(c => c.enabled && c.gameObject.layer == LayerMask.NameToLayer("Ground"));
        Check(label + " platform/render/collider count", supports.Length == count + 1
            && supports.All(c => c.enabled && c.gameObject.activeInHierarchy && c.GetComponent<SpriteRenderer>().enabled)
            && ground == baselineGround - 5 + count, "supports=" + supports.Length + " ground=" + ground);
        Check(label + " navigation and Rules use only active supports", supports.All(c => nav.Nodes.Any(n => n.Collider == c))
            && nav.Nodes.All(n => n.Collider.enabled && n.Collider.gameObject.activeInHierarchy)
            && Private<Collider2D[]>(game.Rules, "platforms").SequenceEqual(supports));
        Check(label + " existing horizontal one-way geometry preserved", supports.Skip(1).All(c =>
            c.usedByEffector && c.GetComponent<PlatformEffector2D>().useOneWay
            && Mathf.Abs(c.transform.eulerAngles.z) < .01f && Mathf.Abs(c.bounds.size.y - .3f) < .001f
            && Vector2.Distance(c.bounds.size, c.GetComponent<SpriteRenderer>().bounds.size) < .001f));
    }

    static IEnumerator LayoutCapture(string label)
    {
        Directory.CreateDirectory(Output);
        ScreenCapture.CaptureScreenshot(Output + "platforms-" + label + ".png");
        yield return Frames(3);
    }

    static IEnumerator OrdinaryPlatformJump(Collider2D target, string label)
    {
        var movement = game.Player.GetComponent<PlayerMovement>();
        var shape = game.Player.GetComponent<Collider2D>();
        float startY = shape.bounds.min.y, peakY = startY;
        int jumps = 0;
        Action onJump = () => jumps++;
        movement.JumpStarted += onJump;
        float targetX = target.bounds.center.x;
        Key direction = targetX < shape.bounds.center.x ? Key.A : Key.D;
        heldKeys = new[] { Key.Space, direction };
        float start = Time.time, until = Time.time + 1.6f;
        bool landed = false;
        while (Time.time < until)
        {
            peakY = Mathf.Max(peakY, shape.bounds.min.y);
            bool atX = direction == Key.A ? shape.bounds.center.x <= targetX : shape.bounds.center.x >= targetX;
            heldKeys = Time.time - start < .10f
                ? atX ? new[] { Key.Space } : new[] { Key.Space, direction }
                : atX ? Array.Empty<Key>() : new[] { direction };
            if (jumps > 0 && Time.time - start > .2f && movement.IsGrounded
                && Mathf.Abs(shape.bounds.min.y - target.bounds.max.y) < .06f) { landed = true; break; }
            yield return null;
        }
        heldKeys = Array.Empty<Key>();
        movement.JumpStarted -= onJump;
        Check(label + " ordinary single jump lands", landed && jumps == 1 && !movement.HasDoubleJump
            && game.Dodge.CooldownRemaining == 0 && game.Rules.AcceptedJumps == 0,
            "jumps=" + jumps + " feet=" + shape.bounds.min + " top=" + target.bounds.max.y.ToString("F3")
            + " rise=" + (peakY - startY).ToString("F3") + " margin=" + (peakY - target.bounds.max.y).ToString("F3"));
        yield return Frames(4);
    }

    static IEnumerator StagePlatforms()
    {
        Record("Stage layout smoke: real player physics and virtual A/D + Space only. Initial floor placement per side; no repositioning between the three landings. Bosses stopped only for reachability checks. Temporary player health extends W2/W3 observations; controlled damage advances lifecycle. Not balance/victory proof.");
        game.MainMenu(); yield return Frames(3);
        var authored = Private<Collider2D[]>(game, "platforms");
        var positions = authored.Select(c => c.transform.position).ToArray();
        var scales = authored.Select(c => c.transform.localScale).ToArray();
        var sizes = authored.Select(c => c.bounds.size).ToArray();
        var arena = game.ArenaBounds;
        int baselineGround = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
            .Count(c => c.enabled && c.gameObject.layer == LayerMask.NameToLayer("Ground"));
        SetAlphaGameSize(1920, 1080);
        yield return TrainingFresh(d => V2Rule(d, new ConditionalRule { Condition = 3, Action = "F01", AmmoUse = AmmoUseOption.AllowEmpty }));
        CheckStageLayout("Training", 1, baselineGround);
        Check("normal player tuning retained", game.Player.GetComponent<PlayerMovement>().JumpSpeed == 12
            && game.Player.GetComponent<PlayerMovement>().MoveSpeed == 7 && game.Player.GetComponent<Rigidbody2D>().gravityScale == 3);
        yield return LayoutCapture("training");
        var trainer = game.Bosses[0];
        yield return Seconds(4.5f);
        Check("training enemy retains real slow shots", trainer.IsTrainingEnemy && trainer.Actions.Activated[(int)BossAbility.Burst] > 0);
        trainer.StopCombat();
        OnFloor(game.Player.gameObject, trainer.Navigation, -6.5f); yield return Seconds(.2f);
        yield return OrdinaryPlatformJump(LayoutSupports()[1], "Training ground -> platform");
        Check("real platform landing still triggers training Rule fire", game.Rules.AutomaticShots > 0 && game.Rifle.ShotsFired > 0);
        var previous = Private<GameObject>(game, "stagePlatforms");
        game.BackFromTraining();
        Check("Back immediately disables old geometry", previous != null && !previous.activeInHierarchy && authored.All(c => c.gameObject.activeInHierarchy));
        yield return Frames(3);
        Check("Back destroys old platform group", previous == null);
        game.StartConfiguredEncounter(); yield return Frames(5);
        CheckStageLayout("Training re-entry", 1, baselineGround);
        previous = Private<GameObject>(game, "stagePlatforms");
        var oldPlayer = game.Player;
        game.Player.TakeDamage(game.Player.MaxHealth, game.Bosses[0].gameObject);
        yield return Until(() => game.Player != oldPlayer, 2); yield return Frames(4);
        Check("training respawn replaces one platform group without accumulation", previous == null && game.Player != oldPlayer);
        CheckStageLayout("Training respawn", 1, baselineGround);
        game.ContinueFromTraining(); yield return Frames(3);
        Check("Continue still opens Rule Setup", game.Phase == DuelPhase.Configure && game.Player == null && !game.TrainingPending);
        game.StartFight(); yield return Frames(5);
        CheckStageLayout("W1", 7, baselineGround);
        var waveOne = LayoutSupports();
        Check("W1 mirrored three equal-height pairs", Enumerable.Range(1, 3).All(i =>
            Mathf.Abs(waveOne[i].bounds.center.x + waveOne[i + 3].bounds.center.x) < .001f
            && Mathf.Abs(waveOne[i].bounds.max.y - waveOne[i + 3].bounds.max.y) < .001f
            && Mathf.Abs(waveOne[i].bounds.max.y - authored[0].bounds.max.y - 1.7f * i) < .001f));
        yield return LayoutCapture("wave1");
        yield return Until(() => game.Phase == DuelPhase.Fight, 4);
        yield return Seconds(4);
        Check("W1 live Boss starts attacks", game.Phase == DuelPhase.Fight && game.Bosses[0].Actions.AttackId > 0);
        game.Bosses[0].StopCombat();
        foreach (var shot in game.GetComponentsInChildren<Projectile>()) shot.Retire();
        for (int side = 0; side < 2; side++)
        {
            OnFloor(game.Player.gameObject, game.Bosses[0].Navigation, side == 0 ? -6.5f : 6.5f);
            yield return Seconds(.2f);
            for (int level = 1; level <= 3; level++)
                yield return OrdinaryPlatformJump(waveOne[level + side * 3], "W1 " + (side == 0 ? "left" : "right") + " level " + level);
        }
        game.Player.TakeDamage(game.Player.MaxHealth, game.Bosses[0].gameObject);
        yield return Until(() => game.Phase == DuelPhase.Failed, 1);
        game.Reconfigure(); game.StartFight(); yield return Frames(4);
        CheckStageLayout("W1 retry", 7, baselineGround);
        yield return Until(() => game.Phase == DuelPhase.Fight, 4);
        game.Bosses[0].Health.TakeDamage(game.Bosses[0].Health.MaxHealth, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.Configure, 2);
        Check("normal W1 -> W2 progression", game.Wave == 2 && Private<GameObject>(game, "stagePlatforms") == null);
        for (int wave = 2; wave <= 3; wave++)
        {
            game.StartFight(); yield return Frames(5);
            CheckStageLayout("W" + wave, 5, baselineGround);
            Check("W" + wave + " exact original transforms/sizes/bounds and Boss roster", authored.Select((c, i) =>
                c.transform.position == positions[i] && c.transform.localScale == scales[i] && c.bounds.size == sizes[i]).All(v => v)
                && game.ArenaBounds == arena && game.Bosses.Count == (wave == 3 ? 2 : 1));
            yield return LayoutCapture("wave" + wave);
            game.Player.SetRuntimeHealth(10000, 10000);
            yield return Until(() => game.Phase == DuelPhase.Fight, 4); yield return Seconds(4);
            Check("W" + wave + " short live combat", game.Phase == DuelPhase.Fight && game.Bosses.Any(b => b.Actions.AttackId > 0));
            foreach (var boss in game.Bosses) boss.Health.TakeDamage(boss.Health.MaxHealth, game.Player.gameObject);
            yield return Until(() => game.Phase == (wave == 3 ? DuelPhase.Complete : DuelPhase.Configure), 2);
        }
        Check("normal three-wave flow completes", game.Phase == DuelPhase.Complete);
        yield return TrainingFresh(null);
        game.ToggleDeveloperTools(); game.JumpToDeveloperWave(1); yield return Frames(4);
        CheckStageLayout("F8 Training -> W1", 7, baselineGround);
        game.ToggleDeveloperTools(); game.JumpToDeveloperWave(3); yield return Frames(4);
        CheckStageLayout("F8 W1 -> W3", 5, baselineGround);
        game.MainMenu(); yield return Frames(4);
        Check("menu leaves no runtime layout/collider/actor residue", Private<GameObject>(game, "stagePlatforms") == null
            && Object.FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None).Length == 5
            && Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length == 0 && Projectile.Active.Count == 0);
    }
}
#endif
