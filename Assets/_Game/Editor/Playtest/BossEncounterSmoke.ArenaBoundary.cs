#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Arena Boundary and Defaults")]
    public static void RunArenaBoundary() => Start("arena-boundary", false);

    static bool ArenaLinesInside(BossAgent actor)
    {
        var bounds = actor.Navigation.ArenaBounds;
        foreach (var line in actor.View.GetComponentsInChildren<LineRenderer>())
            if (line.enabled && line.useWorldSpace)
                for (int i = 0; i < line.positionCount; i++)
                    if (!ArenaBoundary2D.Contains(bounds, line.GetPosition(i), Mathf.Max(line.startWidth, line.endWidth) * .5f - .001f)) return false;
        return true;
    }

    static IEnumerator ArenaBoundary()
    {
        Record("Controlled boundary fixtures; real projectile physics, Boss renderers, sword hazard ticks and normal encounter lifecycle.");
        yield return TrainingFresh(null);
        Check("fresh session uses captured tuning", Mathf.Abs(game.GetDeveloperValue(DeveloperSetting.DodgeCooldown) - 1.7f) < .0001f
            && game.GetDeveloperValue(DeveloperSetting.Damage) == 28 && game.GetDeveloperValue(DeveloperSetting.FireInterval) == .4f
            && game.GetDeveloperValue(DeveloperSetting.BurstSpread) == 14);
        game.ToggleDeveloperTools();
        game.SetDeveloperValue(DeveloperSetting.Damage, 11); game.ResetDeveloperTuning();
        Check("Restore defaults returns captured tuning", game.GetDeveloperValue(DeveloperSetting.Damage) == 28
            && game.GetDeveloperValue(DeveloperSetting.DodgeCooldown) == 1.7f && game.GetDeveloperValue(DeveloperSetting.BurstSpread) == 14);
        game.ToggleDeveloperTools();
        var trainer = game.Bosses[0];
        var bounds = game.ArenaBounds;
        Check("training has attempt-local boundary", game.Player.GetComponentInParent<ArenaBoundary2D>().Bounds == bounds);
        OnFloor(trainer.gameObject, trainer.Navigation, 12.5f);
        typeof(BossAgent).GetField("trainingDirection", PrivateInstance).SetValue(trainer, 1f);
        trainer.Actions.StopCombat();
        trainer.Actions.TryStart(BossAbility.Burst, trainer.Position + Vector2.right * 30);
        yield return Seconds(.3f);
        Check("training long forward warning stays within arena", trainer.PublicThreats.Count > 0
            && trainer.PublicThreats.All(t => ArenaBoundary2D.Contains(bounds, t.End)) && ArenaLinesInside(trainer)
            && trainer.View.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled && l.name == "GunAimTell"));
        ScreenCapture.CaptureScreenshot(Output + "arena-training-warning.png");
        yield return Frames(2);
        trainer.StopCombat();

        var directions = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down, new Vector2(1, 1).normalized };
        foreach (var direction in directions)
        {
            Vector2 centre = bounds.center;
            Vector2 origin = centre + direction * (ArenaBoundary2D.ExitDistance(bounds, centre, direction) - .6f);
            var bullet = Object.Instantiate(trainer.ProjectilePrefab, origin, Quaternion.identity);
            bullet.Launch(direction, 80, 1, game.Player.gameObject);
            bool inside = true;
            float end = Time.time + .2f;
            while (bullet != null && Time.time < end)
            {
                inside &= ArenaBoundary2D.Contains(bounds, bullet.transform.position);
                yield return null;
            }
            Check("fast bullet stops at perimeter " + direction, inside && bullet == null);
        }
        // A real bullet still crosses the training platform in the arena interior.
        var platform = LayoutSupports().OrderByDescending(c => c.bounds.max.y).First();
        Vector2 under = new Vector2(platform.bounds.center.x, platform.bounds.min.y - .4f);
        var through = Object.Instantiate(trainer.ProjectilePrefab, under, Quaternion.identity);
        through.Launch(Vector2.up, 10, 1, game.Player.gameObject);
        yield return Seconds(.16f);
        Check("interior platform remains penetrable", through != null && through.transform.position.y > platform.bounds.max.y);
        if (through != null) through.Retire();
        var reflected = Object.Instantiate(trainer.ProjectilePrefab, new Vector2(-14, 5), Quaternion.identity);
        reflected.Launch(Vector2.right, 10, 1, game.Player.gameObject);
        Check("reflection retains live projectile", reflected.Deflect(trainer.gameObject, ProjectileActor.Environment));
        yield return Seconds(.2f);
        Check("reflected bullet also stops at boundary", reflected == null);
        game.ContinueFromTraining(); game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.5f);
        Check("training Continue enters W1", game.Phase == DuelPhase.Fight && game.Wave == 1);

        yield return Fresh(2); StopPolicies(); yield return Frames(2);
        var sword = game.Bosses[0];
        var hazards = Private<SwordHazards>(sword.Actions, "swordHazards");
        hazards.Clear();
        // Instance-only speed fixture makes the old 2.4-second cutoff observable in a 30-unit arena.
        var original = sword.Profile;
        var profile = Object.Instantiate(original);
        typeof(BossAgent).GetField("profile", PrivateInstance).SetValue(sword, profile);
        restoration.Add(() => { if (sword != null) typeof(BossAgent).GetField("profile", PrivateInstance).SetValue(sword, original); Object.Destroy(profile); });
        profile.Sword.WaveSpeed = 5;
        hazards.Wave(new Vector2(-13, 6), Vector2.right);
        hazards.Cut(new Vector2(-14, 3.65f), Vector2.right, false);
        var wave = hazards.Traces.First(t => t.Wave);
        var echo = hazards.Traces.First(t => !t.Wave);
        Check("public threats predict actual boundary instead of lifetime or range", wave.Threat.EndsAt - wave.Threat.StartsAt > original.Sword.WaveLife
            && (echo.Threat.EndsAt - echo.Threat.StartsAt) * echo.Threat.ProjectileSpeed > original.Sword.EchoTravel);
        yield return Until(() => Time.time > echo.Threat.StartsAt + 19f / echo.Threat.ProjectileSpeed, 3);
        Check("afterblade remains after old 18-unit travel cap", hazards.Traces.Contains(echo) && ArenaLinesInside(sword));
        checkingPause = true; game.SetPause(true);
        Vector2 beforePause = echo.A(Time.time); yield return RealSeconds(.15);
        Check("pause freezes blade flight", echo.A(Time.time) == beforePause && hazards.Traces.Contains(echo));
        game.SetPause(false); checkingPause = false;
        yield return Until(() => Time.time > wave.Threat.StartsAt + 2.5f, 2);
        Check("ordinary wave remains past old 2.4-second lifetime", hazards.Traces.Contains(wave));
        Check("afterblade retires at physical boundary", !hazards.Traces.Contains(echo));
        ScreenCapture.CaptureScreenshot(Output + "arena-long-lived-wave.png");
        yield return Until(() => !hazards.Pending, 4);
        Check("ordinary wave retires at physical boundary", !hazards.Pending && ArenaLinesInside(sword));

        foreach (var direction in directions)
        {
            hazards.Clear();
            Vector2 centre = bounds.center;
            Vector2 nearEdge = centre + direction * (ArenaBoundary2D.ExitDistance(bounds, centre, direction) - 1.2f);
            hazards.Wave(nearEdge, direction);
            yield return Seconds(.3f);
            Check("sword wave stops at perimeter " + direction, !hazards.Pending && ArenaLinesInside(sword));
        }
        // Existing body collision remains a separate removal reason.
        var playerCentre = (Vector2)game.Player.GetComponent<Collider2D>().bounds.center;
        hazards.Wave(playerCentre + Vector2.left, Vector2.right);
        yield return Seconds(.3f);
        Check("ordinary wave still retires on player contact", !hazards.Pending);

        yield return Fresh(3); StopPolicies(); yield return Frames(2);
        sword = game.Bosses.First(b => b.ActorId == 2);
        var gunner = game.Bosses.First(b => b.ActorId == 1);
        hazards = Private<SwordHazards>(sword.Actions, "swordHazards"); hazards.Clear();
        var ally = (Vector2)gunner.Body.bounds.center;
        hazards.Wave(ally + Vector2.left, Vector2.right);
        int blocks = hazards.AllyBlocks;
        yield return Seconds(.15f);
        Check("W3 ally body still blocks sword wave", !hazards.Pending && hazards.AllyBlocks > blocks);
        game.MainMenu(); yield return Frames(3);
        Check("menu cleans projectiles and arena boundary", Projectile.Active.Count == 0
            && Object.FindObjectsByType<ArenaBoundary2D>(FindObjectsSortMode.None).Length == 0);
    }
}
#endif
