#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One isolated Archive profile through real session/UI/Drone paths; no normal-profile test unlocks.</summary>
public partial class TopDownSmokeTest
{
    private PoliceArchiveStore phase9OriginalStore;
    private PoliceArchiveProfile phase9OriginalProfile;
    private bool phase9ArchiveIsolated;
    private const BindingFlags ArchiveFields = BindingFlags.NonPublic | BindingFlags.Static;

    private void Phase9UseArchive(string path)
    {
        var store = new PoliceArchiveStore(path);
        var profile = store.Load(); // Fresh object loaded from the real JSON file, not an in-memory copy.
        typeof(PoliceArchive).GetField("store", ArchiveFields).SetValue(null, store);
        typeof(PoliceArchive).GetField("current", ArchiveFields).SetValue(null, profile);
    }

    private void RestorePhase9Archive()
    {
        if (!phase9ArchiveIsolated) return;
        typeof(PoliceArchive).GetField("store", ArchiveFields).SetValue(null, phase9OriginalStore);
        typeof(PoliceArchive).GetField("current", ArchiveFields).SetValue(null, phase9OriginalProfile);
        phase9ArchiveIsolated = false;
    }

    private IEnumerator Phase9Msp()
    {
        phase9OriginalProfile = PoliceArchive.Current;
        phase9OriginalStore = (PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null);
        string officialBefore = File.Exists(PoliceArchive.SavePath) ? File.ReadAllText(PoliceArchive.SavePath) : null;
        string path = Path.GetFullPath("Logs/GO_GHOST_VS/Phase9ArchiveTests/" + Guid.NewGuid().ToString("N") + "/archive.json");
        phase9ArchiveIsolated = true;
        Phase9UseArchive(path);
        Record("MSP: isolated fresh Archive, actual victory resolver/save/reload/UI/projectiles; forced outcomes and observation protection. Not balance/natural victory. Profile=" + path);
        try { yield return Phase9Loop(path); }
        finally { v2ProtectedPlayer = null; RestorePhase9Archive(); }
        Check("Normal Profile Preserved", (File.Exists(PoliceArchive.SavePath) ? File.ReadAllText(PoliceArchive.SavePath) : null) == officialBefore
            && ReferenceEquals(PoliceArchive.Current, phase9OriginalProfile), "normal profile file unchanged; isolated Archive bindings restored");
    }

    private IEnumerator Phase9Loop(string path)
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var view = FindAnyObjectByType<SessionView>();
        yield return Frames(3);
        var originalState = flow.State;
        yield return ClickSessionButton("PoliceArchiveButton");
        bool opened = view.ArchiveOpen;
        string text = GameObject.Find("Details").GetComponent<TMPro.TMP_Text>().text;
        Check("Endings Section", text.Contains("ENDINGS") && text.Contains("No endings recorded.")
            && PoliceArchive.Current.UnlockedEndings.Count == 0, "existing profile collection; no invented ending IDs/routing");
        Check("Achievements Section", text.Contains(PoliceArchive.TempFirstVictory) && text.Contains("AVAILABLE")
            && text.Contains("TEMP / placeholder condition"), "one clearly temporary Boss-victory condition and Drone reward");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase9-archive-empty.png");
        yield return ClickSessionButton("PoliceArchiveButton");
        Check("Archive UI", opened && !view.ArchiveOpen && flow.State == originalState
            && GameObject.Find("DeployButton") != null, "POLICE ARCHIVE opens from current New Mission; BACK restores the unchanged Continue page");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("LeftArmButton");
        yield return ClickSessionButton("LeftArmButton");
        string armLabel = GameObject.Find("LeftArmButton").GetComponentInChildren<TMPro.TMP_Text>().text;
        bool blocked = !flow.TrySetDraftSlot(LoadoutSlot.LeftArm, LoadoutRules.DetachedDroneArm);
        Check("Drone Hidden Before Unlock", !PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
            && !armLabel.Contains("Drone") && blocked && LoadoutRules.NextOption(LoadoutSlot.LeftArm, LoadoutRules.CounterArm) == "",
            "not in Left Arm cycle/next-option label; draft rejects direct equip too");
        yield return ClickSessionButton("LeftArmButton"); // Human
        yield return DeployAndFocus(flow);
        flow.ActiveFight.BossHealth.TakeDamage(800, flow.ActiveFight.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("TEMP Achievement", PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory)
            && PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
            && string.IsNullOrEmpty(PoliceArchive.LastSaveError), "real session victory resolver records achievement and reward together");
        yield return ClickSessionButton("PoliceArchiveButton");
        text = GameObject.Find("Details").GetComponent<TMPro.TMP_Text>().text;
        bool recordedUI = text.Contains("COMPLETED") && text.Contains("UNLOCKED") && text.Contains("No endings recorded.");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase9-archive-unlocked.png");
        yield return ClickSessionButton("PoliceArchiveButton");
        var beforeReload = PoliceArchive.Current;
        yield return ClickSessionButton("NewGameButton");
        bool newGameKeeps = PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
            && flow.OfficerIndex == 1 && flow.Draft.BodyModificationPercent == 0;
        Phase9UseArchive(path); // Drop old cache, load persistent achievement + unlock into a new profile.
        yield return ClickSessionButton("PoliceArchiveButton");
        text = GameObject.Find("Details").GetComponent<TMPro.TMP_Text>().text;
        bool reloadUI = text.Contains("COMPLETED") && text.Contains("UNLOCKED");
        yield return ClickSessionButton("PoliceArchiveButton");
        string json = File.ReadAllText(path);
        Check("Unlock Persistence", recordedUI && newGameKeeps && reloadUI && !ReferenceEquals(beforeReload, PoliceArchive.Current)
            && json.Contains(PoliceArchive.TempFirstVictory) && json.Contains(LoadoutRules.DetachedDroneArm),
            "achievement/reward written to version-1 JSON; New Game keeps them; discarded cache/reloaded disk profile and Archive UI retain both");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        yield return FixtureNextOfficer(flow); // Existing rule: first officer starts 0%; next officer opens Loadout.
        yield return ClickSessionButton("LeftArmButton");
        yield return ClickSessionButton("LeftArmButton");
        bool visible = GameObject.Find("LeftArmButton").GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Detached Drone Arm");
        yield return ClickSessionButton("LeftArmButton");
        bool selected = flow.Draft.GetSlot(LoadoutSlot.LeftArm).ItemId == LoadoutRules.DetachedDroneArm;
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase9-drone-loadout.png");
        yield return DeployAndFocus(flow);
        var f = flow.ActiveFight;
        var drone = f.Player.GetComponent<PlayerDetachedDroneArm>();
        var arm = f.Player.GetComponent<OfficerVisual>().Part(LoadoutSlot.LeftArm);
        Check("Drone Equip", visible && selected && f.Deployment.HasDetachedDrone && f.Deployment.BodyModificationPercent == 10
            && drone.Unit != null && !arm.enabled && Phase9UnitCount() == 1,
            "unlocked Left Arm cycle selects Drone; immutable 10% snapshot spawns one detached unit and hides left arm");
        v2ProtectedPlayer = f.Player; f.Player.IsInvulnerable = true;
        var playerBody = f.Player.GetComponent<Rigidbody2D>();
        var reader = f.Player.GetComponent<PlayerInputReader>();
        var shots = f.Player.GetComponent<PlayerShooting>();
        AimAtWorld(f.Player.transform.position + Vector3.left * 20, false);
        Vector3 unitStart = drone.Unit.position;
        int hp = f.BossHealth.CurrentHealth;
        Keys(Key.D); yield return Seconds(.35f); Keys();
        yield return Seconds(.45f);
        bool followed = drone.Unit.position.x > unitStart.x + 1
            && Vector2.Distance(drone.Unit.position, playerBody.position + new Vector2(1.3f, 1)) < .25f;
        Keys(Key.E, Key.Q); yield return Frames(3); Keys();
        yield return Phase8Until(() => f.BossHealth.CurrentHealth < hp && drone.ShotsFired >= 2, "autonomous Drone projectile damage", 6);
        Vector2 target = f.BossHealth.GetComponent<Collider2D>().bounds.center - drone.Unit.position;
        Check("Autonomous Drone Combat", followed && drone.ShotsFired >= 2 && f.BossHealth.CurrentHealth < hp
            && Vector2.Dot(drone.AimDirection, target.normalized) > .97f && reader.AimMode == AimControl.Manual
            && reader.AimDirection.x < -.9f && !reader.AttackHeld && shots.ShotsFired == 0
            && !f.Player.GetComponent<PlayerCounterArm>().IsWindowOpen && !f.Player.GetComponent<PlayerTimeSlow>().IsActive,
            $"follow={followed}; independent aim/fire, shots={drone.ShotsFired}, Boss HP {hp}->{f.BossHealth.CurrentHealth}; mouse stays left, no LMB/E/Q control of Drone");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/phase9-drone-combat.png");
        var oldUnit = drone.Unit;
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        yield return FixtureNextOfficer(flow);
        bool deathClean = oldUnit == null && Phase9UnitCount() == 0;
        yield return DeployAndFocus(flow); // Keep Drone in inherited draft; must create exactly one new unit.
        f = flow.ActiveFight; drone = f.Player.GetComponent<PlayerDetachedDroneArm>();
        bool redeploySingle = drone.Unit != null && Phase9UnitCount() == 1;
        f.BossHealth.TakeDamage(800, f.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        bool victoryClean = drone.Unit == null && Phase9UnitCount() == 0;
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow);
        yield return FixtureNextOfficer(flow);
        // Equip then remove through the actual unlocked cycle before deployment.
        for (int i = 0; i < 4; i++) yield return ClickSessionButton("LeftArmButton");
        yield return DeployAndFocus(flow);
        f = flow.ActiveFight;
        bool removed = !f.Deployment.HasDetachedDrone && f.Player.GetComponent<PlayerDetachedDroneArm>().Unit == null
            && f.Player.GetComponent<OfficerVisual>().Part(LoadoutSlot.LeftArm).enabled && Phase9UnitCount() == 0;
        Check("Drone Cleanup", deathClean && redeploySingle && victoryClean && removed,
            "death removes unit; same-Drone redeploy has one; victory removes it; equip/remove before next deployment produces intact arm/no Drone");
        f.BossHealth.TakeDamage(800, f.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("Core Flow", flow.State == GameSessionFlow.FlowState.Result && PoliceArchive.Current.CompletedAchievements.Count == 1
            && PoliceArchive.Current.UnlockedHiddenAugmentations.Count == 1 && PoliceArchive.Current.UnlockedEndings.Count == 0,
            "Archive/back, victories, New Game, defeat/Continue/loadout/redeploy complete; repeated wins do not duplicate records or author endings");
    }

    private static int Phase9UnitCount() => UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,
        FindObjectsSortMode.None).Count(x => x.gameObject.name == "DetachedDrone_TEST");
}
#endif
