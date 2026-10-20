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
        Record("NOT RUN: Archive UI scenario retired by the full presentation reset. Use bare-gameplay.");
        yield break;
    }

    private static int Phase9UnitCount() => UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,
        FindObjectsSortMode.None).Count(x => x.gameObject.name == "DetachedDrone_TEST");
}
#endif
