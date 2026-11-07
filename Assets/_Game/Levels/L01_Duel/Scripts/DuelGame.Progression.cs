using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        public const int FinalBossWave = 3, BonusWave = 4;
        public string WaveLabel => Wave == BonusWave ? "BONUS WAVE" : "WAVE " + Wave;
        VoiceStudioLine[] DialogueLines => Phase == DuelPhase.BonusOffer
            ? VoiceStudioCatalog.Current.BonusOffer : VoiceStudioCatalog.Current.BossIntro;
        public int FinalBossLine { get; private set; }
        public string FinalBossDialogue => DialogueLines[FinalBossLine].text;
        public string FinalBossDialogueSpeaker => DialogueLines[FinalBossLine].key.StartsWith("Handler/") ? "Happy" : "Final Boss";
        public bool FinalBossChoiceReady => FinalBossLine == DialogueLines.Length - 1;

        public void ContinueToWaveTwo()
        {
            if (Phase != DuelPhase.Between || Wave != 1 || DeveloperToolsOpen) return;
            ClearEncounter();
            Wave = 2;
            SetPhase(DuelPhase.Configure);
        }

        void StartBossDialogue(DuelPhase dialoguePhase)
        {
            FinalBossLine = 0;
            SetPhase(dialoguePhase);
            handler.PlayLine(DialogueLines[0].key);
        }

        public void AdvanceFinalBossDialogue()
        {
            if (Paused || DeveloperToolsOpen || Phase != DuelPhase.BonusOffer || FinalBossChoiceReady) return;
            PlayNextBossLine();
        }

        void TickFinalBossIntro()
        {
            if (handler.Speaking) return;
            if (FinalBossChoiceReady)
            {
                input.SuppressUiFireUntilRelease();
                StartCountdown();
                return;
            }
            PlayNextBossLine();
        }

        void PlayNextBossLine()
        {
            FinalBossLine++;
            ui.Show();
            handler.PlayLine(DialogueLines[FinalBossLine].key);
        }

        public void ChooseBonus(bool enter)
        {
            if (Phase != DuelPhase.BonusOffer || DeveloperToolsOpen || !FinalBossChoiceReady) return;
            if (!enter)
            {
                MainMenu();
                return;
            }
            ClearEncounter();
            Wave = BonusWave;
            FirstConfiguration = false;
            SetPhase(DuelPhase.Configure);
        }

    }
}
