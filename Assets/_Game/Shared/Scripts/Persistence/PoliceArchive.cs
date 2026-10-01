using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent unlock facts plus one frozen record per resolved combat attempt. No live session/combat state.</summary>
public sealed class PoliceArchiveProfile
{
    private readonly List<string> endings = new List<string>();
    private readonly List<string> achievements = new List<string>();
    private readonly List<string> hiddenAugmentations = new List<string>();
    private readonly List<PoliceRunRecord> runs = new List<PoliceRunRecord>();
    public IReadOnlyList<string> UnlockedEndings => endings.AsReadOnly();
    public IReadOnlyList<string> CompletedAchievements => achievements.AsReadOnly();
    public IReadOnlyList<string> UnlockedHiddenAugmentations => hiddenAugmentations.AsReadOnly();
    /// <summary>Oldest first. Records are frozen copies; later loadouts never change them.</summary>
    public IReadOnlyList<PoliceRunRecord> RunRecords => runs.AsReadOnly();
    public PoliceRunRecord LatestRun => runs.Count > 0 ? runs[runs.Count - 1] : null;
    /// <summary>Sequential and persistent: never reuses a number from completed history.</summary>
    public int NextOfficerNumber
    {
        get
        {
            int highest = 0;
            foreach (var run in runs) highest = Math.Max(highest, run.OfficerNumber);
            return highest + 1;
        }
    }

    public bool HasAchievement(string id) => achievements.Contains(id);
    public bool HasHiddenAugmentation(string id) => hiddenAugmentations.Contains(id);

    public bool UnlockEnding(string id) => Add(endings, id);
    public bool CompleteAchievement(string id) => Add(achievements, id);
    public bool UnlockHiddenAugmentation(string id) => Add(hiddenAugmentations, id);
    public void AddRunRecord(PoliceRunRecord record)
    {
        if (record == null || !record.IsValid) throw new ArgumentException("A complete run record is required.");
        runs.Add(record);
    }

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
        unlockedHiddenAugmentations = hiddenAugmentations.ToArray(),
        runRecords = runs.ToArray()
    };

    internal static bool TryFromData(PoliceArchiveData data, out PoliceArchiveProfile profile)
    {
        profile = null;
        if (data == null || data.saveVersion < 1 || data.saveVersion > PoliceArchiveStore.CurrentVersion ||
            !Valid(data.unlockedEndings) || !Valid(data.completedAchievements) || !Valid(data.unlockedHiddenAugmentations)) return false;
        // Minimal v1 -> v2 migration: version 1 had no run history, so it loads with an empty one.
        var records = data.saveVersion == 1 ? Array.Empty<PoliceRunRecord>() : data.runRecords;
        if (records == null) return false;
        foreach (var record in records) if (record == null || !record.IsValid) return false;
        profile = new PoliceArchiveProfile();
        profile.endings.AddRange(data.unlockedEndings);
        profile.achievements.AddRange(data.completedAchievements);
        profile.hiddenAugmentations.AddRange(data.unlockedHiddenAugmentations);
        profile.runs.AddRange(records);
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
    public PoliceRunRecord[] runRecords; // Added in version 2.
}

/// <summary>
/// TEMP generated officer data for one mission. Generated once before deployment and frozen into its run record.
/// No names, biographies or other fields. Ranges and the rank pool are TEST placeholders.
/// </summary>
public sealed class OfficerProfile
{
    public static readonly string[] TempRanks = { "OFFICER", "SERGEANT", "LIEUTENANT" };
    public int OfficerNumber { get; }
    public int Age { get; }
    public string Rank { get; }
    public int YearsOfService { get; }
    public int MissionsCompleted { get; }
    public int MissionSuccessRate { get; }

    private OfficerProfile(int number, int age, string rank, int years, int missions, int successRate)
    {
        OfficerNumber = number; Age = age; Rank = rank; YearsOfService = years; MissionsCompleted = missions; MissionSuccessRate = successRate;
    }

    public static OfficerProfile Generate(int officerNumber)
    {
        // System.Random keeps gameplay's UnityEngine.Random state untouched.
        var random = new System.Random();
        int age = random.Next(22, 56); // TEMP: 22-55 inclusive.
        int years = random.Next(1, age - 21 + 1); // Never more than (age - 21).
        int missions = random.Next(years * 2, years * 6 + 1); // Loose TEMP scale with service, at least 2.
        int successes = random.Next((missions + 1) / 2, missions + 1); // Fictional prior history; never simulated per mission.
        int rate = Mathf.RoundToInt(100f * successes / missions);
        return new OfficerProfile(Math.Max(1, officerNumber), age, TempRanks[random.Next(TempRanks.Length)], years, missions, rate);
    }
}

/// <summary>
/// One completed combat attempt, frozen at resolution: officer profile, the immutable deployment's seven slot IDs
/// (LoadoutSlot ordinal order) and BossFight's already-frozen result values. Never recomputed afterwards.
/// </summary>
[Serializable]
public sealed class PoliceRunRecord
{
    [SerializeField] private int officerNumber;
    [SerializeField] private int age;
    [SerializeField] private string rank;
    [SerializeField] private int yearsOfService;
    [SerializeField] private int missionsCompleted;
    [SerializeField] private int missionSuccessRate;
    [SerializeField] private bool success;
    [SerializeField] private string[] slotItemIds;
    [SerializeField] private float bodyModificationPercent;
    [SerializeField] private float combatDuration;
    [SerializeField] private int bossHealth;
    [SerializeField] private int playerHealth;
    [SerializeField] private float accuracyPercent;

    public int OfficerNumber => officerNumber;
    public int Age => age;
    public string Rank => rank;
    public int YearsOfService => yearsOfService;
    public int MissionsCompleted => missionsCompleted;
    public int MissionSuccessRate => missionSuccessRate;
    public bool Success => success;
    public float BodyModificationPercent => bodyModificationPercent;
    public float CombatDuration => combatDuration;
    public int BossHealth => bossHealth;
    public int PlayerHealth => playerHealth;
    public float AccuracyPercent => accuracyPercent;
    public string SlotItemId(LoadoutSlot slot) => slotItemIds[LoadoutRules.Index(slot)];

    private PoliceRunRecord() { } // For JsonUtility.

    public PoliceRunRecord(OfficerProfile officer, DeploymentSnapshot deployment, bool success,
        float combatDuration, int bossHealth, int playerHealth, float accuracyPercent)
    {
        if (officer == null) throw new ArgumentNullException(nameof(officer));
        if (deployment == null) throw new ArgumentNullException(nameof(deployment));
        officerNumber = officer.OfficerNumber; age = officer.Age; rank = officer.Rank; yearsOfService = officer.YearsOfService;
        missionsCompleted = officer.MissionsCompleted; missionSuccessRate = officer.MissionSuccessRate;
        this.success = success;
        slotItemIds = new string[LoadoutRules.SlotCount];
        for (int i = 0; i < slotItemIds.Length; i++) slotItemIds[i] = deployment.GetSlot((LoadoutSlot)i).ItemId ?? "";
        bodyModificationPercent = deployment.BodyModificationPercent;
        this.combatDuration = combatDuration; this.bossHealth = bossHealth; this.playerHealth = playerHealth; this.accuracyPercent = accuracyPercent;
    }

    internal bool IsValid
    {
        get
        {
            if (officerNumber < 1 || string.IsNullOrWhiteSpace(rank) || slotItemIds == null || slotItemIds.Length != LoadoutRules.SlotCount) return false;
            foreach (var id in slotItemIds) if (id == null) return false;
            return true;
        }
    }
}

/// <summary>One local profile, independent of session New Game. TEMP victory reward is not final design.</summary>
public static class PoliceArchive
{
    // TEMP / placeholder condition: defeat the Boss once. Replace when the real challenge is designed.
    public const string TempFirstVictory = "TEMP_FIRST_VICTORY";
    // TEMP placeholder endings and TEST threshold only. Replaceable; not final names, routing or design.
    public const string TempEndingUnder50 = "TEMP_ENDING_UNDER_50";
    public const string TempEnding50OrMore = "TEMP_ENDING_50_OR_MORE";
    public const float TempEndingThresholdPercent = 50f;
    public static string LastSaveError { get; private set; }
    private static PoliceArchiveStore store;
    private static PoliceArchiveProfile current;
    public static PoliceArchiveProfile Current { get { EnsureLoaded(); return current; } }
    public static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "PoliceArchive", "archive.json");
    public static PoliceArchiveLoadStatus LoadStatus { get { EnsureLoaded(); return store.LastLoadStatus; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCache() { store = null; current = null; LastSaveError = null; } // Domain Reload can be disabled; disk is untouched.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureLoaded()
    {
        if (current != null) return;
        store = new PoliceArchiveStore(SavePath);
        current = store.Load();
        if (!string.IsNullOrEmpty(store.LastError)) Debug.LogWarning("Police Archive: " + store.LastError);
    }

    /// <summary>TEMP routing: only the winning deployment's Body Modification % decides the placeholder ending.</summary>
    public static string TempEndingFor(float bodyModificationPercent) =>
        bodyModificationPercent < TempEndingThresholdPercent ? TempEndingUnder50 : TempEnding50OrMore;

    /// <summary>
    /// Call exactly once per resolved combat attempt (victory or defeat). Adds the frozen record, evaluates the
    /// placeholder achievement and ending from that record, then writes the Archive once.
    /// </summary>
    public static bool RecordCompletedRun(PoliceRunRecord run, out string error)
    {
        Current.AddRunRecord(run);
        EvaluateCompletedRun(Current, run);
        return Save(out error);
    }

    // Achievements and endings read the completed run, not a separate progression path.
    private static void EvaluateCompletedRun(PoliceArchiveProfile profile, PoliceRunRecord run)
    {
        if (!run.Success) return;
        // TEMP_FIRST_VICTORY: any Boss victory. Its existing reward unlocks the Detached Drone Arm. Both are idempotent.
        profile.CompleteAchievement(TempFirstVictory);
        profile.UnlockHiddenAugmentation(LoadoutRules.DetachedDroneArm);
        profile.UnlockEnding(TempEndingFor(run.BodyModificationPercent));
    }

    public static bool Save(out string error)
    {
        EnsureLoaded();
        bool saved = store.TrySave(current, out error);
        LastSaveError = error;
        return saved;
    }
}
