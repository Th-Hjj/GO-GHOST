#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    static IEnumerator DirectionalDash()
    {
        Record("CONTROLLED directional dash: fixed initial placements and stationary elevated targets; real executor, physics and collision. No balance proof.");
        bool captured = false;
        for (int sample = 0; sample < 3; sample++)
        {
            yield return Fresh(2); StopPolicies();
            var sword = game.Bosses[0]; var action = sword.Actions;
            float x = sample == 1 ? 13 : -13;
            OnFloor(sword.gameObject, sword.Navigation, x);
            Position(game.Player.gameObject, new Vector2(sample == 0 ? -11 : sample == 1 ? 11 : -13, 1.2f));
            game.Player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            yield return Until(() => sword.Motor.Grounded, 1); yield return Seconds(.25f);
            int hp = game.Player.CurrentHealth;
            Check("directional target legal " + sample, sword.Tactics.LegalAt(BossAbility.DrawDash, sword.Position, sword.Perception.Facts, out _));
            if (sample == 0)
            {
                sword.Tactics.Enabled = true;
                yield return Until(() => action.Busy, .4f);
                sword.Tactics.Enabled = false;
                Check("normal policy selects diagonal Draw Dash", action.Ability == BossAbility.DrawDash);
            }
            else Check("normal executor accepts directional Draw Dash " + sample, action.TryStart(BossAbility.DrawDash, sword.Perception.Facts.Position));
            yield return Until(() => action.Phase == BossActionPhase.Locked, .5f);
            Vector2 origin = action.Origin, end = action.End, direction = action.Aim;
            Check("locked direction reaches upward target " + sample, direction.y > .5f && (sample == 2 ? Mathf.Abs(direction.x) < .01f : Mathf.Abs(direction.x) > .3f));
            Check("dash length stays within existing budget", Vector2.Distance(origin, end) <= sword.Navigation.SameLevelSpacing + .01f);
            bool observed = false, straight = true, bounded = true;
            float peakSpeed = 0;
            float limit = Time.time + 1.2f;
            while (Time.time < limit && action.Ability == BossAbility.DrawDash && action.Phase != BossActionPhase.Recovery)
            {
                if (sword.Motor.DashActive)
                {
                    observed = true;
                    straight &= Mathf.Abs(Vector3.Cross(direction, sword.Position - origin).z) < .04f;
                    bounded &= Vector2.Dot(sword.Position - origin, direction) <= Vector2.Distance(origin, end) + .04f;
                    peakSpeed = Mathf.Max(peakSpeed, Vector2.Dot(sword.Velocity, direction));
                    if (sample == 0 && !captured && sword.Position.y > origin.y + .6f)
                    {
                        System.IO.Directory.CreateDirectory("Logs/DirectionalDash");
                        ScreenCapture.CaptureScreenshot("Logs/DirectionalDash/up-right.png");
                        captured = true;
                    }
                }
                yield return null;
            }
            Check("real directional drive observed " + sample, observed && straight && bounded && Mathf.Abs(peakSpeed - sword.Profile.DashSpeed) < 1,
                "straight=" + straight + " bounded=" + bounded + " peakSpeed=" + peakSpeed);
            Check("directional body contact damages exactly once " + sample, hp - game.Player.CurrentHealth == sword.Profile.DrawDash.Damage,
                "damage=" + (hp - game.Player.CurrentHealth) + " aim=" + direction + " position=" + sword.Position);
            Check("upward drive stops without extra launch velocity", !sword.Motor.DashActive && sword.Velocity.y <= .1f);
            Check("airborne endpoint does not branch into Return Cut", action.Started[(int)BossAbility.ReturnCut] == 0);
            Check("cooldown preserved", !action.Ready(BossAbility.DrawDash));
            yield return Until(() => sword.Motor.Grounded, 2);
            Check("ordinary gravity returns sword to support", sword.Motor.Grounded);
        }

        yield return Fresh(2); StopPolicies();
        var actor = game.Bosses[0]; var nav = actor.Navigation;
        OnFloor(actor.gameObject, nav, -13); OnFloor(game.Player.gameObject, nav, -7);
        yield return Until(() => actor.Motor.Grounded, 1); yield return Seconds(.25f);
        Check("grounded horizontal route still legal", actor.Actions.TryStart(BossAbility.DrawDash, actor.Perception.Facts.Position));
        yield return Until(() => actor.Actions.Phase == BossActionPhase.Locked, .5f);
        Vector2 lockedAim = actor.Actions.Aim, lockedEnd = actor.Actions.End;
        Position(game.Player.gameObject, new Vector2(-11, 2));
        game.Player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        yield return Until(() => actor.Actions.Phase == BossActionPhase.Active, .4f);
        Check("target movement after lock cannot redirect dash", actor.Actions.Aim == lockedAim && actor.Actions.End == lockedEnd);
        yield return Until(() => !actor.Actions.Busy, 1.5f);
        yield return Until(() => actor.Actions.Ready(BossAbility.DrawDash), 3);
        Check("real jump starts", actor.Motor.TryJump());
        yield return Until(() => !actor.Motor.Grounded, .25f);
        Check("airborne Draw Dash start still rejected", !actor.Actions.TryStart(BossAbility.DrawDash, game.Player.transform.position));

        // Directional geometry clips against the same world surfaces; no floor/platform drop-through.
        Vector2 testStart = new Vector2(-13, 2);
        bool clear = nav.TryDirectionalAttack(testStart, testStart + new Vector2(3, -4), actor.Motor.Size, out var downward);
        Check("free-space downward diagonal remains a straight legal segment", clear && downward.y < testStart.y
            && Mathf.Abs(Vector3.Cross((new Vector2(3, -4)).normalized, downward - testStart).z) < .02f);
        nav.TryDirectionalAttack(testStart, testStart + new Vector2(-8, 6), actor.Motor.Size, out var wallEnd);
        Check("diagonal arena clipping preserves direction", Mathf.Abs(Vector3.Cross((new Vector2(-8, 6)).normalized, wallEnd - testStart).z) < .02f
            && wallEnd.x >= nav.ArenaBounds.min.x + actor.Motor.Size.x * .5f);
        OnFloor(actor.gameObject, nav, -13); yield return Seconds(.1f);
        Check("ground still blocks downward start", !nav.TryDirectionalAttack(actor.Position, actor.Position + Vector2.down * 4, actor.Motor.Size, out _));

        for (int wave = 2; wave <= 3; wave++)
        {
            yield return Fresh(wave); MouseAt(game.Bosses[0].Position, true); yield return Seconds(3); mouseHeld = false;
            Check("short W" + wave + " combat progresses", game.Phase == DuelPhase.Fight && game.FightClock > 2);
        }
        game.MainMenu(); yield return Frames(2);
        Check("cleanup releases actors and projectiles", Projectile.Active.Count == 0 && UnityEngine.Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length == 0);
    }
}
#endif
