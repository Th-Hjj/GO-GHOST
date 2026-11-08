using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        public const int FinalBossWave = 3, BonusWave = 4;
        public string WaveLabel => Wave == BonusWave ? "BONUS WAVE" : "MISSION " + Wave;
        VoiceStudioLine[] DialogueLines => VoiceStudioCatalog.Current.BonusOffer;
        public int FinalBossLine { get; private set; }
        public string FinalBossDialogue => DialogueLines[FinalBossLine].text;
        public string FinalBossDialogueSpeaker => DialogueLines[FinalBossLine].key.StartsWith("Handler/") ? "Happy" : BossAgent.FinalBossName;
        public bool FinalBossChoiceReady { get; private set; }
        internal HandlerVoice CurrentVoice => Phase == DuelPhase.TargetProfile || Phase == DuelPhase.Training ? preparationVoice : handler;
        public string PendingDialogue => CurrentVoice.PendingDialogue;
        internal string HappySpeakerName => Phase == DuelPhase.TargetProfile ? "Happy" : handler.SpeakerName;
        public bool DialogueReady => CurrentVoice.DialogueReady;
        public bool DialogueSpeaking => PendingDialogue != null && CurrentVoice.Speaking;
        bool OnPreparationPage => Phase == DuelPhase.Configure || Phase == DuelPhase.TargetProfile || Phase == DuelPhase.Training;
        // A finished sentence can remain on screen while a fresh click takes a preparation exit.
        internal bool DialogueAllowsPreparationNavigation => OnPreparationPage && !DialogueSpeaking;
        internal int DialogueVisibleCharacters => CurrentVoice.VisibleCharacters;
        public void AcknowledgeDialogue()
        {
            if (Paused || DeveloperToolsOpen || PendingDialogue == null) return;
            if (Phase == DuelPhase.Training || InvasionHeld) input?.SuppressUiFireUntilRelease();
            CurrentVoice.Acknowledge(waitForVoice: Phase == DuelPhase.Training);
        }

        public void ContinueToWaveTwo()
        {
            if (Phase != DuelPhase.Between || Wave != 1 || DeveloperToolsOpen || PendingDialogue != null) return;
            ClearEncounter();
            Wave = 2;
            SetPhase(DuelPhase.Configure);
        }

        void StartBonusOffer()
        {
            FinalBossLine = 0;
            FinalBossChoiceReady = false;
            SetPhase(DuelPhase.BonusOffer);
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
