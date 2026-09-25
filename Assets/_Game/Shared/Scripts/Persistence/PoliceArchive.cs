using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent unlock facts only. No officer, loadout, combat state or final content IDs.</summary>
public sealed class PoliceArchiveProfile
{
    private readonly List<string> endings = new List<string>();
    private readonly List<string> achievements = new List<string>();
    private readonly List<string> hiddenAugmentations = new List<string>();
    public IReadOnlyList<string> UnlockedEndings => endings.AsReadOnly();
    public IReadOnlyList<string> CompletedAchievements => achievements.AsReadOnly();
    public IReadOnlyList<string> UnlockedHiddenAugmentations => hiddenAugmentations.AsReadOnly();

    public bool UnlockEnding(string id) => Add(endings, id);
    public bool CompleteAchievement(string id) => Add(achievements, id);
    public bool UnlockHiddenAugmentation(string id) => Add(hiddenAugmentations, id);

    private static bool Add(List<string> list, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Archive record ID is required.");
        if (list.Contains(id)) return false;
        list.Add(id);
        return true;
    }

    internal PoliceArchiveData ToData() => new PoliceArchiveData
    {
        saveVersion = PoliceArchiveStore.CurrentVersion,
        unlockedEndings = endings.ToArray(),
        completedAchievements = achievements.ToArray(),
        unlockedHiddenAugmentations = hiddenAugmentations.ToArray()
    };

    internal static bool TryFromData(PoliceArchiveData data, out PoliceArchiveProfile profile)
    {
        profile = null;
        if (data == null || data.saveVersion != PoliceArchiveStore.CurrentVersion ||
            !Valid(data.unlockedEndings) || !Valid(data.completedAchievements) || !Valid(data.unlockedHiddenAugmentations)) return false;
        profile = new PoliceArchiveProfile();
        profile.endings.AddRange(data.unlockedEndings);
        profile.achievements.AddRange(data.completedAchievements);
        profile.hiddenAugmentations.AddRange(data.unlockedHiddenAugmentations);
        return true;
    }

    private static bool Valid(string[] values)
    {
        if (values == null) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values) if (string.IsNullOrWhiteSpace(value) || !seen.Add(value)) return false;
        return true;
    }
}

// No default field values: missing JSON fields must fail validation, not silently become valid data.
[Serializable]
internal sealed class PoliceArchiveData
{
    public int saveVersion;
    public string[] unlockedEndings;
    public string[] completedAchievements;
    public string[] unlockedHiddenAugmentations;
}

/// <summary>One local profile, independent of GameSessionFlow.NewGame. No scene object or Archive UI.</summary>
public static class PoliceArchive
{
    private static PoliceArchiveStore store;
    private static PoliceArchiveProfile current;
    public static PoliceArchiveProfile Current { get { EnsureLoaded(); return current; } }
    public static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "PoliceArchive", "archive.json");
    public static PoliceArchiveLoadStatus LoadStatus { get { EnsureLoaded(); return store.LastLoadStatus; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCache() { store = null; current = null; } // Domain Reload can be disabled; disk is untouched.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureLoaded()
    {
        if (current != null) return;
        store = new PoliceArchiveStore(SavePath);
        current = store.Load();
        if (!string.IsNullOrEmpty(store.LastError)) Debug.LogWarning("Police Archive: " + store.LastError);
    }

    public static bool Save(out string error)
    {
        EnsureLoaded();
        return store.TrySave(current, out error);
    }
}
