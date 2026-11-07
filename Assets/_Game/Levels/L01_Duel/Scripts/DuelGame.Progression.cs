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
        public bool FinalBossChoiceReady { get; private set; }
        public string PendingDialogue => handler.PendingDialogue;
        internal string HappySpeakerName => handler.SpeakerName;
        public bool DialogueReady => handler.DialogueReady;
        internal int DialogueVisibleCharacters => handler.VisibleCharacters;
        public void AcknowledgeDialogue()
        {
            if (!Paused && !DeveloperToolsOpen) handler.Acknowledge();
        }

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
            FinalBossChoiceReady = false;
            SetPhase(dialoguePhase);
            SpeakBossPage();
        }

        void SpeakBossPage() => handler.SpeakDialogue(DialogueLines[FinalBossLine].key, FinishBossPage);

        void FinishBossPage()
        {
            if (FinalBossLine + 1 < DialogueLines.Length)
            {
                FinalBossLine++;
                SpeakBossPage();
                ui.Show();
            }
            else if (Phase == DuelPhase.FinalBoss)
            {
                input.SuppressUiFireUntilRelease();
                ui.BeginBossOpeningReveal();
                StartCountdown();
            }
            else if (Phase == DuelPhase.BonusOffer)
            {
                FinalBossChoiceReady = true;
                ui.Show();
            }
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
