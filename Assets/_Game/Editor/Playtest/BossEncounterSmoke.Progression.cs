#if UNITY_EDITOR
using System.Collections;
using System.IO;
using GoGhost.Duel;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    // One optional route through the normal game flow, using controlled victories to avoid a balance dependency.
    static IEnumerator GameFlow()
    {
        game.NewGame();
        PlayMetrics.MarkTestData();
        game.CompleteCharacterCreation();
        var character = game.Appearance;
        var rules = JsonUtility.ToJson(game.DraftLoadout);

        string path = PlayMetrics.JsonPath;
        string closed = null;
        for (int wave = 1; wave <= DuelGame.FinalBossWave; wave++)
        {
            Require(game.Wave == wave && game.TrainingPending, "Missing configuration for wave " + wave);
            yield return WaitFor(() => !game.DialogueSpeaking, "Preparation voice did not finish");
            yield return null;
            Click("TargetProfile");
            Require(game.Phase == DuelPhase.TargetProfile && game.PendingDialogue == "Target Profile for Mission " + wave, "Missing target dossier title");
            yield return WaitFor(() => game.DialogueReady && !game.DialogueSpeaking, "Dossier title did not finish");
            game.AcknowledgeDialogue();
            Require(game.PendingDialogue.StartsWith("Codename,"), "Codename did not follow the dossier title");
            yield return WaitFor(() => !game.DialogueSpeaking, "Codename recording did not finish");
            yield return null;
            Click("ProfileBack");
            Click("TargetProfile");
            Require(game.Phase == DuelPhase.TargetProfile && game.PendingDialogue == null, "Profile replayed on a return visit");
            string lastSkill = wave == 1 ? "Piercing Shot · Optical Camo" : wave == 2 ? "Pursuit Draw · Still Blade" : "[placeholder]";
            Require(System.Array.Exists(game.GetComponentsInChildren<TMPro.TMP_Text>(),text => text.text == lastSkill),
                "Returning to an interrupted dossier did not reveal its final field");
            Require(System.Array.TrueForAll(game.GetComponents<HandlerVoice>(), voice => !voice.Speaking),
                "Voice still playing on a return visit");
            Click("ProfileBack");
            Click("TestLoadout");
            Require(game.Phase == DuelPhase.Training && game.Bosses[0].IsTrainingEnemy, "Missing test room");
            Click("← Back");
            Require(game.Phase == DuelPhase.Configure, "Test Loadout did not return to preparation");
            Click("StartMission");
            yield return WaitFor(() => game.Phase != DuelPhase.MissionEntry, "Mission entrance did not finish");
            Require(game.Phase == DuelPhase.Prepare && game.PendingDialogue == null, "Missing direct pre-fight countdown");
            yield return WaitFor(() => game.Phase == DuelPhase.Fight, "Combat did not start");
            game.Player.IsInvulnerable = true;
            game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
            if (wave == 1)
            {
                yield return WaitFor(() => game.Phase == DuelPhase.Between && game.PendingDialogue == null, "Mission 1 result", 15);
                game.ContinueToWaveTwo();
            }
            if (wave == 2)
            {
                yield return WaitFor(() => game.Phase == DuelPhase.Configure, "Wave 2 result");
                Require(!PlayMetrics.RunActive && PlayMetrics.Run.levelCleared, "Wave 2 metrics cutoff");
                closed = File.ReadAllText(path);
            }
        }

        foreach (var line in new[] { "You're strong.", "But it's not over yet.", "Let’s see what you can do on your own." })
            yield return WaitFor(() => game.CutsceneText == line && game.PendingDialogue == line, "Missing kneeling dialogue: " + line);
        yield return WaitFor(() => game.CutsceneSpeakerId == DuelGame.CutsceneSpeaker.Happy, "Missing interrupted EMP warning");
        yield return WaitFor(() => game.Bosses[0].Axe.PhaseTwo, "Phase two did not start", 30);
        game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.BonusOffer, "Missing Bonus offer");
        yield return WaitFor(() => game.FinalBossChoiceReady, "Missing Bonus choice", 15);
        game.ChooseBonus(true);
        Require(game.TrainingPending && game.Wave == DuelGame.BonusWave && game.Appearance == character
            && JsonUtility.ToJson(game.DraftLoadout) == rules, "Bonus lost identity or rules");
        Click("StartMission");
        yield return WaitFor(() => game.Phase == DuelPhase.Prepare, "Bonus entrance did not finish");
        Require(game.Phase == DuelPhase.Prepare && game.Bosses.Count == 2, "Missing Bonus duo");
        Require(File.ReadAllText(path) == closed, "Wave 3 or Bonus changed the closed Wave 2 record");
        game.MainMenu();
    }

    static void Click(string name)
    {
        foreach (var button in game.GetComponentsInChildren<UnityEngine.UI.Button>())
            if (button.name == name && button.IsInteractable()) { button.onClick.Invoke(); return; }
        throw new System.InvalidOperationException("Missing available button: " + name);
    }
}
#endif
