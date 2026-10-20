#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    static Collider2D[] BehaviorLeftTiers()
        => LayoutSupports().Where(c => PlayerMovement.IsDropThroughSupport(c) && c.bounds.center.x < -.1f)
            .OrderBy(c => c.bounds.max.y).ToArray();

    static IEnumerator BehaviorPlatforms()
    {
        Record("Focused formal-scene physics with virtual input. Only initial actor placement and stopped Boss policies are fixtures; no authored geometry changes.");
        yield return AlphaFresh(3, d => V2Rule(d, new ConditionalRule { Condition = 4, Action = "D04" }));
        var player = game.Player;
        var movement = player.GetComponent<PlayerMovement>();
        var shape = player.GetComponent<Collider2D>();
        var body = player.GetComponent<Rigidbody2D>();
        body.interpolation = RigidbodyInterpolation2D.None;
        float floor = LayoutSupports()[0].bounds.max.y;
        float py = floor + shape.bounds.extents.y + .025f;
        for (int i = 0; i < game.Bosses.Count; i++)
        {
            var boss = game.Bosses[i];
            var bossBody = boss.GetComponent<Rigidbody2D>();
            bossBody.interpolation = RigidbodyInterpolation2D.None;
            bossBody.constraints = RigidbodyConstraints2D.FreezeAll;
            Position(boss.gameObject, new Vector2(-.3f + i, floor + boss.Motor.Size.y * .5f + .025f));
        }
        Position(player.gameObject, new Vector2(-1.3f, py));
        MouseAt(new Vector2(8, py), false);
        yield return Seconds(.15f);
        Check("Automatic Dodge query ignores exactly registered Boss bodies",
            game.Dodge.AvailableTravel(1, game.Dodge.Distance) > 2.9f
            && game.Bosses.All(b => game.Dodge.CanPassThroughBody(b.Body))
            && !game.Dodge.CanPassThroughBody(LayoutSupports()[0]));
        float before = shape.bounds.center.x;
        PlatformTrigger();
        yield return Until(() => game.Dodge.IsDodging, .4f);
        Check("Conditional Dodge accepts and installs its real pass-through pairs", game.Rules.AcceptedDodges == 1
            && game.Bosses.All(b => Physics2D.GetIgnoreCollision(shape, b.Body)));
        yield return Seconds(.35f);
        Check("Conditional Dodge physically crosses both Boss bodies at the unchanged distance",
            shape.bounds.center.x > 1.5f && Mathf.Abs(shape.bounds.center.x - before - game.Dodge.Distance) < .12f,
            "travel=" + (shape.bounds.center.x - before));
        Check("Dodge restores the ordinary solid body pairs",
            game.Bosses.All(b => !Physics2D.GetIgnoreCollision(shape, b.Body)) && !game.Dodge.IsDodging);
        Position(player.gameObject, new Vector2(-1.3f, py));
        yield return Frames(3);
        heldKeys = new[] { Key.D };
        yield return Seconds(.45f);
        heldKeys = Array.Empty<Key>();
        Check("Ordinary movement still collides with the Boss body", shape.bounds.max.x <= game.Bosses[0].Body.bounds.min.x + .04f);

        yield return AlphaFresh(1, d => V2Rule(d, new ConditionalRule { Condition = 4, Action = "D04" }));
        shape = game.Player.GetComponent<Collider2D>();
        py = LayoutSupports()[0].bounds.max.y + shape.bounds.extents.y + .025f;
        Position(game.Player.gameObject, new Vector2(game.ArenaBounds.max.x - .8f, py));
        MouseAt(new Vector2(game.ArenaBounds.max.x + 5, py), false);
        yield return Seconds(.12f);
        before = shape.bounds.center.x;
        Check("Automatic Dodge admission retains the terrain wall", game.Dodge.AvailableTravel(1, game.Dodge.Distance) < .6f);
        PlatformTrigger();
        yield return Seconds(.35f);
        Check("Automatic Dodge remains physically blocked by terrain", game.Rules.AcceptedDodges == 1
            && shape.bounds.max.x <= game.ArenaBounds.max.x + .03f && shape.bounds.center.x - before < .6f);

        yield return AlphaFresh(1, null);
        movement = game.Player.GetComponent<PlayerMovement>();
        shape = game.Player.GetComponent<Collider2D>();
        var tiers = BehaviorLeftTiers();
        float x = Mathf.Max(tiers[0].bounds.min.x, tiers[2].bounds.min.x) + shape.bounds.extents.x + .3f;
        PlatformPlace(tiers[2], x);
        yield return Seconds(.15f);
        int jumps = 0, apexes = 0;
        Action onJump = () => jumps++;
        Action onApex = () => apexes++;
        movement.JumpStarted += onJump;
        movement.JumpApexReached += onApex;
        heldKeys = new[] { Key.S };
        yield return Until(() => movement.DropThroughCount == 1, .3f);
        Check("One S press uses only the current support collision pair", movement.DropThroughSupport == tiers[2]
            && Physics2D.GetIgnoreCollision(shape, tiers[2]) && !Physics2D.GetIgnoreCollision(shape, tiers[1]));
        checkingPause = true;
        game.SetPause(true);
        var frozen = game.Player.transform.position;
        yield return RealSeconds(.15);
        Check("Pause freezes an in-flight drop and preserves its pair", game.Player.transform.position == frozen
            && movement.DropThroughSupport == tiers[2]);
        game.SetPause(false);
        checkingPause = false;
        yield return Seconds(.9f);
        Check("Held S lands on the next platform without dropping through another", movement.IsGrounded
            && movement.DropThroughCount == 1 && Mathf.Abs(shape.bounds.min.y - tiers[1].bounds.max.y) < .09f);
        Check("Drop separation restores collision and creates no jump or apex", movement.DropThroughSupport == null
            && !Physics2D.GetIgnoreCollision(shape, tiers[2]) && jumps == 0 && apexes == 0);
        for (int next = 0; next < 2; next++)
        {
            heldKeys = Array.Empty<Key>();
            yield return Frames(3);
            heldKeys = new[] { Key.S };
            yield return Seconds(.8f);
        }
        heldKeys = Array.Empty<Key>();
        yield return Frames(3);
        int drops = movement.DropThroughCount;
        heldKeys = new[] { Key.S };
        yield return Seconds(.3f);
        heldKeys = Array.Empty<Key>();
        Check("Fresh presses descend one support each and cannot cross the floor", drops == 3
            && movement.DropThroughCount == drops && movement.IsGrounded
            && Mathf.Abs(shape.bounds.min.y - LayoutSupports()[0].bounds.max.y) < .09f
            && !Physics2D.GetIgnoreCollision(shape, LayoutSupports()[0]));
        movement.JumpStarted -= onJump;
        movement.JumpApexReached -= onApex;

        // A higher-priority ordinary movement row becomes true only after the transfer starts.
        yield return AlphaFresh(1, d => {
            V2Rule(d, V2Ground("M10", GroundStateOption.Airborne));
            V2Rule(d, new ConditionalRule { Condition = 6, AdditionalCondition = 17, Action = "J04",
                PlatformDirection = PlatformDirectionOption.Lower });
        });
        movement = game.Player.GetComponent<PlayerMovement>();
        shape = game.Player.GetComponent<Collider2D>();
        tiers = BehaviorLeftTiers();
        x = Mathf.Max(tiers[0].bounds.min.x, tiers[2].bounds.min.x) + shape.bounds.extents.x + .3f;
        PlatformPlace(tiers[2], x);
        yield return Seconds(.15f);
        mouseHeld = true;
        MouseAt(new Vector2(0, shape.bounds.center.y), true);
        yield return Frames(3);
        jumps = apexes = 0;
        movement.JumpStarted += onJump;
        movement.JumpApexReached += onApex;
        PlatformTrigger();
        yield return Until(() => movement.DropThroughCount > 0, .4f);
        var destination = game.Rules.PlatformDestination;
        mouseHeld = false;
        int retargets = 0;
        float until = Time.time + 2;
        while (game.Rules.PlatformTransitionsCompleted == 0 && Time.time < until)
        {
            if (game.Rules.PlatformDestination != null && game.Rules.PlatformDestination != destination) retargets++;
            yield return null;
        }
        Check("Automatic Lower uses the same S capability and freezes the next real support",
            movement.DropThroughCount == 1 && destination == tiers[1] && retargets == 0
            && game.Rules.PlatformTransitionsCompleted == 1);
        Check("An accepted transfer survives optional-state loss and a higher ordinary Move row",
            game.Rules.PlatformTransitionsCompleted == 1 && Mathf.Abs(shape.bounds.min.y - tiers[1].bounds.max.y) < .09f);
        Check("Automatic drop creates real landing without fake Jump Started or Apex",
            movement.IsGrounded && jumps == 0 && apexes == 0 && game.Rules.AcceptedJumps == 0
            && Private<IList>(game.Rules, "events").Cast<object>().Any(n =>
                (int)n.GetType().GetField("Condition").GetValue(n) == 3));
        yield return Seconds(.55f);
        Check("Completed event transfer releases both Legs channels and cannot replay",
            game.Rules.PlatformDestination == null && !game.Rules.Ownership.JumpOwned
            && game.Rules.PlatformTransitionsCompleted == 1);
        movement.JumpStarted -= onJump;
        movement.JumpApexReached -= onApex;

        yield return AlphaFresh(1, null);
        movement = game.Player.GetComponent<PlayerMovement>();
        shape = game.Player.GetComponent<Collider2D>();
        tiers = BehaviorLeftTiers();
        PlatformPlace(tiers[2], tiers[2].bounds.center.x);
        yield return Seconds(.15f);
        Check("Lifecycle fixture starts real drop", movement.TryDropThrough(tiers[2]));
        movement.enabled = false;
        Check("Disable immediately restores its owned platform collision", movement.DropThroughSupport == null
            && !Physics2D.GetIgnoreCollision(shape, tiers[2]));
        game.MainMenu();
        yield return Frames(3);
        Check("Menu disposes controller, input actor and temporary platform layout",
            game.Rules == null && game.Player == null && Private<GameObject>(game, "stagePlatforms") == null);
    }
}
#endif
