using System;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        internal const float PreparationFadeSeconds = .35f;
        readonly bool[] profileOpened = new bool[FinalBossWave + 1];
        HandlerVoice preparationVoice;
        VoiceStudioLine[] profileLines = Array.Empty<VoiceStudioLine>();
        internal VoiceStudioLine[] ProfileLines => profileLines;
        internal int ProfilePage { get; private set; }
        // Source of the real player parts for the preparation figure; never instantiated there.
        internal OfficerVisual PlayerVisualPrefab => combatPrefab != null ? combatPrefab.GetComponentInChildren<OfficerVisual>(true) : null;
        internal BossSilhouetteView ProfileSilhouette =>
            (Wave == 1 ? gunnerPrefab : swordsmanPrefab).GetComponentInChildren<BossSilhouetteView>();

        public void OpenTargetProfile()
        {
            if (Phase != DuelPhase.Configure || Paused || DeveloperToolsOpen || !DialogueAllowsPreparationNavigation || !ui.OnPreparationOverview || Wave > FinalBossWave) return;
            profileLines = Array.FindAll(VoiceStudioCatalog.Current.lines, line => line.profileMission == Wave);
            ProfilePage = profileOpened[Wave] ? profileLines.Length : 0;
            profileOpened[Wave] = true;
            SetPhase(DuelPhase.TargetProfile);
            SpeakProfilePage();
        }

        void SpeakProfilePage()
        {
            if (ProfilePage >= profileLines.Length) return;
            preparationVoice.SpeakDialogue(profileLines[ProfilePage].key, () =>
            {
                ProfilePage++;
                ui.Show();
                SpeakProfilePage();
            });
        }

        public void BackFromProfile()
        {
            if (Phase == DuelPhase.TargetProfile && !DeveloperToolsOpen && DialogueAllowsPreparationNavigation) SetPhase(DuelPhase.Configure);
        }

        public void StartMission()
        {
            if (Phase != DuelPhase.Configure || Paused || DeveloperToolsOpen || !DialogueAllowsPreparationNavigation || !ui.ConfigurationReady) return;
            ui.KeepPreparationForFade();
            CreateEncounter(false, fadePreparation: true);
            ui.FadePreparation(0);
        }
    }
}
