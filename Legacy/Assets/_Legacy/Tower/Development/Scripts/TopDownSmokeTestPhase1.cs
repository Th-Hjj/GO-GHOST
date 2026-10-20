#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    public static void RunPhase1FoundationBatch() => RunSmokeBatch("phase1-foundation");

    private void Phase1Models()
    {
        Check("slot ordinals preserved", (int)LoadoutSlot.Torso == 4 && (int)LoadoutSlot.Brain == 5 && (int)LoadoutSlot.Weapon == 6
            && Enum.GetNames(typeof(LoadoutSlot)).Length == 7 && ((LoadoutSlot)4).ToString() == "Torso", "seven slots; Torso replaces ordinal 4");
        var draft = new LoadoutDraft(true);
        for (int i = 0; i < 6; i++) draft.TrySetSlot((LoadoutSlot)i, LoadoutRules.ModuleForSlot((LoadoutSlot)i));
        var deployed = draft.Deploy(12);
        var record = new VictoryRecord(deployed);
        Check("prototype values unchanged", deployed.BodyModificationPercent == 100 && deployed.ArmCount == 2 && deployed.LegCount == 2
            && deployed.ShotsPerSecond == 10 && deployed.MoveSpeed == 10 && deployed.BulletDamage == 12 && deployed.DodgeStartInterval == .8f
            && deployed.HasTimeSlow && deployed.Aim == AimControl.AI && deployed.WeaponCompatibility == WeaponCompatibility.Normal,
            "10/10/10/10/20/40, 4+3 per arm, 6+2 per leg; existing Torso ID retained");
        Check("snapshot and victory stay immutable", !draft.TrySetSlot(LoadoutSlot.Torso, "") && record.BodyModificationPercent == 100
            && !ReferenceEquals(record.WinningDeployment, deployed) && record.WinningDeployment.GetSlot(LoadoutSlot.Weapon).BodyContributionPercent == 0,
            "sealed source, independent winning copy, Weapon zero");

        var compound = new AugmentationDefinition("TEST_COMPOUND_LEGS", WeaponCompatibility.Normal, LoadoutSlot.LeftLeg, LoadoutSlot.RightLeg);
        var modified = new AugmentationDefinition("TEST_MODIFIED_WEAPON", WeaponCompatibility.Modified, LoadoutSlot.Torso);
        var disabled = new AugmentationDefinition("TEST_DISABLED_WEAPON", WeaponCompatibility.Disabled, LoadoutSlot.LeftArm);
        var catalog = new[] { compound, modified, disabled,
            new AugmentationDefinition(LoadoutRules.LegMoveSpeed, WeaponCompatibility.Normal, LoadoutSlot.LeftLeg),
            new AugmentationDefinition(LoadoutRules.LegMoveSpeed, WeaponCompatibility.Normal, LoadoutSlot.RightLeg) };
        var isolated = new LoadoutDraft(true, catalog);
        isolated.TrySetSlot(LoadoutSlot.RightLeg, LoadoutRules.LegMoveSpeed);
        Check("compound conflict is atomic", !isolated.TrySetSlot(LoadoutSlot.LeftLeg, compound.ItemId)
            && isolated.GetSlot(LoadoutSlot.LeftLeg).ItemId == "" && isolated.GetSlot(LoadoutSlot.RightLeg).ItemId == LoadoutRules.LegMoveSpeed,
            "a separately occupied required slot rejects installation without partial writes");
        isolated.TrySetSlot(LoadoutSlot.RightLeg, "");
        Check("compound occupies both legs", isolated.TrySetSlot(LoadoutSlot.LeftLeg, compound.ItemId)
            && isolated.GetSlot(LoadoutSlot.RightLeg).ItemId == compound.ItemId && isolated.BodyModificationPercent == 20,
            "one installation, both existing 10% capacities");
        isolated.TrySetSlot(LoadoutSlot.Torso, modified.ItemId);
        Check("Modified weapon declaration", isolated.WeaponCompatibility == WeaponCompatibility.Modified && isolated.CanUseWeapon, "data only; no invented weapon effect");
        isolated.TrySetSlot(LoadoutSlot.LeftArm, disabled.ItemId);
        Check("Disabled weapon declaration", isolated.WeaponCompatibility == WeaponCompatibility.Disabled && !isolated.CanUseWeapon
            && !isolated.TrySetSlot(LoadoutSlot.Weapon, LoadoutRules.AssaultRifle), "restrictive status wins; selected weapon retained with 0% contribution");
        var compoundSnapshot = isolated.Deploy(2);
        Check("compound snapshot does not invent passive effects", compoundSnapshot.LegCount == 0 && compoundSnapshot.MoveSpeed == 6
            && compoundSnapshot.WeaponCompatibility == WeaponCompatibility.Disabled, "only known Rapid Fire/Move Speed IDs grant their effects");
        var replacement = new LoadoutDraft(compoundSnapshot, true);
        replacement.TrySetSlot(LoadoutSlot.RightLeg, "");
        Check("compound removal releases entire installation", replacement.GetSlot(LoadoutSlot.LeftLeg).ItemId == ""
            && replacement.GetSlot(LoadoutSlot.RightLeg).ItemId == "" && compoundSnapshot.GetSlot(LoadoutSlot.LeftLeg).ItemId == compound.ItemId,
            "remove from either leg; old immutable snapshot unaffected");
        replacement.TrySetSlot(LoadoutSlot.LeftArm, ""); replacement.TrySetSlot(LoadoutSlot.Torso, "");
        Check("weapon compatibility restores Normal", replacement.CanUseWeapon && replacement.WeaponCompatibility == WeaponCompatibility.Normal, "removal recomputes policy");
        Check("test definitions absent from formal catalog", !new LoadoutDraft(true).TrySetSlot(LoadoutSlot.LeftLeg, compound.ItemId), "no fake hidden upgrade in normal UI/catalog");
        bool rejected = false;
        try { new AugmentationDefinition("TEST_INVALID", WeaponCompatibility.Normal, LoadoutSlot.Weapon); }
        catch (ArgumentException) { rejected = true; }
        Check("compound requires body slots", rejected, "Weapon cannot contribute as an occupied body slot");
    }

    private PoliceArchiveStore Phase1Archive()
    {
        // Every file, backup and invalid original stays under a unique ignored test directory.
        string root = Path.GetFullPath(Path.Combine("Logs/GO_GHOST_VS/Phase1ArchiveTests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var store = new PoliceArchiveStore(Path.Combine(root, "archive.json"));
        var profile = store.Load();
        Check("Archive missing file creates valid empty save", store.LastLoadStatus == PoliceArchiveLoadStatus.CreatedEmpty
            && profile.CompletedAchievements.Count == 0 && profile.UnlockedEndings.Count == 0 && profile.UnlockedHiddenAugmentations.Count == 0
            && File.Exists(store.FilePath), "isolated path=" + store.FilePath);
        profile.CompleteAchievement("TEST_ACHIEVEMENT"); profile.UnlockHiddenAugmentation("TEST_HIDDEN_AUGMENT"); profile.UnlockEnding("TEST_ENDING");
        bool saved = store.TrySave(profile, out string error);
        var reloaded = new PoliceArchiveStore(store.FilePath).Load();
        Check("Archive disk round trip", saved && reloaded.CompletedAchievements.Count == 1 && reloaded.CompletedAchievements[0] == "TEST_ACHIEVEMENT"
            && reloaded.UnlockedHiddenAugmentations[0] == "TEST_HIDDEN_AUGMENT" && reloaded.UnlockedEndings[0] == "TEST_ENDING", error ?? "all three collections persisted");
        store.TrySave(reloaded, out error); // backup now contains the test record too.
        File.WriteAllText(store.FilePath, "{ broken JSON");
        var recovered = store.Load();
        Check("Archive corrupt primary recovers backup", store.LastLoadStatus == PoliceArchiveLoadStatus.RecoveredBackup
            && recovered.CompletedAchievements.Count == 1 && Directory.GetFiles(root, "archive.json.invalid-*").Length == 1,
            "invalid original preserved; previous valid JSON restored");
        foreach (string invalid in new[] { "{}", "null", "{\"saveVersion\":1,\"unlockedEndings\":[],\"completedAchievements\":[\"\"],\"unlockedHiddenAugmentations\":[]}" })
        {
            string path = Path.Combine(root, "invalid-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, invalid);
            var invalidStore = new PoliceArchiveStore(path); var fallback = invalidStore.Load();
            Check("Archive invalid schema safe fallback", invalidStore.LastLoadStatus == PoliceArchiveLoadStatus.RecoveredEmpty
                && fallback.CompletedAchievements.Count == 0 && new PoliceArchiveStore(path).Load().UnlockedEndings.Count == 0, "preserved invalid file; valid empty replacement");
        }
        string newer = Path.Combine(root, "future.json");
        string futureJson = "{\"saveVersion\":999,\"unlockedEndings\":[],\"completedAchievements\":[],\"unlockedHiddenAugmentations\":[]}";
        File.WriteAllText(newer, futureJson); var futureStore = new PoliceArchiveStore(newer);
        Check("Archive Save cannot bypass version check", !futureStore.TrySave(new PoliceArchiveProfile(), out error)
            && File.ReadAllText(newer) == futureJson, "Save requires prior Load; existing newer file untouched");
        var future = futureStore.Load();
        Check("Archive newer schema preserved", futureStore.LastLoadStatus == PoliceArchiveLoadStatus.UnsupportedVersion
            && !futureStore.TrySave(future, out error) && File.ReadAllText(newer) == futureJson, "no destructive downgrade");
        string blocker = Path.Combine(root, "not-a-directory");File.WriteAllText(blocker, "test");
        var unavailable = new PoliceArchiveStore(Path.Combine(blocker, "archive.json"));
        Check("Archive I/O failure does not throw into gameplay", unavailable.Load().CompletedAchievements.Count == 0
            && unavailable.LastLoadStatus == PoliceArchiveLoadStatus.Unavailable, "in-memory empty profile; failed persistence reported");
        return store;
    }

    private IEnumerator Phase1Foundation()
    {
        Record("Phase1: virtual Input System keyboard/mouse and real uGUI. Forced outcomes validate lifecycle, not human winnability.");
        Phase1Models();
        var official = PoliceArchive.Current;
        string officialBefore = File.ReadAllText(PoliceArchive.SavePath);
        var archive = Phase1Archive();
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);FocusV2(flow);flow.ActiveFight.Boss.StopCombat();
        yield return Frames(3);Keys(Key.E);yield return Frames(3);Keys();
        Check("Human E has no Time Slow", !flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>().IsActive, "not equipped");
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("TorsoButton");yield return ClickSessionButton("BrainButton");
        Check("Torso UI and percent", GameObject.Find("TorsoButton").GetComponentInChildren<TMPro.TMP_Text>().text.Contains("TORSO")
            && flow.Draft.BodyModificationPercent == 60 && flow.Draft.GetSlot((LoadoutSlot)4).ItemId == LoadoutRules.TorsoSlow, "Torso20 + Brain40; same seven slots");
        yield return PolishScreen("phase1-loadout");
        yield return DeployAndFocus(flow);var fight = flow.ActiveFight;fight.Boss.StopCombat();
        yield return BrainV2(flow);
        fight.Player.GetComponent<Rigidbody2D>().position = new Vector2(-9, -1.69f);
        fight.Boss.GetComponent<Rigidbody2D>().position = new Vector2(9, -1.7f);FocusV2(flow);yield return Frames(3);
        yield return TorsoV2(flow); // real E; unchanged enemy clock, player speed, duration and cooldown.
        fight.Boss.StopCombat();foreach(var projectile in FindObjectsByType<Projectile>()) projectile.Retire();
        var slow = fight.Player.GetComponent<PlayerTimeSlow>();var dodge = fight.Player.GetComponent<PlayerDodge>();
        Keys();yield return Seconds(.15f);Keys(Key.LeftShift);yield return Frames(3);Keys();
        Check("Shift Dodge during active Time Slow", dodge.IsDodging && slow.IsActive && fight.Player.IsInvulnerable, "independent ground dodge while E effect is active");
        while(slow.IsActive)yield return null;
        Keys(Key.E);yield return Frames(3);Keys();
        Check("E respects cooldown", !slow.IsActive && slow.CooldownRemaining > 0, "cannot reactivate early");
        Keys(Key.LeftShift);yield return Frames(3);Keys();
        Check("Shift Dodge during Time Slow cooldown", dodge.IsDodging && !slow.IsActive && slow.CooldownRemaining > 0, "Torso never intercepts Shift");
        while(slow.CooldownRemaining > 0)yield return null;
        yield return Frames(3);Keys(Key.E);yield return Frames(3);Keys();
        Check("Torso active before death", slow.IsActive, "real E activation");
        fight.Player.TakeDamage(fight.Player.MaxHealth, fight.Boss.gameObject);yield return Frames(3);
        Check("attempt end clears Torso and combat state", !slow.IsActive && slow.CooldownRemaining == 0 && slow.WorldRate == 1
            && !fight.Player.IsInvulnerable && fight.Boss.State == CyberpsychoBoss.AttackState.Stopped, "explicit idempotent cleanup");
        fight.Player.GetComponent<OfficerIdentity>().EndAttempt();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        Check("old AI actor and target disposed", fight == null && slow == null && flow.ActiveFight == null, "new attempt must bind a fresh target");
        yield return ClickSessionButton("TorsoButton");yield return ClickSessionButton("BrainButton");yield return DeployAndFocus(flow);
        fight = flow.ActiveFight;fight.Boss.StopCombat();slow = fight.Player.GetComponent<PlayerTimeSlow>();
        yield return VerifyManualAim(flow);
        Keys(Key.E);yield return Frames(3);Keys();
        Check("removed Torso E cannot activate", !slow.IsEquipped && !slow.IsActive && slow.CooldownRemaining == 0 && flow.Deployment.BodyModificationPercent == 0, "fresh Human snapshot and clock");
        yield return MeasureDodgeInterval(flow, .8f, "Phase1 removed Torso");
        Check("redeploy full health", fight.Player.CurrentHealth == 100 && fight.BossHealth.CurrentHealth == 800, "fresh 100/800");
        fight.BossHealth.TakeDamage(fight.BossHealth.MaxHealth, fight.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("Phase1 winning record", flow.Victory.OfficerIndex == 3 && flow.Victory.BodyModificationPercent == 0
            && flow.Victory.WinningDeployment.GetSlot(LoadoutSlot.Weapon).BodyContributionPercent == 0, "previous officer60%, winner0%");
        yield return PolishScreen("phase1-result");yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);flow.ActiveFight.Boss.StopCombat();
        var afterNewGame = new PoliceArchiveStore(archive.FilePath).Load();
        Check("Police Archive survives actual New Game", afterNewGame.CompletedAchievements.Count == 1 && afterNewGame.CompletedAchievements[0] == "TEST_ACHIEVEMENT"
            && ReferenceEquals(official, PoliceArchive.Current), "isolated on-disk record and current Archive lifetime independent of session reset");
        Check("official Archive has no test pollution", File.ReadAllText(PoliceArchive.SavePath) == officialBefore, "test IDs only in ignored isolated directory");
        Check("session reset still clears loadout", flow.OfficerIndex == 1 && flow.Deployment.BodyModificationPercent == 0 && flow.Deployment.Aim == AimControl.Manual
            && !flow.AugmentationsUnlocked && flow.Victory == null && flow.ActiveFight.Player.CurrentHealth == 100 && flow.ActiveFight.BossHealth.CurrentHealth == 800,
            "Archive survives; session starts fresh");
    }
}
#endif
