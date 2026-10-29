#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Boss Friendly Fire")]
    public static void RunFriendlyFire() => Start("friendly-fire", false);

    static IEnumerator FriendlyFire()
    {
        Record("CONTROLLED real contacts and attack windows. Repositioned actors prove damage/lifecycle, not balance.");
        yield return TeamBlocking();
        yield return Fresh(3); StopPolicies();
        var gun = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        var sword = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        OnFloor(gun.gameObject, gun.Navigation, -8); OnFloor(sword.gameObject, gun.Navigation, -2);
        OnFloor(game.Player.gameObject, gun.Navigation, 6); yield return Seconds(.3f);
        int gunHp = gun.Health.CurrentHealth, swordHp = sword.Health.CurrentHealth;
        var self = Object.Instantiate(gun.ProjectilePrefab, gun.Position, Quaternion.identity);
        self.Launch(Vector2.left, 18, 12, gun.gameObject, friendlyFire:true);
        yield return Seconds(.15f);
        Check("Boss projectile ignores its own body", gun.Health.CurrentHealth == gunHp);
        if(self != null) self.Retire();
        var shot = Object.Instantiate(gun.ProjectilePrefab, gun.Position + Vector2.right, Quaternion.identity);
        shot.Launch(Vector2.right, 18, 12, gun.gameObject, friendlyFire:true);
        checkingPause = true; game.SetPause(true); float pauseTime = Time.time; Vector3 pausePosition = shot.transform.position;
        yield return RealSeconds(.15);
        Check("pause freezes friendly projectile and damage", Time.time == pauseTime && shot != null
            && shot.transform.position == pausePosition && sword.Health.CurrentHealth == swordHp);
        game.SetPause(false); checkingPause = false;
        yield return Until(() => shot == null, .7f);
        Check("resumed friendly bullet damages exactly once", sword.Health.CurrentHealth == swordHp - 12);

        // Exercise the actual gun executor's opt-in, including its ordinary burst damage.
        OnFloor(sword.gameObject, gun.Navigation, 10); yield return Seconds(.2f);
        swordHp = sword.Health.CurrentHealth;
        Check("real Burst accepts", gun.Actions.TryStart(BossAbility.Burst, game.Player.transform.position));
        yield return Until(() => Projectile.Active.Any(p => p.Owner == gun.gameObject), 1.5f);
        Check("real gun bullets enable friendly fire", Projectile.Active.Any(p => p.Owner == gun.gameObject && p.FriendlyFire));
        OnFloor(sword.gameObject, gun.Navigation, -2);
        yield return Until(() => sword.Health.CurrentHealth < swordHp, .8f);
        Check("real Burst damages partner", sword.Health.CurrentHealth <= swordHp - gun.Profile.Burst.Damage);

        yield return Fresh(3); StopPolicies();
        gun = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        sword = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        OnFloor(sword.gameObject, sword.Navigation, 0); OnFloor(gun.gameObject, sword.Navigation, 3);
        OnFloor(game.Player.gameObject, sword.Navigation, 7); yield return Seconds(.3f);
        gunHp = gun.Health.CurrentHealth; swordHp = sword.Health.CurrentHealth;
        Check("Guard accepts for friendly reflection", sword.Actions.TryStart(BossAbility.Guard, game.Player.transform.position));
        yield return Until(() => sword.Actions.Phase == BossActionPhase.Active, .6f);
        var reflected = Object.Instantiate(gun.ProjectilePrefab, sword.Position + Vector2.right * .9f, Quaternion.identity);
        reflected.Launch(Vector2.left, 18, 12, gun.gameObject, friendlyFire:true);
        yield return Until(() => reflected == null, .5f);
        Check("Guard reflects friendly bullet into its original shooter", reflected == null && sword.Actions.Guarded == 1
            && sword.Health.CurrentHealth == swordHp && gun.Health.CurrentHealth == gunHp - 12);

        // Both player and partner occupy the real slash arc; each gets one hit per beat.
        yield return Fresh(3); StopPolicies();
        gun = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        sword = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        OnFloor(sword.gameObject, sword.Navigation, -8); OnFloor(game.Player.gameObject, sword.Navigation, -5.5f);
        OnFloor(gun.gameObject, sword.Navigation, 7); yield return Seconds(.3f);
        Check("real close phrase accepts", sword.Actions.TryStartSword(BossAbility.SwordDuel));
        yield return Until(() => sword.Actions.Phase == BossActionPhase.Locked, .8f);
        OnFloor(gun.gameObject, sword.Navigation, -6.5f);
        gunHp = gun.Health.CurrentHealth; int playerHp = game.Player.CurrentHealth; swordHp = sword.Health.CurrentHealth;
        yield return Until(() => gun.Health.CurrentHealth < gunHp, .7f);
        Check("real close slash damages partner at normal cut damage", gun.Health.CurrentHealth == gunHp - sword.Profile.Sword.CutDamage);
        yield return Until(() => sword.Actions.Phase != BossActionPhase.Active, .5f);
        Check("one swing cannot repeatedly damage partner", gun.Health.CurrentHealth == gunHp - sword.Profile.Sword.CutDamage);
        Check("player and Boss can each take a hit in the same swing", game.Player.CurrentHealth == playerHp - sword.Profile.Sword.CutDamage);
        Check("sword never damages itself", sword.Health.CurrentHealth == swordHp);

        yield return Fresh(3); StopPolicies();
        gun = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        sword = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        OnFloor(sword.gameObject, sword.Navigation, -10); OnFloor(game.Player.gameObject, sword.Navigation, 5);
        OnFloor(gun.gameObject, sword.Navigation, 10); yield return Seconds(.3f);
        Check("real Draw phrase accepts", sword.Actions.TryStartSword(BossAbility.SwordPursuit));
        yield return Until(() => sword.Actions.CurrentSwordBeat == SwordBeat.Draw && sword.Actions.Phase == BossActionPhase.Locked, 1);
        OnFloor(gun.gameObject, sword.Navigation, -5); gunHp = gun.Health.CurrentHealth;
        yield return Until(() => gun.Health.CurrentHealth < gunHp, .8f);
        Check("real Draw sweep damages blocking partner", gun.Health.CurrentHealth == gunHp - sword.Profile.Sword.DrawDamage);

        yield return Fresh(3); StopPolicies();
        gun = game.Bosses.First(b => b.Profile.Kind == BossKind.Gunner);
        sword = game.Bosses.First(b => b.Profile.Kind == BossKind.Swordsman);
        OnFloor(sword.gameObject, sword.Navigation, -12); OnFloor(gun.gameObject, sword.Navigation, -5);
        OnFloor(game.Player.gameObject, sword.Navigation, 3); yield return Seconds(.3f);
        var hazards = Private<SwordHazards>(sword.Actions, "swordHazards");
        gunHp = gun.Health.CurrentHealth; playerHp = game.Player.CurrentHealth;
        hazards.Wave(gun.Position + Vector2.left * 2, Vector2.right);
        yield return Until(() => !hazards.Pending, .7f);
        Check("wave damages partner once and retires", gun.Health.CurrentHealth == gunHp - sword.Profile.Sword.WaveDamage && !hazards.Pending);
        gunHp = gun.Health.CurrentHealth;
        hazards.Cut(gun.Position + Vector2.left * 2, Vector2.right, false);
        Check("delayed blade is present", hazards.Pending);
        yield return Seconds(.1f);
        Check("delayed blade remains harmless before release", gun.Health.CurrentHealth == gunHp);
        yield return Until(() => !hazards.Pending, 1);
        Check("afterblade damages partner once and retires", gun.Health.CurrentHealth == gunHp - sword.Profile.Sword.EchoDamage && !hazards.Pending);
        Check("blocked hazards spare player behind partner", game.Player.CurrentHealth == playerHp);

        // A real friendly projectile kills one Boss; the survivor still owns the encounter.
        var lethal = Object.Instantiate(gun.ProjectilePrefab, gun.Position + Vector2.left, Quaternion.identity);
        lethal.Launch(Vector2.right, 18, gun.Health.CurrentHealth, sword.gameObject, friendlyFire:true);
        yield return Until(() => !gun.IsAlive, .4f); yield return Frames(2);
        Check("friendly kill uses normal Boss death cleanup", !gun.IsAlive && !gun.Body.enabled && !gun.Running);
        Check("one friendly kill does not finish W3", game.Phase == DuelPhase.Fight && sword.IsAlive);
        sword.Health.TakeDamage(sword.Health.CurrentHealth, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.Complete, .6f);
        Check("last Boss death completes W3", game.Phase == DuelPhase.Complete);
        game.MainMenu(); yield return Frames(2);
        Check("menu clears friendly projectiles and owned hazards", Projectile.Active.Count == 0
            && Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length == 0);
        for(int wave=1;wave<=3;wave++)
        {
            yield return Fresh(wave); int firstFrame = Time.frameCount; yield return Seconds(2);
            Check("W"+wave+" normal combat advances", game.Phase == DuelPhase.Fight && game.FightClock > 1.8f && Time.frameCount > firstFrame);
        }
        Record("NOT TESTED: full-fight balance, physical input, standalone or Build/Release.");
    }
}
#endif
