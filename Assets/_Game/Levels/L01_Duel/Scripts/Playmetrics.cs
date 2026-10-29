using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoGhost.Duel;
using UnityEngine;

// Duel-owned observations only. No gameplay decisions or Shared -> level dependencies.
public static class PlayMetrics
{
    [Serializable] public sealed class ActionStats
    {
        public string action;
        public int count, drops;
        public float seconds, holdSeconds;
    }
    [Serializable] public sealed class OperationStats
    {
        public string operation, unit;
        public int automaticCount, manualCount, automaticDrops, manualDrops;
        public float automaticSeconds, manualSeconds, assistedSeconds, automaticHoldSeconds;
        public bool hasData;
        public float automaticPercent;
        public List<ActionStats> actions = new List<ActionStats>();
        public void Calculate()
        {
            float a = unit == "seconds" ? automaticSeconds : automaticCount;
            float m = unit == "seconds" ? manualSeconds : manualCount;
            float total = a + m + (unit == "seconds" ? assistedSeconds : 0);
            hasData = total > 0;
            automaticPercent = hasData ? 100f * a / total : 0;
        }
    }
    [Serializable] public sealed class Attempt
    {
        public string attemptId, startedUtc, endedUtc, result = "in_progress";
        public int wave;
        public bool started, developerWaveJump, tuningChanged, testData;
        public float seconds;
        public string endReason;
        public int playerHealth = -1, playerMaxHealth = -1;
        public int gunnerHealth = -1, gunnerMaxHealth = -1, swordHealth = -1, swordMaxHealth = -1;
        public string[] ruleDescriptions;
        public OperationRules[] rules;
        public float[] initialTuning, finalTuning;
        public OperationStats[] operations = NewOperations();
        public bool hasAutomationData;
        public float automaticPercent;
        // Retain the initial metrics reader API; properties are not duplicated in JSON.
        public int Wave { get => wave; set => wave = value; }
        public bool Died { get => result == "died"; set => result = value ? "died" : "won"; }
        public float Seconds { get => seconds; set => seconds = value; }
        public int Jumps { get => Total(operations[1]); set => operations[1].manualCount = value - operations[1].automaticCount; }
        public int Dodges { get => Total(operations[4]); set => operations[4].manualCount = value - operations[4].automaticCount; }
        public int Shots { get => Total(operations[3]); set => operations[3].manualCount = value - operations[3].automaticCount; }
    }
    [Serializable] public sealed class RunData
    {
        public int schemaVersion = 4;
        public string sessionId, runId, startedUtc, updatedUtc;
        public string playerName = "YOU", endedUtc, endReason;
        public bool characterCreated, finished, waveOneSkipped;
        public string playtestReminder = "not_shown";
        public double playSeconds, creationSeconds, configurationSeconds, trainingSeconds, preparationSeconds, fightSeconds, pauseSeconds, resultSeconds;
        public int helpOpened, rulesAdded, rulesEdited, rulesRemoved, rulesReordered, maxRulesEquipped, trainingDeaths;
        public double firstRuleAtSeconds = -1, firstFightAtSeconds = -1;
        public string[] latestRules = Array.Empty<string>();
        public Attempt activeAttempt;
        public string applicationVersion, platform;
        public bool editor, levelCleared, testData;
        public float combatSeconds, levelClearSeconds;
        public string percentageMethod = "Equal mean of nonempty operation ratios. Move/Aim: seconds. Jump/Fire/Dodge: accepted counts. Assisted aim is in the denominator, not the automatic numerator. Holds and drops excluded. This is not a comprehension or autonomy score.";
        public string[] tuningNames = Enum.GetNames(typeof(DeveloperSetting));
        public int[] deathsByWave = new int[4];
        public OperationStats[] operations = NewOperations();
        public bool hasAutomationData;
        public float automaticPercent;
        public List<Attempt> attempts = new List<Attempt>();
    }

    static string sessionId;
    static RunData run;
    static bool counting;
    static double lastClock, lastSave;
    static DuelPhase stage;
    static bool stagePaused;
    public static readonly int[] DeathsByWave = new int[4];
    public static readonly List<Attempt> History = new List<Attempt>();
    static int Total(OperationStats op) => op.automaticCount + op.manualCount;
    static Attempt Last => History.Count == 0 ? null : History[History.Count - 1];
    public static int Jumps => (Current ?? Last)?.Jumps ?? 0;
    public static int Dodges => (Current ?? Last)?.Dodges ?? 0;
    public static int Shots => (Current ?? Last)?.Shots ?? 0;
    public static float LastSeconds => Last?.seconds ?? 0;
    public static float RunSeconds => run?.combatSeconds ?? 0;
    public static bool LevelCleared => run?.levelCleared ?? false;
    public static float LevelClearSeconds => run?.levelClearSeconds ?? 0;
    public static Attempt Current { get; private set; }
    public static RunData Run => run;
    public static string LastSaveError { get; private set; }
    public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "Analytics");
    public static string JsonPath => run == null ? "" : Path.Combine(DirectoryPath, run.runId + ".json");
    public static string CsvPath => run == null ? "" : Path.Combine(DirectoryPath, run.runId + ".csv");
    public static bool RunActive => run != null && !run.finished;
    public static bool CheckpointDue => RunActive && Time.realtimeSinceStartupAsDouble - lastSave >= 10;
    public static bool Counting => counting && Current != null;
    public static string BossName(int wave) => wave switch { 1 => "Gunner", 2 => "Swordsman", 3 => "Final Boss", DuelGame.BonusWave => "Bonus Duo", _ => "-" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitializeSession()
    {
        sessionId = Guid.NewGuid().ToString("N");
        run = null; Current = null; counting = false; LastSaveError = null;
        lastClock = lastSave = 0; stage = DuelPhase.Menu; stagePaused = false;
        History.Clear(); Array.Clear(DeathsByWave, 0, DeathsByWave.Length);
    }
    static OperationStats[] NewOperations()
    {
        var result = new OperationStats[5];
        for (int i = 0; i < result.Length; i++) result[i] = new OperationStats {
            operation = ((ControlOperation)i).ToString(), unit = i == 0 || i == 2 ? "seconds" : "count" };
        return result;
    }
    public static void ResetRun()
    {
        if (Current != null) Finish("abandoned", false);
        EndRun("new_game");
        if (string.IsNullOrEmpty(sessionId)) sessionId = Guid.NewGuid().ToString("N");
        History.Clear(); Array.Clear(DeathsByWave, 0, DeathsByWave.Length);
        run = new RunData { sessionId = sessionId, runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"),
            startedUtc = DateTime.UtcNow.ToString("o"), applicationVersion = Application.version,
            platform = Application.platform.ToString(), editor = Application.isEditor, attempts = History, deathsByWave = DeathsByWave };
        Current = null; counting = false; LastSaveError = null;
        stage = DuelPhase.CharacterCreation; stagePaused = false;
        lastClock = lastSave = Time.realtimeSinceStartupAsDouble;
        Save();
    }
    public static void SetName(string name, bool confirmed = false)
    {
        if (!RunActive) return;
        run.playerName = string.IsNullOrWhiteSpace(name) ? "YOU" : name.Trim();
        run.characterCreated |= confirmed;
    }
    public static void TrackTime()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        double elapsed = Math.Max(0, now - lastClock);
        lastClock = now;
        if (!RunActive) return;
        run.playSeconds += elapsed;
        if (stagePaused) run.pauseSeconds += elapsed;
        else switch (stage)
        {
            case DuelPhase.CharacterCreation: run.creationSeconds += elapsed; break;
            case DuelPhase.Configure: run.configurationSeconds += elapsed; break;
            case DuelPhase.Training: run.trainingSeconds += elapsed; break;
            case DuelPhase.Prepare: run.preparationSeconds += elapsed; break;
            case DuelPhase.Fight: run.fightSeconds += elapsed; break;
            default: run.resultSeconds += elapsed; break;
        }
    }
    public static void SetStage(DuelPhase phase, bool paused)
    {
        TrackTime(); stage = phase; stagePaused = paused;
    }
    public static void EndRun(string reason)
    {
        if (!RunActive) return;
        TrackTime();
        if (Current != null) Finish("abandoned", false, reason);
        run.finished = true; run.endReason = reason; run.endedUtc = DateTime.UtcNow.ToString("o");
        Save();
    }
    public static string[] Describe(ConditionalLoadout loadout)
    {
        var rows = loadout.OrderedRules(); var result = new string[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var expression = ConditionalCatalog.Expression(rows[i]);
            result[i] = expression.Conditions + " -> " + expression.Action;
        }
        return result;
    }
    public static void RecordConfiguration(ConditionalLoadout loadout, string change = null)
    {
        if (!RunActive) return;
        TrackTime();
        run.latestRules = Describe(loadout);
        run.maxRulesEquipped = Math.Max(run.maxRulesEquipped, loadout.TotalRules);
        if (loadout.TotalRules > 0 && run.firstRuleAtSeconds < 0) run.firstRuleAtSeconds = run.playSeconds;
        switch (change)
        {
            case "add": run.rulesAdded++; break;
            case "edit": run.rulesEdited++; break;
            case "remove": run.rulesRemoved++; break;
            case "reorder": run.rulesReordered++; break;
        }
        Save();
    }
    public static void OpenHelp() { if (RunActive) { run.helpOpened++; Save(); } }
    public static void RecordTrainingDeath() { if (RunActive) { run.trainingDeaths++; Save(); } }
    public static void Reminder(string choice)
    {
        if (!RunActive) return;
        run.playtestReminder = choice;
        run.waveOneSkipped |= choice == "yes";
        Save();
    }
    public static void BeginAttempt(int wave, ConditionalLoadout loadout, float[] tuning, bool waveJump, bool tuned)
    {
        if (wave < 1 || wave > 2) return;
        if (!RunActive) ResetRun();
        if (Current != null) Finish("abandoned", false);
        Current = new Attempt { attemptId = Guid.NewGuid().ToString("N"), wave = wave,
            startedUtc = DateTime.UtcNow.ToString("o"), rules = loadout.Copy().Operations,
            initialTuning = (float[])tuning.Clone(), finalTuning = (float[])tuning.Clone(),
            developerWaveJump = waveJump, tuningChanged = tuned, testData = waveJump || tuned };
        Current.ruleDescriptions = Describe(loadout);
        run.latestRules = Current.ruleDescriptions;
        counting = false;
    }
    public static void StartCounting()
    {
        if (Current == null) return;
        TrackTime(); Current.started = true; counting = true;
        if (run.firstFightAtSeconds < 0) run.firstFightAtSeconds = run.playSeconds;
    }
    public static void BeginAttempt(int wave) => BeginAttempt(wave, new ConditionalLoadout(), Array.Empty<float>(), false, false);
    public static void CountJump() => Record(ControlOperation.Jump, null, count: 1);
    public static void CountDodge() => Record(ControlOperation.Dodge, null, count: 1);
    public static void EndAttempt(int wave, bool died, float seconds, int shots, bool levelCleared)
    {
        if (Current == null) return;
        int unobserved = Mathf.Max(0, shots - Current.Shots);
        if (unobserved > 0) Record(ControlOperation.Fire, null, count: unobserved);
        SetClock(seconds); Finish(died ? "died" : "won", levelCleared);
    }
    public static void SetClock(float seconds) { if (Current != null) Current.seconds = seconds; }
    public static void MarkTuning(float[] tuning)
    {
        if (Current == null) return;
        Current.tuningChanged = Current.testData = true;
        Current.finalTuning = (float[])tuning.Clone();
    }
    public static void MarkTestData()
    {
        if (RunActive) run.testData = true;
        if (Current != null) Current.testData = true;
    }
    public static void Record(ControlOperation operation, string automaticAction, int count = 0, float seconds = 0, bool hold = false, bool drop = false)
    {
        if (!Counting) return;
        var op = Current.operations[(int)operation];
        bool automatic = !string.IsNullOrEmpty(automaticAction);
        if (hold) { if (automatic) op.automaticHoldSeconds += seconds; }
        else if (drop) { if (automatic) op.automaticDrops += count; else op.manualDrops += count; }
        else if (operation == ControlOperation.Aim && automaticAction == "Q06") op.assistedSeconds += seconds;
        else if (automatic) { op.automaticCount += count; op.automaticSeconds += seconds; }
        else { op.manualCount += count; op.manualSeconds += seconds; }
        string key = automatic ? automaticAction : "Manual";
        ActionStats detail = null;
        foreach (var candidate in op.actions) if (candidate.action == key) { detail = candidate; break; }
        if (detail == null) { detail = new ActionStats { action = key }; op.actions.Add(detail); }
        if (hold) detail.holdSeconds += seconds;
        else if (drop) detail.drops += count;
        else { detail.count += count; detail.seconds += seconds; }
    }
    public static void Calculate(OperationStats[] operations, out bool hasData, out float percentage)
    {
        int valid = 0; percentage = 0;
        foreach (var op in operations) { op.Calculate(); if (op.hasData) { valid++; percentage += op.automaticPercent; } }
        hasData = valid > 0;
        if (hasData) percentage /= valid;
    }
    static void Add(OperationStats[] target, OperationStats[] source)
    {
        for (int i = 0; i < target.Length; i++)
        {
            var t = target[i]; var s = source[i];
            t.automaticCount += s.automaticCount; t.manualCount += s.manualCount;
            t.automaticSeconds += s.automaticSeconds; t.manualSeconds += s.manualSeconds;
            t.assistedSeconds += s.assistedSeconds;
            t.automaticHoldSeconds += s.automaticHoldSeconds;
            t.automaticDrops += s.automaticDrops; t.manualDrops += s.manualDrops;
            foreach (var a in s.actions)
            {
                var d = t.actions.Find(x => x.action == a.action);
                if (d == null) { d = new ActionStats { action = a.action }; t.actions.Add(d); }
                d.count += a.count; d.seconds += a.seconds; d.holdSeconds += a.holdSeconds; d.drops += a.drops;
            }
        }
    }
    public static OperationStats[] RunOperations(bool includeCurrent)
    {
        var total = NewOperations();
        if (run != null) foreach (var a in run.attempts) Add(total, a.operations);
        if (includeCurrent && Current != null) Add(total, Current.operations);
        return total;
    }
    public static void Finish(string result, bool levelCleared, string reason = null)
    {
        if (Current == null) return;
        counting = false;
        Current.result = result; Current.endedUtc = DateTime.UtcNow.ToString("o");
        Current.endReason = reason ?? result;
        Calculate(Current.operations, out Current.hasAutomationData, out Current.automaticPercent);
        run.attempts.Add(Current);
        run.combatSeconds += Current.seconds;
        run.testData |= Current.testData;
        if (result == "died" && Current.wave >= 1 && Current.wave < run.deathsByWave.Length) run.deathsByWave[Current.wave]++;
        Current = null;
        run.operations = RunOperations(false);
        Calculate(run.operations, out run.hasAutomationData, out run.automaticPercent);
        if (levelCleared) { run.levelCleared = true; run.levelClearSeconds = run.combatSeconds; }
        Save();
    }
    public static bool Save()
    {
        if (run == null) return true;
        try
        {
            TrackTime();
            Directory.CreateDirectory(DirectoryPath);
            run.updatedUtc = DateTime.UtcNow.ToString("o");
            run.activeAttempt = Current;
            if (Current != null) Calculate(Current.operations, out Current.hasAutomationData, out Current.automaticPercent);
            WriteFile(JsonPath, JsonUtility.ToJson(run, true));
            lastSave = Time.realtimeSinceStartupAsDouble;
            LastSaveError = null;
            return true;
        }
        catch (Exception e) { lastSave = Time.realtimeSinceStartupAsDouble; LastSaveError = e.Message; Debug.LogWarning("Analytics save failed: " + e.Message); return false; }
    }
    static void WriteFile(string path, string content)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
}
