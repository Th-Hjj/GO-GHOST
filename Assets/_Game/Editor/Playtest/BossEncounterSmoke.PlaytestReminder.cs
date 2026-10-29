#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Seven Minute Reminder")]
    public static void RunPlaytestReminder() => Start("playtest-reminder", false);

    static void ReminderDue() => typeof(DuelGame).GetField("playtestReminderAt", PrivateInstance)
        .SetValue(game, Time.realtimeSinceStartupAsDouble - .01);

    static IEnumerator ClickReminderPointer(string button)
    {
        mousePosition = RectTransformUtility.WorldToScreenPoint(null, ConfigButtonNamed(button).transform.position);
        yield return Frames(2);
        mouseHeld = true; yield return Frames(2);
        mouseHeld = false; yield return Frames(3);
    }

    static IEnumerator PlaytestReminder()
    {
        checkingPause = true;
        Record("Deadline accelerated through private fixture state; runtime threshold remains 420 real seconds. No physical input or seven-minute wall-clock soak claim.");
        game.MainMenu(); yield return Frames(2);
        Check("Menu has no timer", !Private<bool>(game, "playtestReminderArmed") && !game.PlaytestPromptOpen);
        ClickConfig("New Game"); yield return Frames(2);
        double deadline = Private<double>(game, "playtestReminderAt");
        Check("New Game arms 420 real seconds at character creation", game.Phase == DuelPhase.CharacterCreation
            && DuelGame.PlaytestReminderSeconds == 420 && deadline > Time.realtimeSinceStartupAsDouble + 415);
        var appearance = game.Appearance;
        game.Appearance.Name = "Playtest";
        typeof(DuelGame).GetField("playtestReminderAt", PrivateInstance).SetValue(game, Time.realtimeSinceStartupAsDouble + .5);
        yield return RealSeconds(.1);
        Check("No early popup before deadline", !game.PlaytestPromptOpen);
        ReminderDue(); yield return Frames(3);
        Check("Creation timeout opens exact modal and blocks drawing", game.PlaytestPromptOpen && game.Paused
            && Time.timeScale == 0 && AudioListener.pause
            && Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "The playtest is ending soon. Do you want to jump to wave 2?")
            && !Object.FindAnyObjectByType<CharacterFaceCanvas>().enabled);
        ClickConfig("PlaytestNo"); yield return Frames(3);
        Check("No preserves creation and restores drawing", !game.PlaytestPromptOpen && !game.Paused && Time.timeScale == 1
            && game.Appearance == appearance && Object.FindAnyObjectByType<CharacterFaceCanvas>().enabled);
        ReminderDue(); yield return Frames(3);
        Check("No is one-shot for this New Game", !game.PlaytestPromptOpen);

        game.NewGame(); game.CompleteCharacterCreation(); yield return Frames(2);
        deadline = Private<double>(game, "playtestReminderAt");
        ClickConfig("AddRule"); yield return Frames(2);
        var ui = Object.FindAnyObjectByType<DuelUI>();
        var page = Private<RectTransform>(ui, "page");
        int selector = Private<int>(ui, "selectorRow");
        Check("Configuration retains New Game deadline", Private<double>(game, "playtestReminderAt") == deadline);
        ReminderDue(); yield return Frames(2);
        heldKeys = new[] { Key.Escape, Key.F8 }; yield return Frames(2);
        heldKeys = Array.Empty<Key>(); yield return Frames(2);
        Check("Modal blocks Escape/F8 and background rule editor", game.PlaytestPromptOpen && !game.DeveloperToolsOpen
            && !page.GetComponent<CanvasGroup>().interactable && !ConfigButtonNamed("AddRule").IsInteractable());
        ClickConfig("PlaytestNo"); yield return Frames(2);
        Check("No preserves same rule editor page and draft selection", Private<RectTransform>(ui, "page") == page
            && Private<int>(ui, "selectorRow") == selector && page.GetComponent<CanvasGroup>().interactable);

        game.NewGame(); game.CompleteCharacterCreation(); game.StartConfiguredEncounter();
        yield return Frames(5);
        deadline = Private<double>(game, "playtestReminderAt");
        Check("Training uses original New Game timer", game.Phase == DuelPhase.Training && Private<bool>(game, "playtestReminderArmed"));
        game.ContinueFromTraining(); game.StartFight();
        Check("Training Continue/W1 start do not restart timer", deadline == Private<double>(game, "playtestReminderAt"));
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        yield return Seconds(.25f);
        Check("W1 combat ticks normally before reminder", game.Phase == DuelPhase.Fight && game.FightClock > 0);
        ReminderDue(); yield return Frames(2);
        float resumedClock = game.FightClock;
        ClickConfig("PlaytestNo"); yield return Seconds(.2f);
        Check("No resumes active W1 combat", !game.Paused && !game.PlaytestPromptOpen && game.FightClock > resumedClock);
        game.NewGame(); game.CompleteCharacterCreation(); game.StartFight();
        deadline = Private<double>(game, "playtestReminderAt");
        game.SetPause(true); game.Reconfigure(); game.StartFight();
        Check("Reconfigure/retry does not restart timer", deadline == Private<double>(game, "playtestReminderAt"));
        game.SetPause(true);
        ReminderDue(); yield return Frames(3);
        float countdown = game.PreparationRemaining;
        yield return RealSeconds(.15);
        Check("Timeout during paused preparation freezes countdown", game.PlaytestPromptOpen && game.PreparationRemaining == countdown);
        ClickConfig("PlaytestNo"); yield return Frames(2);
        Check("No restores prior pause menu", game.Paused && Time.timeScale == 0 && ConfigButtonNamed("Resume").IsInteractable());
        game.SetPause(false);

        game.NewGame(); game.CompleteCharacterCreation(); game.StartFight();
        game.ToggleDeveloperTools(); ReminderDue(); yield return Frames(3);
        ClickConfig("PlaytestNo"); yield return Frames(2);
        Check("No preserves F8 and its paused encounter", game.DeveloperToolsOpen && game.Paused);
        game.ToggleDeveloperTools();
        Check("Closing restored F8 resumes normally", !game.DeveloperToolsOpen && !game.Paused && Time.timeScale == 1);

        game.NewGame(); game.CompleteCharacterCreation();
        V2Rule(game.DraftLoadout, V2Ground("Q01"));
        game.Appearance.Name = "Playtest"; appearance = game.Appearance;
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        yield return Seconds(.3f);
        var oldPlayer = game.Player; var draft = game.DraftLoadout;
        ReminderDue(); yield return Frames(3);
        float clock = game.FightClock;
        Vector3 position = game.Player.transform.position;
        int health = game.Player.CurrentHealth, shots = game.Rifle.ShotsFired;
        yield return RealSeconds(.25);
        Check("Combat modal freezes clock, movement, HP and shots", game.PlaytestPromptOpen && game.FightClock == clock
            && game.Player.transform.position == position && game.Player.CurrentHealth == health && game.Rifle.ShotsFired == shots);
        yield return LayoutCapture("playtest-reminder");
        yield return ClickReminderPointer("PlaytestYes");
        Check("Yes creates fresh W2 preparation with same name/face/rules", game.Wave == 2 && game.Phase == DuelPhase.Prepare
            && game.Player != oldPlayer && game.Appearance == appearance && game.Appearance.Name == "Playtest"
            && game.DraftLoadout == draft && game.Bosses.Count == 1 && game.Bosses[0].Profile.Kind == BossKind.Severance
            && !game.PlaytestPromptOpen && !game.Paused && Time.timeScale == 1 && !AudioListener.pause);
        Check("Skip is recorded without a death, win or developer jump", PlayMetrics.History.Any(a => a.wave == 1 && a.result == "skipped")
            && PlayMetrics.DeathsByWave[1] == 0 && !PlayMetrics.Current.developerWaveJump);
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        yield return Seconds(.4f);
        Check("W2 combat advances after Yes", game.Phase == DuelPhase.Fight && game.FightClock > 0);
        ReminderDue(); yield return Frames(2);
        Check("No reminder in W2", !game.PlaytestPromptOpen && !Private<bool>(game, "playtestReminderArmed"));
        game.Bosses[0].Health.TakeDamage(99999, game.Player.gameObject); yield return Seconds(1);
        Check("Controlled W2 win continues to W3 configuration", game.Wave == 3 && game.Phase == DuelPhase.Configure);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        yield return Seconds(.3f);
        Check("W3 still runs both Bosses", game.Wave == 3 && game.Bosses.Count == 2 && game.FightClock > 0);

        game.NewGame(); game.CompleteCharacterCreation(); game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        game.Bosses[0].Health.TakeDamage(99999, game.Player.gameObject); ReminderDue(); yield return Frames(3);
        Check("W1 victory on deadline frame wins over prompt", game.Phase == DuelPhase.Between && !game.PlaytestPromptOpen
            && !Private<bool>(game, "playtestReminderArmed"));
        yield return Seconds(1);
        Check("Normal W1 victory still leads to W2 setup", game.Wave == 2 && game.Phase == DuelPhase.Configure);
        game.NewGame(); ReminderDue(); yield return Frames(2);
        ClickConfig("PlaytestYes"); yield return Frames(2);
        Check("Yes also works directly from character creation", game.Wave == 2 && game.Phase == DuelPhase.Prepare);
        game.MainMenu(); yield return Frames(2);
        Check("Menu cleans modal/time/audio and disarms timer", !game.PlaytestPromptOpen && !Private<bool>(game, "playtestReminderArmed")
            && !game.Paused && Time.timeScale == 1 && !AudioListener.pause);
        game.NewGame(); game.CompleteCharacterCreation(); game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        game.Player.TakeDamage(99999, game.Bosses[0].gameObject); yield return Frames(3);
        Check("Controlled defeat leaves reminder armed", game.Phase == DuelPhase.Failed && Private<bool>(game, "playtestReminderArmed"));
        ReminderDue(); yield return Frames(2);
        ClickConfig("PlaytestYes"); yield return Frames(2);
        Check("Yes works from defeat without overwriting death result", game.Wave == 2 && game.Phase == DuelPhase.Prepare
            && PlayMetrics.History.Count(a => a.result == "died") == 1 && PlayMetrics.DeathsByWave[1] == 1);
        game.NewGame(); game.CompleteCharacterCreation(); game.StartConfiguredEncounter(); yield return Frames(3);
        ReminderDue(); yield return Frames(3);
        game.SendMessage("OnApplicationFocus", false);
        yield return ClickReminderPointer("PlaytestNo");
        Check("Pointer No after focus loss restores usable pause menu", !game.PlaytestPromptOpen && game.Paused
            && ConfigButtonNamed("Resume").IsInteractable());
        ClickConfig("Resume"); yield return Seconds(.2f);
        Check("Resume works and modal click did not fire through", !game.Paused && game.FightClock > 0 && game.Rifle.ShotsFired == 0);
        game.NewGame(); game.CompleteCharacterCreation(); game.StartConfiguredEncounter(); yield return Frames(2);
        ReminderDue(); yield return Frames(2);
        // Training's navigation is on the HUD sibling, outside Page's CanvasGroup.
        mousePosition = RectTransformUtility.WorldToScreenPoint(null, ConfigButtonNamed("Continue →").transform.position);
        yield return Frames(2); mouseHeld = true; yield return Frames(2); mouseHeld = false; yield return Frames(2);
        Check("Fullscreen modal blocks Training HUD navigation clicks", game.PlaytestPromptOpen && game.Phase == DuelPhase.Training);
        yield return ClickReminderPointer("PlaytestYes");
        Check("Pointer Yes from Training enters W2", game.Phase == DuelPhase.Prepare && game.Wave == 2 && !game.PlaytestPromptOpen);
        checkingPause = false;
    }
}
#endif
