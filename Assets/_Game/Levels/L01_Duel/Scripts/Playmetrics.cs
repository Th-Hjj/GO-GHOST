using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// PLAY METRICS (static counters; no scene object needed).
/// Tracks, per session:
///   - run time (sum of every fight's duration, retries included) and the time the level was cleared
///   - deaths per boss (wave 1 = Gunner, wave 2 = Swordsman, wave 3 = Duo)
///   - jumps, dodges and shots in each fight (live, then stored in History)
/// Every finished fight is also appended to metrics.csv in Application.persistentDataPath,
/// so playtest data survives after the game closes.
///
/// Functions:
///   ResetRun()       clears everything (New Game / Continue).
///   BeginAttempt()   a fight was set up (countdown starts); resets the per-fight counters.
///   StartCounting()  the countdown ended; jumps and dodges count from here.
///   CountJump() / CountDodge()  called by PlayerMovement when a jump / dodge is accepted.
///   EndAttempt()     the fight ended (death or win); stores the result and writes the CSV row.
/// </summary>
public static class PlayMetrics
{
    public sealed class Attempt
    {
        public int Wave;
        public bool Died;
        public float Seconds;
        public int Jumps, Dodges, Shots;
    }

    public static readonly int[] DeathsByWave = new int[4];          // index 1..3
    public static readonly List<Attempt> History = new List<Attempt>();

    public static int Jumps { get; private set; }
    public static int Dodges { get; private set; }
    public static int Shots { get; private set; }                    // final value of the last finished fight
    public static float LastSeconds { get; private set; }            // length of the last finished fight
    public static float RunSeconds { get; private set; }             // all finished fights added together
    public static bool LevelCleared { get; private set; }
    public static float LevelClearSeconds { get; private set; }

    static bool counting, attemptOpen;

    public static string BossName(int wave) => wave switch { 1 => "Gunner", 2 => "Swordsman", 3 => "Duo", _ => "-" };

    public static void ResetRun()
    {
        Array.Clear(DeathsByWave, 0, DeathsByWave.Length);
        History.Clear();
        Jumps = Dodges = Shots = 0;
        LastSeconds = RunSeconds = LevelClearSeconds = 0;
        LevelCleared = false;
        counting = attemptOpen = false;
    }

    public static void BeginAttempt(int wave)
    {
        Jumps = Dodges = Shots = 0;
        LastSeconds = 0;
        counting = false;
        attemptOpen = true;
    }

    public static void StartCounting() { if (attemptOpen) counting = true; }

    public static void CountJump()  { if (counting) Jumps++; }
    public static void CountDodge() { if (counting) Dodges++; }

    public static void EndAttempt(int wave, bool died, float seconds, int shots, bool levelCleared)
    {
        if (!attemptOpen) return;                    // ignore duplicate or abandoned attempts
        attemptOpen = false; counting = false;

        Shots = shots; LastSeconds = seconds; RunSeconds += seconds;
        if (died && wave >= 1 && wave < DeathsByWave.Length) DeathsByWave[wave]++;
        History.Add(new Attempt { Wave = wave, Died = died, Seconds = seconds, Jumps = Jumps, Dodges = Dodges, Shots = shots });
        Log("fight", wave, died ? "died" : "won", seconds, Jumps, Dodges, shots);

        if (levelCleared)
        {
            LevelCleared = true; LevelClearSeconds = RunSeconds;
            int j = 0, d = 0, s = 0;
            foreach (var a in History) { j += a.Jumps; d += a.Dodges; s += a.Shots; }
            Log("level_clear", wave, "clear", RunSeconds, j, d, s);
        }
    }

    static void Log(string kind, int wave, string result, float seconds, int jumps, int dodges, int shots)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "metrics.csv");
            if (!File.Exists(path))
                File.AppendAllText(path, "time_utc,kind,wave,boss,result,seconds,jumps,dodges,shots\n");
            File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture,
                "{0:o},{1},{2},{3},{4},{5:0.00},{6},{7},{8}\n",
                DateTime.UtcNow, kind, wave, BossName(wave), result, seconds, jumps, dodges, shots));
        }
        catch (Exception e) { Debug.LogWarning("Metrics log failed: " + e.Message); }
    }
}