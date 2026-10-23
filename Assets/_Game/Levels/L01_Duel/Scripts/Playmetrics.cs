using System;
using System.Collections.Generic;
using System.Globalization;
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
        public float automaticSeconds, manualSeconds, automaticHoldSeconds;
        public bool hasData;
        public float automaticPercent;
        public List<ActionStats> actions = new List<ActionStats>();
        public void Calculate()
        {
            float a = unit == "seconds" ? automaticSeconds : automaticCount;
            float m = unit == "seconds" ? manualSeconds : manualCount;
            hasData = a + m > 0;
            automaticPercent = hasData ? 100f * a / (a + m) : 0;
        }
    }
    [Serializable] public sealed class Attempt
    {
        public string attemptId, startedUtc, endedUtc, result = "in_progress";
        public int wave;
        public bool started, developerWaveJump, tuningChanged, testData;
        public float seconds;
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
        public int schemaVersion = 2;
        public string sessionId, runId, startedUtc, updatedUtc;
        public string applicationVersion, platform;
        public bool editor, levelCleared, testData;
        public float combatSeconds, levelClearSeconds;
        public string percentageMethod = "Equal mean of nonempty operation ratios. Move/Aim: seconds. Jump/Fire/Dodge: accepted counts. Holds and drops excluded; reported separately.";
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
    public static bool Counting => counting && Current != null;
    public static string BossName(int wave) => wave switch { 1 => "Gunner", 2 => "Swordsman", 3 => "Duo", _ => "-" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitializeSession()
    {
        sessionId = Guid.NewGuid().ToString("N");
        run = null; Current = null; counting = false; LastSaveError = null;
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
        if (string.IsNullOrEmpty(sessionId)) sessionId = Guid.NewGuid().ToString("N");
        History.Clear(); Array.Clear(DeathsByWave, 0, DeathsByWave.Length);
        run = new RunData { sessionId = sessionId, runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"),
            startedUtc = DateTime.UtcNow.ToString("o"), applicationVersion = Application.version,
            platform = Application.platform.ToString(), editor = Application.isEditor, attempts = History, deathsByWave = DeathsByWave };
        Current = null; counting = false; LastSaveError = null;
    }
    public static void BeginAttempt(int wave, ConditionalLoadout loadout, float[] tuning, bool waveJump, bool tuned)
    {
        if (run == null) ResetRun();
        if (Current != null) Finish("abandoned", false);
        Current = new Attempt { attemptId = Guid.NewGuid().ToString("N"), wave = wave,
            startedUtc = DateTime.UtcNow.ToString("o"), rules = loadout.Copy().Operations,
            initialTuning = (float[])tuning.Clone(), finalTuning = (float[])tuning.Clone(),
            developerWaveJump = waveJump, tuningChanged = tuned, testData = waveJump || tuned };
        counting = false;
    }
    public static void StartCounting() { if (Current != null) { Current.started = true; counting = true; } }
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
    public static void MarkTestData() { if (Current != null) Current.testData = true; }
    public static void Record(ControlOperation operation, string automaticAction, int count = 0, float seconds = 0, bool hold = false, bool drop = false)
    {
        if (!Counting) return;
        var op = Current.operations[(int)operation];
        bool automatic = !string.IsNullOrEmpty(automaticAction);
        if (hold) { if (automatic) op.automaticHoldSeconds += seconds; }
        else if (drop) { if (automatic) op.automaticDrops += count; else op.manualDrops += count; }
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
    public static void Finish(string result, bool levelCleared)
    {
        if (Current == null) return;
        counting = false;
        Current.result = result; Current.endedUtc = DateTime.UtcNow.ToString("o");
        Calculate(Current.operations, out Current.hasAutomationData, out Current.automaticPercent);
        run.attempts.Add(Current);
        run.combatSeconds += Current.seconds;
        run.testData |= Current.testData;
        if (result == "died" && Current.wave >= 1 && Current.wave <= 3) run.deathsByWave[Current.wave]++;
        Current = null;
        run.operations = RunOperations(false);
        Calculate(run.operations, out run.hasAutomationData, out run.automaticPercent);
        if (levelCleared) { run.levelCleared = true; run.levelClearSeconds = run.combatSeconds; }
        Save();
    }
    public static bool Save()
    {
        if (run == null || run.attempts.Count == 0) return true;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            run.updatedUtc = DateTime.UtcNow.ToString("o");
            WriteFile(JsonPath, JsonUtility.ToJson(run, true));
            WriteFile(CsvPath, Csv());
            LastSaveError = null;
            return true;
        }
        catch (Exception e) { LastSaveError = e.Message; Debug.LogWarning("Analytics save failed: " + e.Message); return false; }
    }
    static void WriteFile(string path, string content)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
    static string Number(float n) => n.ToString("0.######", CultureInfo.InvariantCulture);
    static string Csv()
    {
        var sb = new StringBuilder("schema_version,session_id,run_id,attempt_id,kind,wave,boss,result,started_utc,ended_utc,seconds,editor,test_data,wave_jump,tuning_changed,automation_percent");
        foreach (var op in run.operations)
            sb.Append(',').Append(op.operation).Append("_auto_count,").Append(op.operation).Append("_manual_count,")
                .Append(op.operation).Append("_auto_seconds,").Append(op.operation).Append("_manual_seconds,")
                .Append(op.operation).Append("_hold_seconds,").Append(op.operation).Append("_auto_drops,")
                .Append(op.operation).Append("_manual_drops,").Append(op.operation).Append("_auto_percent");
        sb.AppendLine(",deaths_w1,deaths_w2,deaths_w3");
        foreach (var a in run.attempts)
            CsvRow(sb, a.attemptId, "fight", a.wave, a.result, a.startedUtc, a.endedUtc, a.seconds, a.testData,
                a.developerWaveJump, a.tuningChanged, a.operations, a.hasAutomationData, a.automaticPercent, null);
        CsvRow(sb, "", "run_summary", 0, run.levelCleared ? "clear" : "partial", run.startedUtc, run.updatedUtc,
            run.combatSeconds, run.testData, false, false, run.operations, run.hasAutomationData, run.automaticPercent, run.deathsByWave);
        return sb.ToString();
    }
    static void CsvRow(StringBuilder sb, string id, string kind, int wave, string result, string start, string end,
        float seconds, bool test, bool waveJump, bool tuned, OperationStats[] operations, bool hasData, float percentage, int[] deaths)
    {
        sb.Append("2,").Append(run.sessionId).Append(',').Append(run.runId).Append(',').Append(id).Append(',').Append(kind)
            .Append(',').Append(wave).Append(',').Append(BossName(wave)).Append(',').Append(result).Append(',').Append(start).Append(',').Append(end)
            .Append(',').Append(Number(seconds)).Append(',').Append(run.editor).Append(',').Append(test).Append(',').Append(waveJump).Append(',').Append(tuned)
            .Append(',').Append(hasData ? Number(percentage) : "");
        foreach (var op in operations)
            sb.Append(',').Append(op.automaticCount).Append(',').Append(op.manualCount).Append(',').Append(Number(op.automaticSeconds))
                .Append(',').Append(Number(op.manualSeconds)).Append(',').Append(Number(op.automaticHoldSeconds))
                .Append(',').Append(op.automaticDrops).Append(',').Append(op.manualDrops).Append(',').Append(op.hasData ? Number(op.automaticPercent) : "");
        for (int i = 1; i <= 3; i++) sb.Append(',').Append(deaths == null ? "" : deaths[i].ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
    }
}
