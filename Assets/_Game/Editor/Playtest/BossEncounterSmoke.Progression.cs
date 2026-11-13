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
            Require(game.Phase == DuelPhase.Configure, "Test Room did not return to preparation");
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

        // Phase one stops at 660 and 330 HP until each invasion is read; keep spending the pool between them.
        var finalBoss = game.Bosses[0];
        yield return WaitFor(() =>
        {
            if (!game.FinalBossCutscene && finalBoss.Health.CurrentHealth > 0)
                finalBoss.Health.TakeDamage(100000, game.Player.gameObject);
            return game.FinalBossCutscene;
        }, "Phase one did not reach the kneeling transition", 60);
        Require(game.InvadedRuleCount == 2, "Expected both phase-one invasions, saw " + game.InvadedRuleCount);
        foreach (var line in new[] { "You're strong.", "But it's not over yet.", "Let’s see what you can do on your own." })
            yield return WaitFor(() => game.CutsceneText == line && game.PendingDialogue == line, "Missing kneeling dialogue: " + line, 25);
        yield return WaitFor(() => game.Bosses[0].Axe.PhaseTwo, "Phase two did not start", 30);
        game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.FinalVictoryHold, "Missing Boss defeat pause");
        int finalHealth = game.Player.CurrentHealth;
        game.Player.TakeDamage(100000, finalBoss.gameObject);
        Require(game.Player.CurrentHealth == finalHealth && !finalBoss.Running, "Damage continued during victory");
        yield return WaitFor(() => game.Phase == DuelPhase.Complete, "Missing final completion screen");
        yield return null;
        Require(System.Array.Exists(game.GetComponentsInChildren<TMPro.TMP_Text>(), text => text.text == "You've Finished The Game!"),
            "Missing completion message");
        Require(File.ReadAllText(path) == closed, "Mission 3 changed the closed Wave 2 record");
        Click("Main Menu");
        Require(game.Phase == DuelPhase.Menu && game.Player == null && !game.Paused && !AudioListener.pause
            && Time.timeScale == 1, "Main Menu did not clear combat");
    }

    static void Click(string name)
    {
        foreach (var button in game.GetComponentsInChildren<UnityEngine.UI.Button>())
            if (button.name == name && button.IsInteractable()) { button.onClick.Invoke(); return; }
        throw new System.InvalidOperationException("Missing available button: " + name);
    }
}
#endif
