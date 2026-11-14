using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        public const int FinalBossWave = 3, BonusWave = 4;
        public string WaveLabel => Wave == BonusWave ? "BONUS WAVE" : "MISSION " + Wave;
        internal HandlerVoice CurrentVoice => Phase == DuelPhase.TargetProfile || Phase == DuelPhase.Training ? preparationVoice : handler;
        public string PendingDialogue => CurrentVoice.PendingDialogue;
        internal string HappySpeakerName => Phase == DuelPhase.TargetProfile ? "Happy" : handler.SpeakerName;
        public bool DialogueReady => CurrentVoice.DialogueReady;
        public bool DialogueSpeaking => PendingDialogue != null && CurrentVoice.Speaking;
        bool OnPreparationPage => Phase == DuelPhase.Configure || Phase == DuelPhase.TargetProfile || Phase == DuelPhase.Training;
        // A finished sentence can remain on screen while a fresh click takes a preparation exit.
        internal bool DialogueAllowsPreparationNavigation => OnPreparationPage && !DialogueSpeaking;
        internal int DialogueVisibleCharacters => CurrentVoice.VisibleCharacters;
        // True when a click would move on to the next line; the dialogue box shows its continue mark.
        internal bool DialogueCanAdvance => PendingDialogue != null && !Paused && DialogueReady
            && !(Phase == DuelPhase.Training && DialogueSpeaking);
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

    }
}
