#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Rifle-only catalogue, magazines and automatic reload. Virtual input, controlled outcomes, isolated Archive; flow evidence only.</summary>
public partial class TopDownSmokeTest
{
    private static readonly System.Reflection.FieldInfo ReaderFocus = typeof(PlayerInputReader)
        .GetField("hasFocus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    private static readonly System.Reflection.FieldInfo ReaderRelease = typeof(PlayerInputReader)
        .GetField("waitingForRelease", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    // The operator may click in and out of the editor while a scenario runs. Keep the virtual devices authoritative:
    // a real focus change must not silently stop the officer mid-test.
    private IEnumerator KeepVirtualFocus(GameSessionFlow flow)
    {
        while (true)
        {
            var fight = flow != null ? flow.ActiveFight : null;
            var reader = fight != null ? fight.Player.GetComponent<PlayerInputReader>() : null;
            if (reader != null && !(bool)ReaderFocus.GetValue(reader)) ReaderFocus.SetValue(reader, true);
            if (reader != null) ReaderRelease.SetValue(reader, false); // a refocus must not wait for held virtual keys
            ResendVirtualState();
            yield return null;
        }
    }

    private static string AmmoReadout() => GameObject.Find("Ammo").GetComponent<TMP_Text>().text;
    private static bool BulletBeyondPracticeWalls() => FindObjectsByType<Projectile>()
        .Any(p => p.transform.position.y > 9.5f || p.transform.position.x < -55.8f || p.transform.position.x > -24.2f);

    // Non-throwing wait for outcomes that depend on live combat; the following Check reports a miss.
    private static IEnumerator WeaponsWait(System.Func<bool> condition, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    // Defeat stops on Mission Result; CONTINUE returns to the Main Menu.
    private IEnumerator DefeatToMenu(GameSessionFlow flow)
    {
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION FAILED", 3);
        yield return Frames(2);
        yield return ClickSessionButton("ResultContinue");
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.MainMenu, "Main Menu", 3);
    }

    // Controlled victory through both Boss phases: Phase 1 depletion, the Boss's own transition, then Phase 2.
    private IEnumerator ControlledVictory(BossFight fight)
    {
        fight.BossHealth.TakeDamage(100000, fight.Player.gameObject);
        yield return WeaponsWait(() => fight.BossHealth.IsDead || (fight.BossHealth.PhaseIndex == 2 && !fight.BossHealth.AwaitingSecondPhase), 8);
        fight.BossHealth.TakeDamage(100000, fight.Player.gameObject);
    }

    private IEnumerator WeaponClick(float fx, float fy)
    {
        Mouse(fx, fy, true); yield return Frames(2);
        Mouse(fx, fy, false); yield return Frames(2);
    }

    private IEnumerator WeaponClickAt(Vector3 world)
    {
        AimAtWorld(world, false); yield return Frames(2);
        AimAtWorld(world, true); yield return Frames(2);
        AimAtWorld(world, false); yield return Frames(2);
    }

    private IEnumerator WeaponsPracticeStart(GameSessionFlow flow)
    {
        yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Practice, "practice", 6);
        yield return Frames(3);
        flow.ActiveFight.Player.GetComponent<PlayerInputReader>().SendMessage("OnApplicationFocus", true);
        Keys(); MouseTo(.3f, .9f);
        yield return Frames(3);
    }

    private IEnumerator Weapons()
    {
        Record("Rifle-only: real virtual input, isolated Archive, controlled Boss/death. No retired-weapon compatibility or historical matrix.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(S01Focus(flow));
        try
        {
            yield return WeaponsPracticeStart(flow);
            Check("Only rifle is defined and deployed", LoadoutRules.OfferedWeapons.SequenceEqual(new[] {LoadoutRules.AssaultRifle})
                && flow.Deployment.WeaponId == LoadoutRules.AssaultRifle, flow.Deployment.WeaponId);
            string[] removed = { "sniper", "shotgun", "basic_gun" };
            bool rejected = true;
            foreach (string id in removed)
            {
                var draft = new LoadoutDraft(true);
                rejected &= !LoadoutRules.IsWeapon(id) && !draft.TrySetSlot(LoadoutSlot.Weapon, id);
                try { LoadoutRules.Weapon(id); rejected = false; } catch (System.ArgumentException) { }
                var entries = Enumerable.Range(0, LoadoutRules.SlotCount).Select(i=>flow.Deployment.GetSlot((LoadoutSlot)i)).ToArray();
                entries[(int)LoadoutSlot.Weapon] = new LoadoutEntry(LoadoutSlot.Weapon, id, 0);
                try { new DeploymentSnapshot(1, entries); rejected = false; } catch (System.ArgumentException) { }
            }
            Check("Removed IDs rejected by draft, profile and snapshot", rejected, "no alias or fallback");

            flow.ActiveFight.Player.TakeDamage(100000, gameObject);
            yield return DefeatToMenu(flow);
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_WEAPON");
            var labels = GameObject.Find("UpgradeChoices").GetComponentsInChildren<TMP_Text>().Select(t=>t.text).ToArray();
            Check("Formal upgrade UI offers exactly one rifle", labels.SequenceEqual(new[] {"Assault Rifle"}), string.Join("|", labels));
            ScreenCapture.CaptureScreenshot("Logs/RifleOnly/upgrade-rifle.png"); yield return Frames(3);
            yield return ClickSessionButton("Choice_" + LoadoutRules.AssaultRifle);
            S01Field(flow, "developmentPermissionTier", 0);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            var fight = flow.ActiveFight; var gun = fight.Player.GetComponent<PlayerShooting>(); var sfx = fight.GetComponent<SfxSet>();
            Check("Rifle TEST values unchanged", gun.MagazineSize==10 && gun.AmmoInMagazine==10 && gun.BulletDamage==12
                && Mathf.Abs(gun.FireInterval-.25f)<.001f && Mathf.Abs(flow.Deployment.Weapon.ReloadSeconds-1.2f)<.001f,AmmoReadout());
            Mouse(.3f,.9f,true); yield return Seconds(1f);
            int pulls=gun.TriggerPulls;
            Check("Held fire: one bullet, round and cue per pull",pulls>=3 && pulls<=5 && gun.ShotsFired==pulls
                && gun.AmmoInMagazine==10-pulls && sfx.Requested(Sfx.Shot)==pulls,$"pulls={pulls}, bullets={gun.ShotsFired}");
            yield return WeaponsWait(()=>gun.IsReloading,4f);
            int emptyPulls=gun.TriggerPulls;
            yield return Frames(2);
            Check("Empty rifle starts auto reload",gun.IsReloading && gun.AmmoInMagazine==0 && emptyPulls==10 && AmmoReadout()=="RELOADING",AmmoReadout());
            float x=fight.Player.transform.position.x;
            Keys(Key.D); yield return Seconds(.5f); Keys();
            Check("Reload blocks fire but keeps movement",gun.IsReloading && gun.TriggerPulls==emptyPulls
                && fight.Player.transform.position.x>x+1,$"pulls={gun.TriggerPulls}, dx={fight.Player.transform.position.x-x:0.00}");
            yield return WeaponsWait(()=>gun.TriggerPulls>emptyPulls,3f);
            Mouse(.3f,.9f,false); yield return Frames(2);
            Check("Held rifle resumes after reload",!gun.IsReloading && gun.TriggerPulls>emptyPulls && gun.ShotsFired==gun.TriggerPulls,gun.TriggerPulls.ToString());
            flow.RequestEnterBossRoom();
            Check("Practice door retains refill/statistics boundary",flow.TowerStageId=="tower_entry" && gun.AmmoInMagazine==10
                && gun.TriggerPulls==0 && gun.ShotsFired==0,"ammo="+gun.AmmoInMagazine);
            flow.RequestTowerExit(flow.CurrentRoom); flow.RequestTowerExit(flow.CurrentRoom);
            yield return Frames(3); fight.Boss.StopCombat(); fight.Player.IsInvulnerable=true;
            AimAtWorld(fight.BossHealth.GetComponent<Collider2D>().bounds.center,true);
            yield return WeaponsWait(()=>gun.ShotsHit>=2,4f);
            Mouse(.3f,.9f,false); yield return Seconds(1.2f);
            Check("Rifle real hit keeps damage and accuracy",gun.ShotsHit>=2 && gun.ShotsHit<=gun.ShotsFired
                && fight.BossHealth.CurrentHealth==fight.BossHealth.MaxHealth-12*gun.ShotsHit,$"hits={gun.ShotsHit}/{gun.ShotsFired}");
            fight.Player.IsInvulnerable=false; fight.Player.TakeDamage(100000,gameObject);
            yield return DefeatToMenu(flow);
            Check("Normal failure freezes rifle and continues",PoliceArchive.Current.LatestRun.SlotItemId(LoadoutSlot.Weapon)==LoadoutRules.AssaultRifle
                && flow.State==GameSessionFlow.FlowState.MainMenu,"weapon="+PoliceArchive.Current.LatestRun.SlotItemId(LoadoutSlot.Weapon));

            var menu=flow.GetComponent<DeveloperRouteJump>(); menu.SetVisible(true);
            bool devReject=removed.All(id=>!menu.SelectUpgrade(LoadoutSlot.Weapon,id));
            menu.SelectUpgrade(LoadoutSlot.RightArm,LoadoutRules.ArmFireRate); menu.SelectNode("practice"); menu.SelectTier(0);
            Check("Developer selection rejects removed weapons",devReject && menu.Choices.WeaponId==LoadoutRules.AssaultRifle,menu.Choices.WeaponId);
            menu.JumpSelected(); yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Practice,4f); yield return Frames(3);
            gun=flow.ActiveFight.Player.GetComponent<PlayerShooting>();
            Check("Developer snapshot keeps rifle and Rapid Fire",flow.Deployment.WeaponId==LoadoutRules.AssaultRifle
                && Mathf.Abs(gun.FireInterval-.25f/1.75f)<.001f && gun.MagazineSize==10,gun.FireInterval.ToString());
            flow.DevelopmentRestartFullRun(); yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Practice,4f);
            Check("Full restart defaults to human rifle",flow.Deployment.WeaponId==LoadoutRules.AssaultRifle && flow.Deployment.BodyModificationPercent==0
                && !flow.IsDevelopmentJumpRun && Time.timeScale==1,flow.TowerStageId);
        }
        finally { StopCoroutine(focus); Keys(); Mouse(.5f,.5f,false); }
    }
}
#endif
