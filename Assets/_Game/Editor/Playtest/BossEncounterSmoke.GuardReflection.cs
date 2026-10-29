#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    static Projectile GuardShot(BossAgent sword, Vector2 position, Vector2 direction, float speed = 18)
    {
        var shot = Object.Instantiate(sword.ProjectilePrefab, position, Quaternion.identity);
        shot.Launch(direction, speed, 12, game.Player.gameObject, actor: ProjectileActor.Player);
        return shot;
    }

    static IEnumerator GuardReflection()
    {
        Record("CONTROLLED guard contacts and poses; real projectile physics and normal action clock. No balance proof.");
        foreach (int facing in new[] { 1, -1 })
        {
            yield return Fresh(2); StopPolicies(); AlphaPlace(facing * 5, 0);
            var sword = game.Bosses[0]; var action = sword.Actions;
            yield return Until(() => sword.Motor.Grounded, 1);
            yield return Seconds(.2f);
            sword.Motor.SetMove(facing); yield return Seconds(.12f);
            float startX = sword.Position.x;
            int hp = sword.Health.CurrentHealth, playerHp = game.Player.CurrentHealth;
            Check("guard accepted facing " + facing, action.TryStart(BossAbility.Guard, game.Player.transform.position));
            yield return Until(() => action.Phase == BossActionPhase.Active, .5f);
            Check("guard preparation stops horizontal movement", Mathf.Abs(sword.Position.x - startX) < .015f && Mathf.Abs(sword.Velocity.x) < .001f);
            yield return Frames(1); // Allow LateUpdate to apply the newly active pose.
            var blade = Private<SpriteRenderer[]>(sword.View, "parts")[(int)BossSilhouetteView.Part.Weapon].transform;
            Vector3 tipDirection = blade.TransformVector(Vector3.right).normalized;
            Check("guard blade points vertically down facing " + facing, Vector3.Dot(tipDirection, Vector3.down) > .999f);
            var shot = GuardShot(sword, sword.Position + Vector2.right * facing * .9f, Vector2.left * facing);
            float life = Private<float>(shot, "remainingLife");
            yield return Until(() => shot == null || shot.Deflected, .2f);
            Check("front contact reflects same live projectile", shot != null && shot.Deflected && shot.Owner == sword.gameObject
                && shot.OwnerLayer == sword.gameObject.layer && shot.Actor == ProjectileActor.Environment && action.Guarded == 1);
            if (shot != null)
            {
                Check("reflection retains speed damage and lifetime", Vector2.Distance(shot.GetComponent<Rigidbody2D>().linearVelocity, Vector2.right * facing * 18) < .01f
                    && shot.Damage == 12 && Private<float>(shot, "remainingLife") <= life);
                Check("reflected bullet carries sword color", shot.GetComponent<SpriteRenderer>().color == NeuralPalette.Swordsman);
                Vector2 frozenShot = shot.transform.position; float frozenTime = Time.time;
                checkingPause = true; game.SetPause(true);
                yield return RealSeconds(.12);
                Check("pause freezes guard and reflected bullet", Time.time == frozenTime && (Vector2)shot.transform.position == frozenShot);
                game.SetPause(false); checkingPause = false;
            }
            if (facing == 1)
            {
                checkingPause = true; game.SetPause(true); SetAlphaGameSize(1920, 1080);
                yield return Frames(3);
                System.IO.Directory.CreateDirectory("Logs/GuardReflection");
                ScreenCapture.CaptureScreenshot("Logs/GuardReflection/guard-down.png");
                yield return Frames(3);
                game.SetPause(false); checkingPause = false;
            }
            yield return Until(() => game.Player.CurrentHealth < playerHp, .5f);
            Check("returned bullet damages player without damaging sword", game.Player.CurrentHealth == playerHp - 12 && sword.Health.CurrentHealth == hp);
            Check("guard remains stationary through active", Mathf.Abs(sword.Position.x - startX) < .015f);
            if (action.Phase == BossActionPhase.Active)
            {
                GuardShot(sword, sword.Position - Vector2.right * facing * .9f, Vector2.right * facing);
                yield return Until(() => sword.Health.CurrentHealth < hp, .15f);
                Check("rear projectile bypasses reflection", sword.Health.CurrentHealth == hp - 12 && action.Guarded == 1);
            }
            else Check("rear-contact fixture still in guard window", false);
            yield return Until(() => !action.Busy, 1);
            Check("guard recovery has no footwork", Mathf.Abs(sword.Position.x - startX) < .015f && !action.RecoveryStepping);
            Check("guard retains cooldown", !action.Ready(BossAbility.Guard));
            sword.Motor.SetMove(facing); yield return Seconds(.12f);
            Check("walking resumes after guard", Mathf.Abs(sword.Position.x - startX) > .05f);
        }

        yield return Fresh(2); StopPolicies(); AlphaPlace(2, 0);
        var capacitySword = game.Bosses[0]; var capacityAction = capacitySword.Actions;
        game.Player.SetRuntimeHealth(10000, 10000);
        yield return Until(() => capacitySword.Motor.Grounded, 1); yield return Seconds(.2f);
        Check("full-window guard accepted at close range", capacityAction.TryStart(BossAbility.Guard, game.Player.transform.position));
        yield return Until(() => capacityAction.Phase == BossActionPhase.Active, .5f);
        float guardEnd = Private<float>(capacityAction, "phaseUntil");
        int capacityHp = capacitySword.Health.CurrentHealth;
        var volley = Enumerable.Range(0, 8).Select(i => GuardShot(capacitySword, capacitySword.Position + Vector2.right * .9f, Vector2.left)).ToArray();
        yield return Until(() => capacityAction.Guarded == 8, .2f);
        Check("eight same-step contacts all reflect without exhausting guard", capacityAction.Guarded == 8
            && capacityAction.Phase == BossActionPhase.Active && volley.All(s => s != null && s.Deflected));
        yield return Until(() => Time.time >= guardEnd - .18f, .7f);
        Check("close melee opportunity does not end guard early", capacityAction.Phase == BossActionPhase.Active
            && capacityAction.Ready(BossAbility.Rhythm)
            && capacitySword.Tactics.LegalAt(BossAbility.Rhythm, capacitySword.Position, capacitySword.Perception.Facts, out _));
        var late = GuardShot(capacitySword, capacitySword.Position + Vector2.right * .9f, Vector2.left);
        yield return Until(() => capacityAction.Guarded == 9, .1f);
        Check("late bullet still reflects before the fixed deadline", capacityAction.Guarded == 9 && late != null && late.Deflected
            && capacitySword.Health.CurrentHealth == capacityHp && Private<float>(capacityAction, "phaseUntil") == guardEnd);
        yield return Until(() => capacityAction.Phase == BossActionPhase.Recovery, .3f);
        Check("guard recovers at its original deadline", Time.time >= guardEnd && Time.time < guardEnd + .08f);
        GuardShot(capacitySword, capacitySword.Position + Vector2.right * .9f, Vector2.left);
        yield return Until(() => capacitySword.Health.CurrentHealth < capacityHp, .2f);
        Check("expired guard allows next bullet damage", capacitySword.Health.CurrentHealth == capacityHp - 12);

        // Reflected ownership damages the other Boss and still stops at its body.
        yield return Fresh(3); StopPolicies();
        var sword3 = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        var gun3 = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        OnFloor(sword3.gameObject, sword3.Navigation, 0); OnFloor(gun3.gameObject, sword3.Navigation, 2.5f);
        OnFloor(game.Player.gameObject, sword3.Navigation, 6);
        yield return Until(() => sword3.Motor.Grounded, 1); yield return Seconds(.2f);
        int gunHp = gun3.Health.CurrentHealth, duoPlayerHp = game.Player.CurrentHealth;
        Check("duo guard accepted", sword3.Actions.TryStart(BossAbility.Guard, game.Player.transform.position));
        yield return Until(() => sword3.Actions.Phase == BossActionPhase.Active, .5f);
        var allied = GuardShot(sword3, sword3.Position + Vector2.right * .9f, Vector2.left);
        yield return Until(() => allied == null, .4f);
        Check("reflected bullet damages allied boss and stops", allied == null && sword3.Actions.Guarded == 1
            && gun3.Health.CurrentHealth == gunHp - 12 && game.Player.CurrentHealth == duoPlayerHp);
        game.MainMenu(); yield return Frames(2);
        Check("menu cleans reflected projectiles and actors", Projectile.Active.Count == 0 && Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length == 0);

        for (int wave = 1; wave <= 3; wave++)
        {
            yield return Fresh(wave); MouseAt(game.Bosses[0].Position, true); yield return Seconds(3);
            mouseHeld = false;
            Check("short W" + wave + " combat progresses", game.Phase == DuelPhase.Fight && game.FightClock > 2 && game.Rifle.ShotsFired > 0);
        }
        Record("Controlled defeat/retry checks lifecycle only.");
        game.Player.TakeDamage(100000, null); yield return Until(() => game.Phase == DuelPhase.Failed, .5f);
        Check("defeat resolves", game.Phase == DuelPhase.Failed);
        game.Reconfigure(); yield return Frames(2); game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        Check("retry starts fresh combat", game.Phase == DuelPhase.Fight && game.Bosses.All(b => b.Actions.Guarded == 0));
    }
}
#endif
