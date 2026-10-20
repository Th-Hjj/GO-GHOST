#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/W1 Gunner Platform Smoke")]
    public static void RunGunnerPlatforms() => Start("gunner-platforms", false);
    public static void RunGunnerPlatformCrossings() => Start("gunner-crossings", false);

    static IEnumerator GunnerPlatformCrossings()
    {
        Record("Four directed physical crossings through the same W1 policy route builder. Controlled initial actor position and public player side; no position writes after takeoff, no Camo, real motor/terrain/landing.");
        yield return GunnerPlatformFresh(false);
        var gun = game.Bosses[0]; var supports = LayoutSupports();
        gun.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
        game.Player.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
        Position(gun.gameObject, new Vector2(supports[5].bounds.center.x, supports[5].bounds.max.y + gun.Motor.Size.y * .5f + .025f));
        yield return Seconds(.3f);
        var track = typeof(BossTactics).GetMethod("TrackGunSupport", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cross = typeof(BossTactics).GetMethod("TryPlatformCrossing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int[] targets = { 7, 2, 7, 5 };
        string[] labels = { "Right -> centre", "Centre -> left", "Left -> centre", "Centre -> right" };
        for (int i = 0; i < targets.Length; i++)
        {
            if (i == 2)
            {
                Position(game.Player.gameObject, new Vector2(9, supports[5].bounds.max.y + .80f));
                yield return Until(() => gun.Perception.Facts.Visible && gun.Perception.Facts.Position.x > 7, 1.5f);
                Check("reverse direction is driven by a new visible public observation", gun.Perception.Facts.Visible && gun.Perception.Facts.Position.x > 7);
            }
            gun.Motor.HoldHorizontal(); track.Invoke(gun.Tactics, null);
            var before = gun.Position; float previousJump = gun.Motor.JumpStartedAt;
            bool accepted = (bool)cross.Invoke(gun.Tactics, new object[] { gun.Perception.Facts });
            var route = gun.Tactics.Current?.Route;
            Check(labels[i] + " route is a physical single jump with bounded supported approach", accepted && route != null
                && route.Steps.Count(s => s.Kind == ArenaNavigation2D.TravelKind.Jump) == 1
                && route.Steps.All(s => s.Kind != ArenaNavigation2D.TravelKind.Drop));
            var target = supports[targets[i]]; float deadline = Time.time + 4; int jumps = 0; bool landed = false;
            while (Time.time < deadline)
            {
                if (gun.Motor.JumpStartedAt != previousJump) { previousJump = gun.Motor.JumpStartedAt; jumps++; }
                if (gun.Motor.RouteCompleted && gun.Motor.Grounded && gun.Navigation.TrySupport(gun.Position, gun.Motor.Size, out var on) && on == target)
                { landed = true; break; }
                yield return null;
            }
            Check(labels[i] + " real landing without teleport or falling to ground", landed && jumps == 1 && gun.Tactics.PlatformBlinks == 0,
                "from=" + before + " to=" + gun.Position + " jumps=" + jumps + " failure=" + gun.Motor.RouteFailure);
            yield return Seconds(.2f);
            if (i == 0) yield return LayoutCapture("gunner-centre-jump");
        }
        game.Player.SetRuntimeHealth(10000, 10000); gun.Tactics.Enabled = true;
        int attacks = gun.Actions.AttackId;
        yield return Seconds(4);
        Check("normal attack policy resumes after physical crossings", gun.Actions.AttackId > attacks && game.Phase == DuelPhase.Fight);
        game.MainMenu(); yield return Frames(3);
    }

    static IEnumerator GunnerPlatformFresh(bool policy)
    {
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        game.ConfigurePlaytestWave(1); game.StartFight();
        game.Bosses[0].Tactics.Enabled = policy;
        yield return Until(() => game.Phase == DuelPhase.Fight, 4);
        yield return Seconds(.25f);
    }

    static IEnumerator GunnerPlatforms()
    {
        Record("W1 platform pressure: unforced Boss movement/attacks; virtual player input. Extra player HP only during observation. Controlled placement/death/occupied destination only in separate mechanism fixtures. No balance or full-fight claim.");
        SetAlphaGameSize(1920, 1080);
        yield return GunnerPlatformFresh(true);
        var gun = game.Bosses[0]; var supports = LayoutSupports(); var centre = supports[7];
        Check("W1 seven platforms; central platform matches middle height and width", supports.Length == 8
            && Mathf.Abs(centre.bounds.center.x) < .001f && Mathf.Abs(centre.bounds.max.y - supports[2].bounds.max.y) < .001f
            && supports.Skip(1).All(c => Mathf.Abs(c.bounds.size.x - 4) < .001f));
        Check("all seven platform centres accept the real gunner body", supports.Skip(1).All(c => gun.Navigation.CanStand(
            new Vector2(c.bounds.center.x, c.bounds.max.y + gun.Motor.Size.y * .5f + .025f), gun.Motor.Size)));
        Check("W1 range independent of new platform spacing; original combat stats", gun.Tactics.PlatformPressureEnabled
            && gun.Tactics.EngagementDistance == 7 && gun.Profile.Motion.Speed == 5.8f && gun.Profile.Motion.JumpSpeed == 12
            && gun.Profile.BulletSpeed == 24 && gun.Profile.Health == 666 && gun.Profile.Camo.Cooldown == 7);
        game.Player.SetRuntimeHealth(10000, 10000);
        var visited = new HashSet<Collider2D>();
        int lastAttack = -1; Collider2D lastSupport = null;
        float start = Time.time, end = Time.time + 20;
        bool airborneShot = false, captured = false;
        while (Time.time < end && gun.IsAlive)
        {
            if (gun.Motor.Grounded && gun.Navigation.TrySupport(gun.Position, gun.Motor.Size, out var on))
            {
                if (supports.Skip(1).Contains(on)) visited.Add(on);
                if (on != lastSupport) { Record("OBS support=" + on.name + " centre=" + on.bounds.center + " t=" + (Time.time - start).ToString("F2")); lastSupport = on; }
            }
            if (gun.Actions.AttackId != lastAttack)
            { lastAttack = gun.Actions.AttackId; Record("OBS action=" + gun.Actions.Ability + " position=" + gun.Position + " t=" + (Time.time - start).ToString("F2")); }
            airborneShot |= gun.Actions.Ability == BossAbility.TacticalJumpShot && gun.Actions.Phase == BossActionPhase.Active && gun.Actions.Ammo < gun.Profile.Magazine;
            // A few seconds of actual player fire tests exposure-driven choices without killing the Boss.
            MouseAt(gun.Position, Time.time - start > 7 && Time.time - start < 11);
            float elapsed = Time.time - start;
            heldKeys = elapsed > 8 && elapsed < 9.3f || elapsed > 14 && elapsed < 15.3f ? new[] { Key.D } : Array.Empty<Key>();
            if (!captured && visited.Count >= 2 && !gun.IsCamo)
            { captured = true; yield return LayoutCapture("gunner-platforms"); }
            yield return null;
        }
        mouseHeld = false; heldKeys = Array.Empty<Key>();
        Check("unforced W1 leaves floor, changes platforms and attacks there", gun.Tactics.PlatformMoves > 0 && visited.Count >= 3 && gun.Tactics.PlatformAttacks > 0,
            "moves=" + gun.Tactics.PlatformMoves + " blinks=" + gun.Tactics.PlatformBlinks + " visited=" + visited.Count + " attacks=" + gun.Tactics.PlatformAttacks);
        Check("unforced W1 uses Camo platform relocation", gun.Tactics.PlatformBlinks > 0);
        Check("unforced W1 uses existing airborne gun attack", airborneShot);
        Check("live attacks still damage player and accept player bullets", game.Player.CurrentHealth < 10000 && gun.Health.CurrentHealth < 666,
            "player=" + game.Player.CurrentHealth + " gunner=" + gun.Health.CurrentHealth);

        // Stand at the inner edge of the middle-left support; the crossing itself is normal A/D/Space physics.
        gun.StopCombat(); foreach (var shot in game.GetComponentsInChildren<Projectile>()) shot.Retire();
        var playerShape = game.Player.GetComponent<Collider2D>();
        Position(game.Player.gameObject, new Vector2(supports[2].bounds.max.x - playerShape.bounds.extents.x - .05f,
            supports[2].bounds.max.y + playerShape.bounds.extents.y + .025f));
        yield return Seconds(.2f);
        yield return OrdinaryPlatformJump(centre, "Middle-left -> central platform");
        yield return LayoutCapture("central-platform");

        yield return GunnerPlatformFresh(false); gun = game.Bosses[0];
        var original = gun.Position; int hp = gun.Health.CurrentHealth, ammo = gun.Actions.Ammo;
        Check("Camo can commit a legal platform destination", gun.Tactics.TryStartCamo());
        var committed = Private<BossPlan>(gun.Tactics, "camoPlan");
        Check("committed random destination belongs to an active platform", committed != null && LayoutSupports().Skip(1).Any(c =>
            Mathf.Abs(c.bounds.center.x - committed.Destination.x) < .01f && Mathf.Abs(c.bounds.max.y + gun.Motor.Size.y * .5f + .025f - committed.Destination.y) < .01f));
        yield return Until(() => gun.Actions.Phase == BossActionPhase.Active, 1);
        yield return Seconds(.15f);
        Check("Camo hides before relocating and publishes no moving target", gun.IsCamo && Vector2.Distance(original, gun.Position) < .05f
            && (!gun.TryGetContact(out var hidden) || !hidden.Visible && !hidden.VelocityKnown));
        checkingPause = true; game.SetPause(true); var pausedPosition = gun.Position;
        yield return RealSeconds(.2);
        Check("pause freezes pending blink", gun.Position == pausedPosition && gun.Tactics.PlatformBlinks == 0);
        game.SetPause(false); checkingPause = false;
        yield return Until(() => gun.Tactics.PlatformBlinks > 0, 1.5f);
        float arrived = Time.time;
        Check("one concealed relocation lands at frozen destination with no resources reset", gun.Tactics.PlatformBlinks == 1
            && Vector2.Distance(gun.Position, committed.Destination) < .08f && gun.Motor.Grounded
            && gun.Motor.Velocity.sqrMagnitude < .1f && gun.Health.CurrentHealth == hp && gun.Actions.Ammo == ammo);
        yield return Seconds(.25f);
        Check("relocated body stays supported after physics settles", gun.Motor.Grounded
            && Mathf.Abs(gun.Position.y - committed.Destination.y) < .08f,
            "actual=" + gun.Position + " committed=" + committed.Destination);
        gun.Tactics.Enabled = true;
        yield return Until(() => gun.Actions.Ability != BossAbility.Camo && gun.Actions.Ability != BossAbility.None, 2);
        var followup = gun.Actions.Ability;
        Check("reappearance starts a visible full preparation", !gun.IsCamo && gun.Actions.Phase == BossActionPhase.Prepare);
        yield return Until(() => gun.Actions.Phase == BossActionPhase.Active, 2);
        float requiredTell = gun.Profile.Camo.Recovery + gun.Profile.Settings(followup).Prepare
            + (followup == BossAbility.Burst ? 0 : gun.Profile.Settings(followup).Lock);
        Check("reappearance never skips attack wind-up", Time.time - arrived >= requiredTell - .06f,
            "elapsed=" + (Time.time - arrived).ToString("F3") + " minimum=" + requiredTell.ToString("F3"));

        yield return GunnerPlatformFresh(false); gun = game.Bosses[0]; original = gun.Position;
        Check("occupied-destination fixture commits", gun.Tactics.TryStartCamo());
        committed = Private<BossPlan>(gun.Tactics, "camoPlan");
        Position(game.Player.gameObject, committed.Destination);
        game.Player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        yield return Until(() => !gun.Actions.Busy, 2);
        Check("new occupant cancels relocation without retargeting or overlap", gun.Tactics.PlatformBlinks == 0
            && Vector2.Distance(original, gun.Position) < .1f && !gun.Tactics.PlatformBlinkActive);

        yield return GunnerPlatformFresh(false); gun = game.Bosses[0];
        Check("death fixture starts pending Camo", gun.Tactics.TryStartCamo());
        gun.Health.TakeDamage(gun.Health.MaxHealth, game.Player.gameObject); yield return Frames(3);
        Check("death cancels pending relocation", !gun.Tactics.PlatformBlinkActive && gun.Tactics.PlatformBlinks == 0);
        game.MainMenu(); yield return Frames(3);
        Check("menu clears platform group and projectiles", Private<GameObject>(game, "stagePlatforms") == null && Projectile.Active.Count == 0);

        yield return TrainingFresh(null);
        Check("Training retains one platform and original simple policy", LayoutSupports().Length == 2
            && game.Bosses[0].IsTrainingEnemy && !game.Bosses[0].Tactics.PlatformPressureEnabled);
        for (int wave = 2; wave <= 3; wave++)
        {
            game.ConfigurePlaytestWave(wave); game.StartFight(); yield return Frames(4);
            Check("W" + wave + " retains original five-platform geometry and policy", LayoutSupports().Length == 6
                && game.Bosses.All(b => !b.Tactics.PlatformPressureEnabled) && game.Bosses[0].Navigation.SameLevelSpacing == 8.75f);
            game.Player.SetRuntimeHealth(10000, 10000);
            yield return Until(() => game.Phase == DuelPhase.Fight, 4); yield return Seconds(2);
            Check("W" + wave + " short live combat", game.Phase == DuelPhase.Fight && game.Bosses.Any(b => b.Actions.AttackId > 0));
        }
        game.MainMenu(); yield return Frames(3);
    }
}
#endif
