using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        public const double PlaytestReminderSeconds = 7 * 60;
        public bool PlaytestPromptOpen { get; private set; }
        double playtestReminderAt;
        bool playtestReminderArmed, playtestWasPaused, playtestPauseMenuNeeded;

        void StartPlaytestReminder()
        {
            // Session wall time includes creation, configuration, Training, retries and pause.
            playtestReminderAt = Time.realtimeSinceStartupAsDouble + PlaytestReminderSeconds;
            playtestReminderArmed = true;
        }

        void TickPlaytestReminder()
        {
            if (!playtestReminderArmed || PlaytestPromptOpen) return;
            if (Wave != 1 || Phase == DuelPhase.Menu || Phase == DuelPhase.Between || Phase == DuelPhase.Complete)
            {
                playtestReminderArmed = false;
                return;
            }
            if (Time.realtimeSinceStartupAsDouble < playtestReminderAt) return;
            // Resolve the frame's victory/defeat before reaching here. A real W1 win cancels this.
            playtestReminderArmed = false;
            playtestWasPaused = Paused;
            playtestPauseMenuNeeded = false;
            PlaytestPromptOpen = true;
            ApplyPause(true);
            ui.ShowPlaytestPrompt();
        }

        void ClosePlaytestPrompt(bool restoreSelection)
        {
            if (!PlaytestPromptOpen) return;
            PlaytestPromptOpen = false;
            ui.HidePlaytestPrompt(restoreSelection);
            ApplyPause(playtestWasPaused);
            input?.SuppressUiFireUntilRelease();
            // Focus loss can request a pause while the modal covers the live combat HUD.
            if (restoreSelection && playtestPauseMenuNeeded && !DeveloperToolsOpen) ui.Show();
        }

        public void AnswerPlaytestPrompt(bool jumpToWaveTwo)
        {
            if (!PlaytestPromptOpen) return;
            ClosePlaytestPrompt(!jumpToWaveTwo);
            if (!jumpToWaveTwo) return;

            // Keep installed rules and appearance; an unfinished picker edit is not installed.
            ui.DiscardRulePicker();
            FinishMetrics("skipped");
            ClearEncounter();
            Wave = 2;
            FirstConfiguration = false;
            developerTrainingSelected = false;
            Appearance.Name = Appearance.Name.Trim();
            // This is a player-facing skip, not a developer wave jump or a recorded victory.
            metricsWaveJumpPending = false;
            CreateEncounter(false);
        }
    }
}
