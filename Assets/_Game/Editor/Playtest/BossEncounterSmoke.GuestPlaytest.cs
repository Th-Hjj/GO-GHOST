#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    public static void RunMapGunner() => Start("map-gunner", false);
    public static void RunDashBodies() => Start("dash-bodies", false);
    public static void RunDuoCamoTell() => Start("duo-camo-tell", false);

    static IEnumerator DuoCamoTell()
    {
        Record("Controlled W3 fixture: sword remains alive but stops attacking, freeing the unchanged shared threat budget. Verify gunner reappearance can produce a real paid attack with full tells.");
        yield return GuestFresh(3, false);
        var gun = game.Bosses[0]; var sword = game.Bosses[1];
        gun.Coordinator.StopCombat(); sword.StopCombat();
        sword.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
        Position(sword.gameObject, new Vector2(13, LayoutSupports()[0].bounds.max.y + sword.Motor.Size.y * .5f + .025f));
        Check("duo Camo accepts with free shared budget", gun.Tactics.TryStartCamo());
        yield return Until(() => gun.Tactics.PlatformBlinks == 1, 2);
        Check("duo Camo relocates once", gun.Tactics.PlatformBlinks == 1);
        float arrival = Time.time; gun.Tactics.Enabled = true;
        yield return Until(() => gun.Actions.Ability != BossAbility.Camo && gun.Actions.Ability != BossAbility.None, 3);
        var ability = gun.Actions.Ability;
        Check("duo reappearance uses a visible preparation", !gun.IsCamo && gun.Actions.Phase == BossActionPhase.Prepare);
        int ammo = gun.Actions.Ammo;
        yield return Until(() => gun.Actions.Ammo < ammo, 3);
        Check("duo reappearance pays ammo and retains full tell", gun.Actions.Ammo < ammo
            && Time.time - arrival >= gun.Profile.Camo.Recovery + gun.Profile.Settings(ability).Prepare - .06f,
            "ability=" + ability + " elapsed=" + (Time.time - arrival));
        game.MainMenu(); yield return Frames(3);
    }

    static IEnumerator GuestFresh(int wave, bool policy)
    {
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        game.ConfigurePlaytestWave(wave); game.StartFight();
        foreach (var boss in game.Bosses) boss.Tactics.Enabled = policy;
        yield return Until(() => game.Phase == DuelPhase.Fight, 4);
        game.Player.SetRuntimeHealth(10000, 10000);
        yield return Seconds(.3f);
    }

    static IEnumerator MapGunner()
    {
        Record("Map-independent gunner: short natural W3 observation, followed by controlled Camo/ally occupancy fixtures. No balance or outcome claim.");
        yield return GuestFresh(3, true);
        var gun = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        var sword = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        var geometry = LayoutSupports().Select(c => c.bounds).ToArray();
        Check("W3 enables shared gunner policy only, on original five platforms", gun.Tactics.PlatformPressureEnabled
            && !sword.Tactics.PlatformPressureEnabled && geometry.Length == 6);
        var visited = new HashSet<Collider2D>();
        float end = Time.time + 22;
        int attack = -1; bool captured = false;
        while (Time.time < end)
        {
            if (gun.Motor.Grounded && gun.Navigation.TrySupport(gun.Position, gun.Motor.Size, out var on)
                && LayoutSupports().Skip(1).Contains(on)) visited.Add(on);
            if (attack != gun.Actions.AttackId)
            { attack = gun.Actions.AttackId; Record("OBS W3 gun=" + gun.Actions.Ability + " at=" + gun.Position + " duo=" + gun.Coordinator.Phase); }
            float remaining = end - Time.time;
            heldKeys = remaining < 15 && remaining > 13.6f ? new[] { Key.D } : Array.Empty<Key>();
            MouseAt(gun.Position, remaining < 12 && remaining > 10);
            if (!captured && visited.Count >= 2)
            { captured = true; yield return LayoutCapture("w3-map-gunner"); }
            yield return null;
        }
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        Check("natural W3 changes perches with physical jumps and concealed relocation", visited.Count >= 2
            && gun.Tactics.PlatformMoves > 0 && gun.Tactics.PlatformBlinks > 0,
            "supports=" + visited.Count + " moves=" + gun.Tactics.PlatformMoves + " blinks=" + gun.Tactics.PlatformBlinks
            + " platform attacks=" + gun.Tactics.PlatformAttacks);
        Check("both Bosses still act and original map remains unchanged", gun.Actions.AttackId > 0 && sword.Actions.AttackId > 0
            && LayoutSupports().Select(c => c.bounds).SequenceEqual(geometry) && game.Phase == DuelPhase.Fight);

        yield return GuestFresh(3, false);
        gun = game.Bosses[0]; sword = game.Bosses[1]; gun.Coordinator.StopCombat();
        Check("W3 Camo commits using the ordinary executor", gun.Tactics.TryStartCamo());
        var committed = Private<BossPlan>(gun.Tactics, "camoPlan");
        Check("W3 blink has a frozen supported destination clear of ally", committed != null
            && !gun.Coordinator.Occupied(gun, committed.Destination));
        yield return Until(() => gun.Actions.Phase == BossActionPhase.Active, 1);
        checkingPause = true; game.SetPause(true); int blinks = gun.Tactics.PlatformBlinks;
        yield return RealSeconds(.2);
        Check("W3 pause freezes pending Camo", gun.Tactics.PlatformBlinks == blinks);
        game.SetPause(false); checkingPause = false;
        yield return Until(() => gun.Tactics.PlatformBlinks > 0, 1.5f);
        Check("W3 hidden relocation reaches its committed platform", gun.Tactics.PlatformBlinks == 1 && committed != null
            && Vector2.Distance(gun.Position, committed.Destination) < .1f);
        yield return Seconds(.4f);
        Check("W3 arrival remains supported", gun.Motor.Grounded);

        yield return GuestFresh(3, false);
        gun = game.Bosses[0]; sword = game.Bosses[1]; gun.Coordinator.StopCombat();
        Check("ally occupancy fixture commits Camo", gun.Tactics.TryStartCamo());
        committed = Private<BossPlan>(gun.Tactics, "camoPlan");
        if (committed != null)
        {
            sword.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
            Position(sword.gameObject, committed.Destination);
            sword.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            yield return Until(() => !gun.Actions.Busy, 2);
            Check("new ally occupant cancels blink without retargeting", gun.Tactics.PlatformBlinks == 0 && !gun.Tactics.PlatformBlinkActive);
        }
        game.MainMenu(); yield return Frames(3);
        Check("menu cleans actors and temporary platforms", game.Bosses.Count == 0 && Private<GameObject>(game, "stagePlatforms") == null);
        yield return GuestFresh(2, true); yield return Seconds(3);
        Check("W2 still uses original five platforms and sword combat", LayoutSupports().Length == 6 && !game.Bosses[0].Tactics.PlatformPressureEnabled
            && game.Bosses[0].Actions.AttackId > 0);
        game.MainMenu(); yield return Frames(3);
    }

    static IEnumerator DashBodies()
    {
        Record("Real Rigidbody dash/body/wall checks. Controlled placement and stationary Boss bodies; same manual and rule-facing executor, unchanged tuning.");
        yield return GuestFresh(3, false);
        foreach (var boss in game.Bosses)
        {
            boss.StopCombat();
            var rb = boss.GetComponent<Rigidbody2D>(); rb.interpolation = RigidbodyInterpolation2D.None;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        game.Bosses[0].Coordinator.StopCombat();
        var player = game.Player; var dodge = game.Dodge; var shape = player.GetComponent<Collider2D>();
        var body = player.GetComponent<Rigidbody2D>(); body.interpolation = RigidbodyInterpolation2D.None;
        float floor = LayoutSupports()[0].bounds.max.y;
        float py = floor + shape.bounds.extents.y + .025f;
        for (int i = 0; i < game.Bosses.Count; i++)
            Position(game.Bosses[i].gameObject, new Vector2(-.3f + i, floor + game.Bosses[i].Motor.Size.y * .5f + .025f));
        Position(player.gameObject, new Vector2(-1.3f, py)); yield return Seconds(.2f);
        Check("only registered Boss bodies are eligible", game.Bosses.All(b => dodge.CanPassThroughBody(b.Body))
            && !dodge.CanPassThroughBody(LayoutSupports()[0]));
        Check("normal movement starts with solid Boss collision", game.Bosses.All(b => !Physics2D.GetIgnoreCollision(shape, b.Body)));
        float start = shape.bounds.center.x;
        heldKeys = new[] { Key.D, Key.LeftShift };
        yield return Until(() => dodge.IsDodging, .3f); heldKeys = Array.Empty<Key>();
        Check("manual Dash temporarily ignores both Boss bodies", dodge.IsDodging && game.Bosses.All(b => Physics2D.GetIgnoreCollision(shape, b.Body)));
        yield return Seconds(.3f);
        Check("manual Dash physically crosses both Bosses without pushing them", shape.bounds.center.x > 1.5f
            && Mathf.Abs(shape.bounds.center.x - start - dodge.Distance) < .12f
            && Mathf.Abs(game.Bosses[0].Position.x + .3f) < .02f, "travel=" + (shape.bounds.center.x - start));
        Check("body collision and immunity restore after Dash", !dodge.IsDodging && !player.IsInvulnerable
            && game.Bosses.All(b => !Physics2D.GetIgnoreCollision(shape, b.Body)));
        Check("Dash tuning stays 3 units / .18 movement / .20 immunity / 2 cooldown", dodge.Distance == 3
            && Mathf.Abs(dodge.Duration - .18f) < .001f && dodge.InvulnerabilityDuration == .2f && dodge.DodgeStartInterval == 2);

        dodge.ResetCooldown(); Position(player.gameObject, new Vector2(1.7f, py)); yield return Frames(3);
        Check("explicit rule-facing direction accepts reverse Dash", dodge.TryDodge(-1));
        yield return Seconds(.3f);
        Check("reverse Dash crosses both Boss bodies", shape.bounds.center.x < -1.15f);

        dodge.ResetCooldown(); Position(player.gameObject, new Vector2(-1.3f, py + .45f)); yield return Frames(1);
        Check("air Dash accepts the same body pass-through", dodge.TryDodge(1)); yield return Seconds(.3f);
        Check("air Dash crosses bodies while gravity remains active", shape.bounds.center.x > 1.5f && shape.bounds.center.y < py + .4f);

        dodge.ResetCooldown(); Position(player.gameObject, new Vector2(game.ArenaBounds.max.x - .8f, py)); yield return Frames(2);
        float wallStart = shape.bounds.center.x; Check("wall fixture accepts Dash", dodge.TryDodge(1));
        yield return Seconds(.3f);
        Check("terrain wall still blocks Dash", shape.bounds.max.x <= game.ArenaBounds.max.x + .03f && shape.bounds.center.x - wallStart < 1);

        dodge.ResetCooldown(); Position(player.gameObject, new Vector2(-2.8f, py)); yield return Frames(2);
        Check("overlap-end fixture accepts Dash", dodge.TryDodge(1)); yield return Seconds(.3f);
        Check("ending inside Boss grants no extra immunity or movement", !dodge.IsDodging && !player.IsInvulnerable && Mathf.Abs(body.linearVelocity.x) < .05f);
        heldKeys = new[] { Key.A }; yield return Seconds(.35f); heldKeys = Array.Empty<Key>();
        Check("body collision restores after overlapping endpoint separates", game.Bosses.All(b => !Physics2D.GetIgnoreCollision(shape, b.Body)));
        dodge.ResetCooldown(); Check("cancellation fixture accepts Dash", dodge.TryDodge(1)); dodge.CancelDodge();
        Check("cancel clears all owned ignore pairs immediately", !dodge.IsDodging && game.Bosses.All(b => !Physics2D.GetIgnoreCollision(shape, b.Body)));
        game.MainMenu(); yield return Frames(3);
        Check("menu removes registered actor bodies", game.Player == null && game.Bosses.Count == 0);
    }
}
#endif
