using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GoGhost.Duel
{
    // Zero is the default for saves written before difficulty selection.
    public enum GameDifficulty { Normal, Easy, Hard }

    [Serializable] public sealed class SaveData
    {
        public int version = GameSave.CurrentVersion;
        public string name = "";
        public int wave = 1;
        public GameDifficulty difficulty;
        public bool[] profiles;
        public bool tutorialDone;
        public string runId;
        public OperationRules[] operations;
        // Since v2 the complete drawing and progress commit together in one file.
        public string facePngBase64;
    }

    public static class GameSave
    {
        public const int CurrentVersion = 2;
        const string DataName = "save.json", BackupName = "save.json.bak", TempName = "save.json.tmp";
        static readonly UTF8Encoding Encoding = new UTF8Encoding(false, true);
        static string Folder
        {
            get
            {
#if UNITY_EDITOR
                if (testFolder != null) return testFolder;
#endif
                return Path.Combine(Application.persistentDataPath, "Save");
            }
        }
        public static bool Exists => File.Exists(Path.Combine(Folder, DataName)) || File.Exists(Path.Combine(Folder, BackupName));
        public static string LastMessage { get; private set; }

        // A bool result remains source compatible with statement-style callers.
        public static bool Write(SaveData data, byte[] face)
        {
            bool saved = WriteTo(Folder, data, face, out string issue);
            LastMessage = saved ? null : "Could not save. Please try again.";
            if (!saved) Debug.LogWarning("Save failed: " + issue);
            return saved;
        }
        public static SaveData Read(out byte[] face)
        {
            var data = ReadFrom(Folder, out face, out _, out string notice);
            LastMessage = notice;
            return data;
        }
        public static bool Delete()
        {
            try
            {
                // Remove recoverable older generations before deleting the current
                // slot, so a completed deletion cannot resurrect an old run.
                foreach (string name in new[] { BackupName, TempName, DataName })
                {
                    string path = Path.Combine(Folder, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                LastMessage = null;
            }
            catch (Exception e)
            {
                LastMessage = "Your saved run could not be removed.";
                Debug.LogWarning("Save could not be removed: " + e.Message);
                return false;
            }
            // A v1 image is obsolete after the slot is removed. Failure to remove
            // this unused image does not mean the playable slot still exists.
            try { string image = Path.Combine(Folder, "face.png"); if (File.Exists(image)) File.Delete(image); }
            catch (Exception e) { Debug.LogWarning("Old saved drawing could not be removed: " + e.Message); }
            return true;
        }

        // Explicit folders let regression checks use isolated temporary storage.
        internal static bool WriteTo(string folder, SaveData input, byte[] face, out string issue)
        {
            issue = null;
            try
            {
                if (input == null) throw new InvalidDataException("No save data.");
                if (input.version < 1 || input.version > CurrentVersion) throw new InvalidDataException("Unsupported save version.");
                var data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(input));
                ValidateStructure(data);
                if (face == null)
                {
                    if (ReadFrom(folder, out face, out _, out _) == null || face == null)
                        throw new InvalidDataException("No saved drawing is available; retry with the current drawing.");
                }
                if (!ValidPng(face)) throw new InvalidDataException("The saved drawing is not a complete 512-pixel PNG.");
                data.version = CurrentVersion;
                data.facePngBase64 = Convert.ToBase64String(face);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, DataName), backup = Path.Combine(folder, BackupName), temp = Path.Combine(folder, TempName);
                bool validMain = ReadRecord(path, folder, out _, out bool newerMain, out _) != null;
                ReadRecord(backup, folder, out _, out bool newerBackup, out _);
                if (newerMain || newerBackup) throw new InvalidDataException("A newer save must not be overwritten by this version.");
                WriteDurably(temp, Encoding.GetBytes(JsonUtility.ToJson(data, true)));
                // Verify the bytes we actually wrote before touching the old slot.
                if (ReadRecord(temp, folder, out _, out _, out _) == null)
                    throw new InvalidDataException("The staged save could not be verified.");

                if (File.Exists(path) && validMain)
                {
                    try { File.Replace(temp, path, backup); }
                    catch (PlatformNotSupportedException) { ReplaceWithBackup(temp, path, backup); }
                    catch (NotSupportedException) { ReplaceWithBackup(temp, path, backup); }
                }
                else
                {
                    // A valid backup may be the only recovery source. Do not rotate
                    // a damaged primary into it after recovering that backup.
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(temp, path);
                }
                return true;
            }
            catch (Exception e) { issue = e.Message; return false; }
        }

        static void ReplaceWithBackup(string temp, string path, string backup)
        {
            // For filesystems without Replace: establish and flush the recovery
            // copy while the old primary is still intact, then commit the temp.
            WriteDurably(backup, File.ReadAllBytes(path));
            File.Delete(path);
            File.Move(temp, path);
        }

        static void WriteDurably(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browser persistence still depends on Unity's filesystem sync.
                stream.Flush();
#else
                stream.Flush(true);
#endif
            }
        }

        internal static SaveData ReadFrom(string folder, out byte[] face, out bool recovered, out string notice)
        {
            recovered = false;
            var data = ReadRecord(Path.Combine(folder, DataName), folder, out face, out bool newer, out notice);
            if (data != null || newer) return data;
            // An unfinished .tmp is never treated as a committed save.
            data = ReadRecord(Path.Combine(folder, BackupName), folder, out face, out bool backupNewer, out string backupNotice);
            if (data != null)
            {
                recovered = true;
                notice = "Restored your previous save.";
                if (face == null) notice += " Your drawing could not be restored.";
                return data;
            }
            if (backupNewer) notice = backupNotice;
            if (notice == null && (File.Exists(Path.Combine(folder, DataName)) || File.Exists(Path.Combine(folder, BackupName))))
                notice = "Your save could not be loaded.";
            return null;
        }

        static SaveData ReadRecord(string path, string folder, out byte[] face, out bool newer, out string notice)
        {
            face = null; newer = false; notice = null;
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path, Encoding);
                // JsonUtility turns explicit null collections into empty ones.
                // Validate the stored structure before materializing gameplay data.
                var stored = JObject.Parse(json);
                if (stored["version"]?.Type != JTokenType.Integer) throw new InvalidDataException("Missing save version.");
                int version = (int)stored["version"];
                if (version < 1) throw new InvalidDataException("Missing save version.");
                if (version > CurrentVersion)
                {
                    newer = true;
                    notice = "This save needs a newer version of GO GHOST.";
                    return null;
                }
                if (!(stored["operations"] is JArray operations) || operations.Count != 5)
                    throw new InvalidDataException("Missing saved operations.");
                foreach (var operation in operations)
                    if (!(operation is JObject row) || !(row["Rules"] is JArray))
                        throw new InvalidDataException("Missing saved rules.");
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) throw new InvalidDataException("Missing save data.");
                data.version = version;
                ValidateStructure(data);
                if (data.version == 1)
                {
                    // Migration is memory-only. Keep the v1 JSON and image intact
                    // until a later, verified v2 write succeeds.
                    string image = Path.Combine(folder, "face.png");
                    try { if (File.Exists(image)) face = File.ReadAllBytes(image); }
                    catch (IOException) { face = null; }
                    catch (UnauthorizedAccessException) { face = null; }
                    if (!ValidPng(face)) { face = null; notice = "Your drawing could not be restored."; }
                    data.facePngBase64 = face != null ? Convert.ToBase64String(face) : null;
                    data.version = CurrentVersion;
                }
                else
                {
                    face = Convert.FromBase64String(data.facePngBase64 ?? "");
                    if (!ValidPng(face)) throw new InvalidDataException("Invalid saved drawing.");
                }
                return data;
            }
            catch (Exception)
            {
                face = null;
                notice = "Your save could not be loaded.";
                return null;
            }
        }

        static void ValidateStructure(SaveData data)
        {
            if (!Enum.IsDefined(typeof(GameDifficulty), data.difficulty)) data.difficulty = GameDifficulty.Normal;
            if (data.wave < 1 || data.wave > DuelGame.FinalBossWave) throw new InvalidDataException("Invalid mission.");
            data.name = data.name ?? "";
            if (!string.IsNullOrEmpty(data.runId))
                foreach (char c in data.runId)
                    if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_'))
                        throw new InvalidDataException("Invalid run identifier.");
            var normalized = new ConditionalLoadout();
            if (data.operations == null || data.operations.Length != normalized.Operations.Length)
                throw new InvalidDataException("Missing saved operations.");
            for (int op = 0; op < data.operations.Length; op++)
            {
                var rules = data.operations[op]?.Rules;
                if (rules == null) throw new InvalidDataException("Missing saved rules.");
                foreach (var rule in rules)
                {
                    if (rule == null) throw new InvalidDataException("Missing rule.");
                    var copy = rule.Copy();
                    // Known historical IDs and obsolete parameters remain editable.
                    // Unknown IDs are corruption/incompatibility, not empty rules.
                    ConditionalCatalog.Response(copy.Action);
                    for (int i = 0; i < copy.ConditionCount; i++) ConditionalCatalog.Condition(copy.GetCondition(i).Id);
                    normalized.Operations[op].Rules.Add(copy);
                }
            }
            if (normalized.TotalRules > ConditionalCatalog.MaxRulesTotal) throw new InvalidDataException("Too many saved rules.");
            data.operations = normalized.Operations;
        }

        // Check PNG framing, dimensions and CRCs without creating Unity textures.
        // Image decompression remains owned by CharacterAppearance.ImportPng.
        static bool ValidPng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 45) return false;
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < 8; i++) if (bytes[i] != signature[i]) return false;
            bool pixels = false;
            int at = 8;
            while (at <= bytes.Length - 12)
            {
                uint size = U32(bytes, at);
                if (size > (uint)(bytes.Length - at - 12)) return false;
                int length = (int)size, type = at + 4, next = at + 12 + length;
                uint crc = 0xffffffff;
                for (int i = type; i < type + 4 + length; i++)
                {
                    crc ^= bytes[i];
                    for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
                }
                if ((crc ^ 0xffffffff) != U32(bytes, type + 4 + length)) return false;
                uint chunk = U32(bytes, type);
                if (at == 8 && (chunk != 0x49484452 || length != 13
                    || U32(bytes, type + 4) != CharacterAppearance.Resolution || U32(bytes, type + 8) != CharacterAppearance.Resolution)) return false;
                if (chunk == 0x49444154 && length > 0) pixels = true;
                if (chunk == 0x49454e44) return length == 0 && pixels && next == bytes.Length;
                at = next;
            }
            return false;
        }
        static uint U32(byte[] bytes, int at) => (uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3];

#if UNITY_EDITOR
        static string testFolder;
        internal static bool TestStorageActive => testFolder != null;
        internal static string TestAnalyticsFolder => Path.Combine(testFolder, "Analytics");
        internal static bool TestCleared;
        // Deliberately not reset by SubsystemRegistration: a scope can encompass
        // EnterPlayMode and scene teardown when Domain Reload is disabled.
        public static IDisposable BeginTestStorage(string folder)
        {
            if (testFolder != null) throw new InvalidOperationException("A test storage scope is already active.");
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder)) throw new ArgumentException("Use an absolute isolated directory.");
            string selected = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string live = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Save")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(selected, live, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Tests cannot use player storage.");
            testFolder = selected; TestCleared = false;
            return new TestScope();
        }
        sealed class TestScope : IDisposable
        {
            bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true; testFolder = null; TestCleared = false; LastMessage = null;
            }
        }
#endif
    }

    // What outlives the save slot: finishing the game removes the save, and New Game must not undo this.
    public static class GameProgress
    {
        const string ClearedKey = "progress.cleared";
        // True once the Final Boss has been beaten on this machine. It brings GHOST to the title.
        public static bool Cleared
        {
            get
            {
#if UNITY_EDITOR
                if (GameSave.TestStorageActive) return GameSave.TestCleared;
#endif
                return PlayerPrefs.GetInt(ClearedKey, 0) != 0;
            }
            set
            {
#if UNITY_EDITOR
                if (GameSave.TestStorageActive) { GameSave.TestCleared = value; return; }
#endif
                PlayerPrefs.SetInt(ClearedKey, value ? 1 : 0); PlayerPrefs.Save();
            }
        }
    }

    public sealed partial class DuelGame
    {
        bool savingRun, faceSaved, greeted, tutorialDone;
        string savedRunId;
        bool saveFailed, savedRulesNeedRepair;
        public string SaveNotice { get; private set; }

        public GameDifficulty Difficulty { get; private set; }
        public bool CanChangeDifficulty => !GhostArena && (Phase == DuelPhase.CharacterCreation ||
            Phase == DuelPhase.Configure || Phase == DuelPhase.TargetProfile || Phase == DuelPhase.Failed ||
            Phase == DuelPhase.Between || (Phase == DuelPhase.Complete && Wave == BonusWave));

        internal GameDifficulty? ReadTitleDifficulty()
        {
            if (!GameSave.Exists) return null;
            var data = GameSave.Read(out _);
            if (GameSave.LastMessage != null) SaveNotice = GameSave.LastMessage;
            return data?.difficulty;
        }

        internal bool SetDifficulty(GameDifficulty value)
        {
            if (!Enum.IsDefined(typeof(GameDifficulty), value)) return false;
            if (Phase == DuelPhase.Menu)
            {
                // Patch only the selected slot. An abandoned creation or an Editor run may still be in memory.
                var slot = GameSave.Read(out var drawing);
                if (slot != null)
                {
                    slot.difficulty = value;
                    if (GameSave.Write(slot, drawing)) { SaveNotice = null; return true; }
                }
                SaveNotice = GameSave.LastMessage;
                return false;
            }
            if (!CanChangeDifficulty) return false;
            Difficulty = value;
            if (Phase != DuelPhase.CharacterCreation) SaveGame();
            return true;
        }

        // Written after Upload, on every rule change, when a mission is cleared and on leaving for the menu.
        void SaveGame()
        {
            if (!savingRun || Appearance == null) return;
            if (greeted && !handler.TutorialRunning) tutorialDone = true;
            if (PlayMetrics.Run != null) savedRunId = PlayMetrics.Run.runId;
            int reached = Phase == DuelPhase.Between ? Wave + 1 : Wave;
            var data = new SaveData
            {
                name = Appearance.Name, wave = Mathf.Clamp(reached, 1, FinalBossWave), profiles = (bool[])profileOpened.Clone(),
                tutorialDone = tutorialDone, runId = savedRunId, operations = DraftLoadout.Copy().Operations, difficulty = Difficulty
            };
            if (GameSave.Write(data, faceSaved ? null : Appearance.ExportPng()))
            {
                faceSaved = true;
                if (saveFailed || (savedRulesNeedRepair && ConditionalCatalog.IsValid(DraftLoadout))) SaveNotice = null;
                saveFailed = false;
                savedRulesNeedRepair = !ConditionalCatalog.IsValid(DraftLoadout);
                if (savedRulesNeedRepair && string.IsNullOrEmpty(SaveNotice)) SaveNotice = "Some rules need to be reselected.";
            }
            else
            {
                faceSaved = false;
                saveFailed = true;
                SaveNotice = GameSave.LastMessage;
            }
        }

        // Continue opens the preparation page of the mission reached. A fight in progress is never stored.
        public void ContinueGame()
        {
            if (DeveloperToolsOpen) return;
            var data = GameSave.Read(out var face);
            SaveNotice = GameSave.LastMessage;
            saveFailed = false;
            if (data == null) { ui.Show(); return; }
            ClearEncounter("continue");
            PlayMetrics.ResumeRun(data.runId);
            savedRunId = data.runId;
            metricsWaveJumpPending = false;
            Wave = Mathf.Clamp(data.wave, 1, FinalBossWave);
            Difficulty = data.difficulty;
            System.Array.Clear(profileOpened, 0, profileOpened.Length);
            if (data.profiles != null)
                for (int i = 0; i < profileOpened.Length && i < data.profiles.Length; i++) profileOpened[i] = data.profiles[i];
            DraftLegs = DraftArms = DraftSpine = false;
            var loadout = new ConditionalLoadout();
            if (data.operations != null)
                for (int i = 0; i < loadout.Operations.Length && i < data.operations.Length; i++)
                    if (data.operations[i]?.Rules != null)
                        foreach (var rule in data.operations[i].Rules)
                            if (rule != null) loadout.Operations[i].Rules.Add(rule.Copy());
            DraftLoadout = loadout;
            savedRulesNeedRepair = !ConditionalCatalog.IsValid(loadout);
            if (savedRulesNeedRepair)
                SaveNotice = (string.IsNullOrEmpty(SaveNotice) ? "" : SaveNotice + " ") + "Some rules need to be reselected.";
            ResetCharacterAppearance();
            Appearance.Name = data.name ?? "";
            if (face != null) Appearance.ImportPng(face);
            savingRun = true;
            faceSaved = face != null;
            tutorialDone = data.tutorialDone;
            greeted = false;
            FirstConfiguration = !tutorialDone;
            if (tutorialDone) handler.RestoreSpeakerName(); else handler.ResetSpeakerName();
            PlayMetrics.SetName(Appearance.DisplayName, true);
            SetPhase(DuelPhase.Configure);
            if (tutorialDone) return;
            handler.PlayGreeting(Appearance.Name, DraftLoadout.TotalRules == 0 ? ui.InstallTutorialJumpRule : (System.Action)null);
            greeted = true;
        }
    }
}
