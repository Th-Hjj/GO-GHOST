#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Three guns, magazines and automatic reload. Virtual input, controlled outcomes, isolated Archive; flow evidence only.</summary>
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
        Record("Weapons smoke: virtual keyboard/mouse, controlled defeat/victory, isolated Archive. TEST values, not balance evidence.");
        string officialBefore = File.Exists(PoliceArchive.SavePath) ? File.ReadAllText(PoliceArchive.SavePath) : null;
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var store = (PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null);
        var uiSfx = flow.GetComponent<SfxSet>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // ---------------- Run 1: Assault Rifle (launch default) ----------------
            yield return WeaponsPracticeStart(flow);
            var f = flow.ActiveFight;
            var gun = f.Player.GetComponent<PlayerShooting>();
            var sfx = f.GetComponent<SfxSet>();
            Check("Launch weapon is the Assault Rifle with a full magazine", flow.Deployment.WeaponId == LoadoutRules.AssaultRifle
                && gun.MagazineSize == 10 && gun.AmmoInMagazine == 10 && AmmoReadout() == "10 / 10" && flow.Deployment.BodyModificationPercent == 0,
                AmmoReadout());
            Mouse(.3f, .9f, true); yield return Seconds(1f);
            int held = gun.TriggerPulls;
            Check("AR fires while held, one round and one cue per shot", held >= 3 && gun.AmmoInMagazine == 10 - held && gun.ShotsFired == held
                && sfx.Requested(Sfx.Shot) == held && AmmoReadout() == gun.AmmoInMagazine + " / 10", $"pulls={held} ammo={gun.AmmoInMagazine}");
            Check("AR bullets pass the practice wall", BulletBeyondPracticeWalls(), "a live bullet is outside the room");
            yield return Phase8Until(() => gun.IsReloading, "AR empties", 9);
            int atEmpty = gun.TriggerPulls;
            yield return Frames(2);
            Check("AR empty magazine starts the automatic reload", atEmpty == 10 && gun.AmmoInMagazine == 0 && AmmoReadout() == "RELOADING", AmmoReadout());
            float x0 = f.Player.transform.position.x;
            Keys(Key.D); yield return Seconds(.6f);
            Check("Reloading: can move, cannot fire, no shot cue", gun.IsReloading && gun.TriggerPulls == atEmpty && sfx.Requested(Sfx.Shot) == atEmpty
                && f.Player.transform.position.x > x0 + 1f, $"dx={f.Player.transform.position.x - x0:0.0}");
            Keys();
            yield return Phase8Until(() => !gun.IsReloading, "AR reload completes", 3);
            yield return Seconds(.45f);
            int resumed = gun.TriggerPulls - atEmpty;
            Check("AR resumes automatically while LMB is still held", resumed >= 1 && gun.AmmoInMagazine == 10 - resumed, $"shots after reload={resumed}");
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "AR door", 20);
            Check("Door refills the AR magazine and restarts statistics", gun.AmmoInMagazine + gun.TriggerPulls == 10 && gun.TriggerPulls <= 2
                && gun.ShotsFired == gun.TriggerPulls && !gun.IsReloading, $"ammo={gun.AmmoInMagazine} pulls={gun.TriggerPulls}");
            Keys(); Mouse(.3f, .9f, false);
            yield return Frames(3);
            f.Player.IsInvulnerable = true; // observation protection only
            Vector3 boss = f.BossHealth.GetComponent<Collider2D>().bounds.center;
            AimAtWorld(boss, true);
            yield return WeaponsWait(() => gun.ShotsHit >= 2, 4);
            AimAtWorld(boss, false);
            yield return Seconds(1.2f);
            Check("AR damages the Boss per bullet, accuracy counts bullets", f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth - 12 * gun.ShotsHit
                && gun.ShotsHit <= gun.ShotsFired && AmmoReadout() == gun.AmmoInMagazine + " / 10", $"hits={gun.ShotsHit}/{gun.ShotsFired}");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return DefeatToMenu(flow);
            yield return Frames(3);
            var run = PoliceArchive.Current.LatestRun;
            Check("Defeat record freezes the Assault Rifle", PoliceArchive.Current.RunRecords.Count == 1 && !run.Success
                && run.SlotItemId(LoadoutSlot.Weapon) == LoadoutRules.AssaultRifle && StrictText("Value_WEAPON") == "Assault Rifle"
                && run.AccuracyPercent > 0 && run.AccuracyPercent <= 100 && run.BodyModificationPercent == 0, $"accuracy={run.AccuracyPercent:0.0}%");

            // ---------------- Run 2: Sniper ----------------
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_WEAPON");
            Check("Weapon choices are exactly the three guns", GameObject.Find("UpgradeChoices").GetComponentsInChildren<TMP_Text>().Select(t => t.text)
                .SequenceEqual(new[] { "Assault Rifle", "Sniper", "Shotgun" }), "Assault Rifle | Sniper | Shotgun");
            int equipBefore = uiSfx.Requested(Sfx.Equip);
            yield return ClickSessionButton("Choice_" + LoadoutRules.Sniper);
            Check("Sniper selected with the equip cue", flow.Draft.WeaponId == LoadoutRules.Sniper && uiSfx.Requested(Sfx.Equip) == equipBefore + 1
                && flow.Draft.BodyModificationPercent == 0, "weapon contributes 0%");
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            f = flow.ActiveFight; gun = f.Player.GetComponent<PlayerShooting>(); sfx = f.GetComponent<SfxSet>();
            Check("Sniper magazine", gun.MagazineSize == 4 && AmmoReadout() == "4 / 4", AmmoReadout());
            Mouse(.3f, .9f, true); yield return Seconds(1.6f);
            Check("Sniper: one shot per press even when held", gun.TriggerPulls == 1 && gun.AmmoInMagazine == 3 && gun.ShotsFired == 1
                && sfx.Requested(Sfx.Shot) == 1, $"pulls={gun.TriggerPulls}");
            Check("Sniper bullet passes the practice wall", BulletBeyondPracticeWalls(), "a live bullet is outside the room");
            Mouse(.3f, .9f, false); yield return Frames(2);
            for (int i = 0; i < 3; i++) { yield return Seconds(1.05f); yield return WeaponClick(.3f, .9f); }
            yield return Frames(2);
            Check("Sniper empties after four shots and reloads by itself", gun.TriggerPulls == 4 && gun.IsReloading && AmmoReadout() == "RELOADING", AmmoReadout());
            Mouse(.3f, .9f, true);
            yield return Phase8Until(() => !gun.IsReloading, "Sniper reload completes", 4);
            yield return Seconds(.5f);
            Check("Sniper does not fire from a press held through the reload", gun.TriggerPulls == 4 && gun.AmmoInMagazine == 4, $"pulls={gun.TriggerPulls}");
            Mouse(.3f, .9f, false); yield return Frames(2);
            yield return WeaponClick(.3f, .9f);
            Check("Sniper fires again on a new press", gun.TriggerPulls == 5 && gun.AmmoInMagazine == 3, $"pulls={gun.TriggerPulls}");
            // Walk next to the door, empty the magazine there, then enter while the reload is still running.
            Keys(Key.D); yield return Seconds(3.2f);
            Keys();
            for (int i = 0; i < 3; i++) { yield return Seconds(1.05f); yield return WeaponClick(.3f, .9f); }
            yield return Frames(2);
            bool reloadingAtDoor = gun.IsReloading && flow.State == GameSessionFlow.FlowState.Practice;
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "Sniper door", 10);
            Keys();
            Check("Door ends the running reload and refills the Sniper", reloadingAtDoor && !gun.IsReloading && gun.AmmoInMagazine == 4
                && gun.TriggerPulls == 0 && gun.ShotsFired == 0, $"wasReloading={reloadingAtDoor} ammo={gun.AmmoInMagazine}");
            yield return Frames(3);
            f.Player.IsInvulnerable = true;
            yield return WeaponClickAt(f.BossHealth.GetComponent<Collider2D>().bounds.center);
            yield return WeaponsWait(() => gun.ShotsHit >= 1, 3);
            Check("Sniper: one bullet, one hit, 45 damage", gun.ShotsFired == 1 && gun.ShotsHit == 1
                && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth - 45, $"boss HP={f.BossHealth.CurrentHealth}");
            yield return ControlledVictory(f);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "Sniper victory", 3);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            Check("Sniper 0% victory reaches the Human Ending", flow.State == GameSessionFlow.FlowState.Ending && StrictTexts().Contains("HUMAN ENDING"), "ending screen");
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(3);
            run = PoliceArchive.Current.LatestRun;
            Check("Victory record freezes the Sniper", flow.State == GameSessionFlow.FlowState.MainMenu && run.Success
                && run.SlotItemId(LoadoutSlot.Weapon) == LoadoutRules.Sniper && StrictText("Value_WEAPON") == "Sniper" && run.AccuracyPercent == 100,
                $"accuracy={run.AccuracyPercent:0.0}%");

            // ---------------- Run 3: Shotgun ----------------
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_WEAPON");
            yield return ClickSessionButton("Choice_" + LoadoutRules.Shotgun);
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            f = flow.ActiveFight; gun = f.Player.GetComponent<PlayerShooting>(); sfx = f.GetComponent<SfxSet>();
            Mouse(.3f, .9f, true); yield return Frames(4);
            int pellets = FindObjectsByType<Projectile>().Length;
            yield return Seconds(1.2f);
            Check("Shotgun: one press = one round, six pellets, one cue, no repeat while held", gun.TriggerPulls == 1 && gun.ShotsFired == 6 && pellets == 6
                && gun.AmmoInMagazine == 4 && AmmoReadout() == "4 / 5" && sfx.Requested(Sfx.Shot) == 1, $"pellets={pellets} pulls={gun.TriggerPulls}");
            Check("Shotgun pellets pass the practice wall", BulletBeyondPracticeWalls(), "a live pellet is outside the room");
            Mouse(.3f, .9f, false); yield return Frames(2);
            for (int i = 0; i < 4; i++) { yield return Seconds(.85f); yield return WeaponClick(.3f, .9f); }
            yield return Frames(2);
            Check("Shotgun empties after five shots and reloads by itself", gun.TriggerPulls == 5 && gun.ShotsFired == 30 && gun.IsReloading
                && AmmoReadout() == "RELOADING", AmmoReadout());
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "Shotgun door", 20);
            Keys();
            Check("Door refills the Shotgun", gun.AmmoInMagazine == 5 && gun.TriggerPulls == 0 && gun.ShotsFired == 0 && !gun.IsReloading, AmmoReadout());
            yield return Frames(3);
            f.Player.IsInvulnerable = true;
            yield return WeaponClickAt(f.BossHealth.GetComponent<Collider2D>().bounds.center);
            yield return Seconds(1.3f);
            int farHits = gun.ShotsHit;
            Keys(Key.D); yield return Seconds(1.6f); Keys(); yield return Frames(3);
            yield return WeaponClickAt(f.BossHealth.GetComponent<Collider2D>().bounds.center);
            yield return Seconds(1.2f);
            int closeHits = gun.ShotsHit - farHits;
            Check("Shotgun accuracy counts pellets and never exceeds 100%", gun.ShotsFired == 12 && gun.TriggerPulls == 2 && gun.ShotsHit <= gun.ShotsFired
                && gun.ShotsHit >= 1 && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth - 7 * gun.ShotsHit,
                $"far hits={farHits}/6 close hits={closeHits}/6 bossHP={f.BossHealth.CurrentHealth}");
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return DefeatToMenu(flow);
            yield return Frames(3);
            run = PoliceArchive.Current.LatestRun;
            Check("Defeat record freezes the Shotgun", !run.Success && run.SlotItemId(LoadoutSlot.Weapon) == LoadoutRules.Shotgun
                && StrictText("Value_WEAPON") == "Shotgun" && run.AccuracyPercent > 0 && run.AccuracyPercent <= 100, $"accuracy={run.AccuracyPercent:0.0}%");

            var reloaded = new PoliceArchiveStore(store.FilePath).Load();
            Check("Archive reload keeps the three weapon IDs", reloaded.RunRecords.Select(r => r.SlotItemId(LoadoutSlot.Weapon))
                .SequenceEqual(new[] { LoadoutRules.AssaultRifle, LoadoutRules.Sniper, LoadoutRules.Shotgun })
                && reloaded.UnlockedEndings.SequenceEqual(new[] { PoliceArchive.HumanEnding }), store.FilePath);
            Check("Normal profile untouched", (File.Exists(PoliceArchive.SavePath) ? File.ReadAllText(PoliceArchive.SavePath) : null) == officialBefore
                && store.FilePath != PoliceArchive.SavePath, "isolated store");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
