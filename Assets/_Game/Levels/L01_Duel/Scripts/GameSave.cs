using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GoGhost.Duel
{
    // One automatic slot: the drawn face, name, equipped rules, mission reached and what Happy has already explained.
    [Serializable] public sealed class SaveData
    {
        public int version = 1;
        public string name = "";
        public int wave = 1;
        public bool[] profiles;
        public bool tutorialDone;
        // The analytics record this run belongs to; Continue extends it.
        public string runId;
        public OperationRules[] operations;
    }

    public static class GameSave
    {
        static string Folder => Path.Combine(Application.persistentDataPath, "Save");
        static string DataPath => Path.Combine(Folder, "save.json");
        static string FacePath => Path.Combine(Folder, "face.png");
        public static bool Exists => File.Exists(DataPath);

        public static void Write(SaveData data, byte[] face)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                if (face != null) File.WriteAllBytes(FacePath, face);
                string temp = DataPath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
                if (File.Exists(DataPath)) File.Delete(DataPath);
                File.Move(temp, DataPath);
            }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        public static SaveData Read(out byte[] face)
        {
            face = null;
            try
            {
                if (!Exists) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(DataPath));
                if (data != null && File.Exists(FacePath)) face = File.ReadAllBytes(FacePath);
                return data;
            }
            catch (Exception e) { Debug.LogWarning("Save could not be read: " + e.Message); return null; }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(DataPath)) File.Delete(DataPath);
                if (File.Exists(FacePath)) File.Delete(FacePath);
            }
            catch (Exception e) { Debug.LogWarning("Save could not be removed: " + e.Message); }
        }
    }

    public sealed partial class DuelGame
    {
        bool savingRun, faceSaved, greeted, tutorialDone;
        string savedRunId;

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
                tutorialDone = tutorialDone, runId = savedRunId, operations = DraftLoadout.Copy().Operations
            };
            GameSave.Write(data, faceSaved ? null : Appearance.ExportPng());
            faceSaved = true;
        }

        // Continue opens the preparation page of the mission reached. A fight in progress is never stored.
        public void ContinueGame()
        {
            if (DeveloperToolsOpen) return;
            var data = GameSave.Read(out var face);
            if (data == null) { ui.Show(); return; }
            ClearEncounter("continue");
            PlayMetrics.ResumeRun(data.runId);
            savedRunId = data.runId;
            metricsWaveJumpPending = false;
            Wave = Mathf.Clamp(data.wave, 1, FinalBossWave);
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
            DraftLoadout = ConditionalCatalog.IsValid(loadout) ? loadout : new ConditionalLoadout();
            ResetCharacterAppearance();
            Appearance.Name = data.name ?? "";
            if (face != null) Appearance.ImportPng(face);
            savingRun = faceSaved = true;
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
