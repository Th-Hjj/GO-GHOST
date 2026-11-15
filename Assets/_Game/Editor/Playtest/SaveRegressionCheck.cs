#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

// Focused persistence regressions. Uses only public runtime APIs and unique,
// isolated folders; never starts Play Mode or writes the player's real slot.
public static class SaveRegressionCheck
{
    [MenuItem("GO GHOST/Playtest/Check Save Recovery")]
    public static void RunInEditor() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run save checks in Edit Mode after compilation.");
        string root = Path.GetFullPath(Path.Combine("Logs", "SaveRegression", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
        Directory.CreateDirectory(root);
        var notes = new List<string>();
        int passed = 0, failed = 0;
        bool hadCleared = PlayerPrefs.HasKey("progress.cleared");
        int originalCleared = PlayerPrefs.GetInt("progress.cleared", 0);
        byte[] png = MakePng();

        void ProtectedPreference()
        {
            Require(PlayerPrefs.HasKey("progress.cleared") == hadCleared
                && PlayerPrefs.GetInt("progress.cleared", 0) == originalCleared,
                "A regression check modified the real cleared preference.");
        }
        void Check(string name, Action<string> test)
        {
            string folder = Path.Combine(root, name);
            Directory.CreateDirectory(folder);
            try
            {
                using (GameSave.BeginTestStorage(folder)) test(folder);
                ProtectedPreference();
                Require(GameProgress.Cleared == (originalCleared != 0), "Test scope did not restore live Cleared reads.");
                passed++; notes.Add("PASS " + name);
            }
            catch (Exception e) { failed++; notes.Add("FAIL " + name + ": " + e); }
        }

        Check("legacy-rules", folder =>
        {
            var old = Snapshot("legacy", 2);
            old.version = 1;
            var rows = old.operations[(int)ControlOperation.Jump].Rules;
            for (int i = 0; i < 4; i++)
            {
                var rule = ConditionalLoadout.CreateTutorialJumpRule();
                rule.DisplayOrder = i; rows.Add(rule);
            }
            rows.Add(new ConditionalRule { Condition = ConditionIds.GroundState, GroundState = GroundStateOption.Grounded,
                Action = ActionIds.Jump, DisplayOrder = 4 });
            File.WriteAllText(Main(folder), JsonUtility.ToJson(old, true));
            File.WriteAllBytes(Path.Combine(folder, "face.png"), png);
            byte[] original = File.ReadAllBytes(Main(folder));
            var read = GameSave.Read(out var face);
            Require(read != null && read.version == GameSave.CurrentVersion, "v1 did not migrate in memory.");
            Require(Same(face, png) && Same(File.ReadAllBytes(Main(folder)), original), "Reading mutated the v1 file or lost its drawing.");
            var loadout = Loadout(read);
            Require(loadout.TotalRules == 5, "An invalid old rule erased or dropped rules.");
            var loadedRows = loadout.OrderedRules();
            for (int i = 0; i < 4; i++)
                Require(loadedRows[i].DisplayOrder == i && ConditionalCatalog.IsAllowed((int)ControlOperation.Jump, loadedRows[i]), "A valid old rule changed.");
            Require(loadedRows[4].GroundState == GroundStateOption.Grounded && !ConditionalCatalog.IsValid(loadout)
                && ConditionalCatalog.ConditionLabel(loadedRows[4]).Contains("Reselect"), "Grounded was silently changed or allowed into combat.");
            Require(GameSave.Write(read, null), "Could not commit migrated rules.");
            read = GameSave.Read(out face);
            Require(read != null && Loadout(read).TotalRules == 5 && Same(face, png), "Migration roundtrip lost data.");
            loadout = Loadout(read); loadout.OrderedRules()[4].GroundState = GroundStateOption.OnGround;
            Require(ConditionalCatalog.IsValid(loadout), "Repairing only Grounded did not restore a valid loadout.");
        });

        Check("v2-and-backup", folder =>
        {
            TwoGenerations(png);
            var read = GameSave.Read(out var face);
            Require(read != null && read.name == "second" && read.wave == 2 && Same(face, png), "Latest v2 data/face did not roundtrip.");
            var backup = JsonUtility.FromJson<SaveData>(File.ReadAllText(Backup(folder)));
            Require(backup.version == GameSave.CurrentVersion && backup.name == "first"
                && Same(Convert.FromBase64String(backup.facePngBase64), png), "Backup is not a complete earlier snapshot.");
            Require(!File.Exists(Path.Combine(folder, "face.png")), "v2 unexpectedly depends on an external face file.");
        });

        Check("recover-then-save", folder =>
        {
            TwoGenerations(png);
            byte[] before = File.ReadAllBytes(Backup(folder));
            File.WriteAllText(Main(folder), "{truncated");
            var read = GameSave.Read(out var face);
            Require(read != null && read.name == "first" && Same(face, png)
                && GameSave.LastMessage.Contains("Restored"), "Could not recover a previous complete save.");
            Require(File.ReadAllText(Main(folder)) == "{truncated", "Read changed files.");
            read.name = "recovered";
            Require(GameSave.Write(read, null), "Saving after backup recovery failed.");
            Require(Same(File.ReadAllBytes(Backup(folder)), before), "Corrupt primary replaced the only good backup.");
            Require(GameSave.Read(out _).name == "recovered", "New primary did not commit after recovery.");
        });

        Check("both-corrupt", folder =>
        {
            File.WriteAllText(Main(folder), "{broken main");
            File.WriteAllText(Backup(folder), "{broken backup");
            var main = File.ReadAllBytes(Main(folder)); var backup = File.ReadAllBytes(Backup(folder));
            Require(GameSave.Read(out _) == null && !string.IsNullOrEmpty(GameSave.LastMessage), "Corruption did not produce a readable failure.");
            Require(Same(main, File.ReadAllBytes(Main(folder))) && Same(backup, File.ReadAllBytes(Backup(folder)))
                && !File.Exists(Temp(folder)), "Failed read rewrote damaged files.");
        });

        Check("missing-rules-recover-backup", folder =>
        {
            var saved = Snapshot("with-rules", 2);
            saved.operations[(int)ControlOperation.Jump].Rules.Add(ConditionalLoadout.CreateTutorialJumpRule());
            Require(GameSave.Write(saved, png) && GameSave.Write(saved, null), "Could not prepare rule recovery.");
            // JsonUtility serializes null inline objects/lists as empty values;
            // write actual missing/null JSON to exercise damaged files.
            string json = JsonUtility.ToJson(JsonUtility.FromJson<SaveData>(File.ReadAllText(Backup(folder))));
            int start = json.IndexOf("\"operations\":", StringComparison.Ordinal);
            int end = json.IndexOf(",\"facePngBase64\"", start, StringComparison.Ordinal);
            foreach (string operations in new[] { "null", "[]", "[null,{\"Rules\":[]},{\"Rules\":[]},{\"Rules\":[]},{\"Rules\":[]}]",
                "[{},{\"Rules\":[]},{\"Rules\":[]},{\"Rules\":[]},{\"Rules\":[]}]",
                "[{\"Rules\":null},{\"Rules\":[]},{\"Rules\":[]},{\"Rules\":[]},{\"Rules\":[]}]" })
            {
                File.WriteAllText(Main(folder), json.Substring(0, start) + "\"operations\":" + operations + json.Substring(end));
                byte[] before = File.ReadAllBytes(Main(folder));
                var recovered = GameSave.Read(out _);
                Require(recovered != null && Loadout(recovered).TotalRules == 1
                    && (GameSave.LastMessage ?? "").Contains("Restored"), "Missing rule structure was accepted: " + operations);
                Require(Same(before, File.ReadAllBytes(Main(folder))), "Recovery changed the broken primary.");
            }
        });

        Check("future-primary", folder =>
        {
            TwoGenerations(png);
            var future = JsonUtility.FromJson<SaveData>(File.ReadAllText(Main(folder)));
            future.version = GameSave.CurrentVersion + 1;
            File.WriteAllText(Main(folder), JsonUtility.ToJson(future, true));
            var main = File.ReadAllBytes(Main(folder)); var backup = File.ReadAllBytes(Backup(folder));
            Require(GameSave.Read(out _) == null && GameSave.LastMessage.Contains("newer"), "Future main fell back to an older save.");
            Require(!GameSave.Write(Snapshot("downgrade", 1), png), "Current writer overwrote a future main.");
            Require(!GameSave.Write(future, png), "Writer accepted a future input schema.");
            Require(Same(main, File.ReadAllBytes(Main(folder))) && Same(backup, File.ReadAllBytes(Backup(folder))), "Future-version refusal changed committed files.");
        });

        Check("future-backup", folder =>
        {
            var future = Snapshot("future", 3); future.version = GameSave.CurrentVersion + 1;
            future.facePngBase64 = Convert.ToBase64String(png);
            File.WriteAllText(Backup(folder), JsonUtility.ToJson(future, true));
            var backup = File.ReadAllBytes(Backup(folder));
            Require(GameSave.Read(out _) == null && GameSave.LastMessage.Contains("newer"), "Future backup was accepted.");
            Require(!GameSave.Write(Snapshot("downgrade", 1), png), "Current writer bypassed a future recovery slot.");
            Require(!File.Exists(Main(folder)) && Same(backup, File.ReadAllBytes(Backup(folder))), "Future backup protection changed the slot.");
        });

        Check("failed-staging", folder =>
        {
            TwoGenerations(png);
            var main = File.ReadAllBytes(Main(folder)); var backup = File.ReadAllBytes(Backup(folder));
            Directory.CreateDirectory(Temp(folder));
            Require(!GameSave.Write(Snapshot("blocked", 3), png) && !string.IsNullOrEmpty(GameSave.LastMessage), "Staging failure was reported as success.");
            Require(Same(main, File.ReadAllBytes(Main(folder))) && Same(backup, File.ReadAllBytes(Backup(folder))), "Failed staging damaged a committed generation.");
            Directory.Delete(Temp(folder)); // Exact, empty test-only directory; no recursion.
            Require(GameSave.Write(Snapshot("retry", 3), png) && GameSave.Read(out _).name == "retry", "Storage could not recover after a transient failure.");
        });

        Check("uncommitted-temp", folder =>
        {
            var staged = Snapshot("never-committed", 1); staged.facePngBase64 = Convert.ToBase64String(png);
            File.WriteAllText(Temp(folder), JsonUtility.ToJson(staged, true));
            byte[] before = File.ReadAllBytes(Temp(folder));
            Require(!GameSave.Exists && GameSave.Read(out _) == null, "An unfinished temp was treated as a committed run.");
            Require(Same(before, File.ReadAllBytes(Temp(folder))), "Read mutated an unfinished temp.");
        });

        Check("cleared-isolation", folder =>
        {
            Require(!GameProgress.Cleared, "Isolated progress did not start clear.");
            GameProgress.Cleared = true; Require(GameProgress.Cleared, "Isolated flag did not store true.");
            ProtectedPreference();
            GameProgress.Cleared = false; Require(!GameProgress.Cleared, "Isolated flag did not store false.");
            ProtectedPreference();
        });

        Check("delete-no-resurrection", folder =>
        {
            TwoGenerations(png); File.WriteAllText(Temp(folder), "unfinished");
            Require(GameSave.Delete() && !GameSave.Exists && GameSave.Read(out _) == null, "Deleting a slot left a recoverable old run.");
        });

        string report = $"Save regression: {(failed == 0 ? "PASS" : "FAIL")} ({passed} passed, {failed} failed)\n"
            + string.Join("\n", notes) + "\nFixtures: " + root;
        File.WriteAllText(Path.Combine(root, "report.txt"), report, new UTF8Encoding(false));
        if (failed != 0) throw new InvalidOperationException(report);
        return report;
    }

    static SaveData Snapshot(string name, int wave) => new SaveData
    {
        name = name, wave = wave, tutorialDone = true, profiles = new bool[4],
        runId = "save-regression-fixture", operations = new ConditionalLoadout().Operations
    };
    static ConditionalLoadout Loadout(SaveData data)
    {
        var result = new ConditionalLoadout();
        for (int i = 0; i < result.Operations.Length; i++)
            foreach (var rule in data.operations[i].Rules) result.Operations[i].Rules.Add(rule.Copy());
        return result;
    }
    static void TwoGenerations(byte[] png)
    {
        Require(GameSave.Write(Snapshot("first", 1), png), "First save failed.");
        Require(GameSave.Write(Snapshot("second", 2), null), "Second save failed.");
    }
    static byte[] MakePng()
    {
        var texture = new Texture2D(CharacterAppearance.Resolution, CharacterAppearance.Resolution, TextureFormat.RGBA32, false);
        try
        {
            var pixels = new Color32[CharacterAppearance.Resolution * CharacterAppearance.Resolution];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i % 256), 128, 255, 255);
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture.EncodeToPNG();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    static string Main(string folder) => Path.Combine(folder, "save.json");
    static string Backup(string folder) => Path.Combine(folder, "save.json.bak");
    static string Temp(string folder) => Path.Combine(folder, "save.json.tmp");
    static bool Same(byte[] left, byte[] right) => left != null && right != null && left.SequenceEqual(right);
    static void Require(bool condition, string issue) { if (!condition) throw new InvalidOperationException(issue); }
}
#endif
