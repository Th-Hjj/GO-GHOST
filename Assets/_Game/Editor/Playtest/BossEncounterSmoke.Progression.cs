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
            game.StartConfiguredEncounter();
            Require(game.Phase == DuelPhase.Training && game.Bosses[0].IsTrainingEnemy, "Missing test room");
            game.ContinueFromTraining();
            if (wave == DuelGame.FinalBossWave)
            {
                Require(game.Phase == DuelPhase.FinalBoss, "Missing voiced intro");
                Require(game.FinalBossDialogueSpeaker == "Final Boss", "Missing opening challenge");
                yield return WaitFor(() => game.Phase == DuelPhase.FinalBoss && game.FinalBossDialogueSpeaker == "Happy", "Missing Happy warning");
                yield return WaitFor(() => game.Phase == DuelPhase.Prepare, "Wave 3 intro did not start the countdown");
            }
            Require(game.Phase == DuelPhase.Prepare, "Missing pre-fight countdown");
            yield return WaitFor(() => game.Phase == DuelPhase.Fight, "Combat did not start");
            game.Player.IsInvulnerable = true;
            game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
            if (wave == 1)
            {
                yield return WaitFor(() => game.Phase == DuelPhase.Between, "Wave 1 result");
                game.ContinueToWaveTwo();
            }
            if (wave == 2)
            {
                yield return WaitFor(() => game.Phase == DuelPhase.Configure, "Wave 2 result");
                Require(!PlayMetrics.RunActive && PlayMetrics.Run.levelCleared, "Wave 2 metrics cutoff");
                closed = File.ReadAllText(path);
            }
        }

        yield return WaitFor(() => game.CutsceneChoiceOpen, "Final Boss dialogue choice", 40);
        game.ChooseFinalBossLine(0);
        yield return WaitFor(() => game.Bosses[0].Axe.PhaseTwo, "White Boxer did not start", 25);
        game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.BonusOffer, "Missing Bonus offer");
        while (!game.FinalBossChoiceReady) { game.AdvanceFinalBossDialogue(); yield return null; }
        game.ChooseBonus(true);
        Require(game.TrainingPending && game.Wave == DuelGame.BonusWave && game.Appearance == character
            && JsonUtility.ToJson(game.DraftLoadout) == rules, "Bonus lost identity or rules");
        game.StartConfiguredEncounter();
        game.ContinueFromTraining();
        Require(game.Phase == DuelPhase.Prepare && game.Bosses.Count == 2, "Missing Bonus duo");
        Require(File.ReadAllText(path) == closed, "Wave 3 or Bonus changed the closed Wave 2 record");
        game.MainMenu();
    }
}
#endif
