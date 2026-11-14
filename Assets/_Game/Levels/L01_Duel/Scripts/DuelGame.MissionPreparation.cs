using System;
using System.Collections;
using UnityEngine;

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
        // The line Happy is reading on a first visit, or -1. The page shows every field from the start.
        internal int ProfileReading { get; private set; } = -1;
        Coroutine profileReading;
        // Source of the real player parts for the preparation figure; never instantiated there.
        internal OfficerVisual PlayerVisualPrefab => combatPrefab != null ? combatPrefab.GetComponentInChildren<OfficerVisual>(true) : null;
        internal BossSilhouetteView ProfileSilhouette =>
            (Wave == 1 ? gunnerPrefab : swordsmanPrefab).GetComponentInChildren<BossSilhouetteView>();

        public void OpenTargetProfile()
        {
            if (Phase != DuelPhase.Configure || Paused || DeveloperToolsOpen || !DialogueAllowsPreparationNavigation || !ui.OnPreparationOverview || Wave > FinalBossWave) return;
            profileLines = Array.FindAll(VoiceStudioCatalog.Current.lines, line => line.profileMission == Wave);
            bool first = !profileOpened[Wave];
            ProfilePage = profileLines.Length;
            profileOpened[Wave] = true;
            SetPhase(DuelPhase.TargetProfile);
            if (first) profileReading = StartCoroutine(ReadProfile());
        }

        // Happy reads the file once, line after line, while the player is free to read ahead or leave.
        IEnumerator ReadProfile()
        {
            yield return new WaitForSeconds(.6f);
            for (int i = 0; i < profileLines.Length; i++)
            {
                ProfileReading = i;
                preparationVoice.PlayLine(profileLines[i].key);
                yield return null;
                while (preparationVoice.Speaking) yield return null;
                yield return new WaitForSeconds(.15f);
            }
            ProfileReading = -1;
            profileReading = null;
        }

        void StopProfileReading()
        {
            if (profileReading != null) StopCoroutine(profileReading);
            profileReading = null;
            ProfileReading = -1;
        }

        public void BackFromProfile()
        {
            if (Phase == DuelPhase.TargetProfile && !DeveloperToolsOpen && !Paused) SetPhase(DuelPhase.Configure);
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
