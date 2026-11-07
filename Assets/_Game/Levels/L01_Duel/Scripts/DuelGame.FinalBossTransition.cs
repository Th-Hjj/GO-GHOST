using System.Collections;
using UnityEngine;

namespace GoGhost.Duel
{
    // Spoken dialogue waits for a click. The pulse starts an uninterrupted visual sequence.
    public sealed partial class DuelGame
    {
        public enum CutsceneSpeaker { None, FinalBoss, Happy }
        const float RainbowDrainSeconds = 1.2f;

        Coroutine finalBossTransition;
        // Set before StartCoroutine: its first lines run before the handle is returned.
        public bool FinalBossCutscene { get; private set; }
        public CutsceneSpeaker CutsceneSpeakerId { get; private set; }
        public string CutsceneText { get; private set; }
        public string CutsceneSpeakerLabel => CutsceneSpeakerId switch
        {
            CutsceneSpeaker.FinalBoss => "Final Boss",
            CutsceneSpeaker.Happy => "Happy",
            _ => ""
        };

        // Phase-two retry (Thomas, 2026-11-06): after a phase-two defeat the player may restart phase two
        // directly with their health at the end of phase one, against a full second pool, with no replay.
        int finalBossPhaseTwoHealth = -1;
        bool resumeFinalBossPhaseTwo;
        public bool FinalBossPhaseTwoReached { get; private set; }
        public bool CanRetryFinalBossPhaseTwo => Phase == DuelPhase.Failed && Wave == FinalBossWave && FinalBossPhaseTwoReached;

        public void RetryFinalBossPhaseTwo()
        {
            if (!CanRetryFinalBossPhaseTwo || DeveloperToolsOpen) return;
            resumeFinalBossPhaseTwo = true;
            CreateEncounter(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Developer shortcut: the next Wave 3 entry starts in phase two at full player health.
        public void QueueFinalBossPhaseTwo() { resumeFinalBossPhaseTwo = true; finalBossPhaseTwoHealth = -1; }
#endif

        // Called once per Wave 3 encounter, after the rules are bound. Returns true when phase two starts now.
        bool PrepareFinalBossPhase(BossAgent boss)
        {
            bool resume = resumeFinalBossPhaseTwo;
            resumeFinalBossPhaseTwo = false;
            if (!resume || boss == null || boss.Axe == null) { FinalBossPhaseTwoReached = false; finalBossPhaseTwoHealth = -1; return false; }
            boss.Axe.StartInPhaseTwo();
            StripPlayerAssistance();
            StartPlatformMotion();
            if (finalBossPhaseTwoHealth > 0) Player.SetRuntimeHealth(Player.MaxHealth, finalBossPhaseTwoHealth);
            Player.GetComponent<OfficerVisual>()?.SetAntiqueRevolver(true);
            FinalBossPhaseTwoReached = true;
            Trace($"final boss phase 2 retry: player={Player.CurrentHealth}");
            return true;
        }

        // The first pool is spent: the Boss holds and the scripted transition takes over.
        void BeginFinalBossPhaseTwo()
        {
            if (FinalBossCutscene) return;
            finalBossPhaseTwoHealth = Player != null ? Player.CurrentHealth : -1;
            FinalBossPhaseTwoReached = true;
            FinalBossCutscene = true;
            finalBossTransition = StartCoroutine(FinalBossTransition());
        }

        IEnumerator FinalBossTransition()
        {
            var boss = System.Array.Find(actors, actor => actor != null && actor.Axe != null);
            input.SetGameplayInputEnabled(false);
            while (!boss.Motor.Grounded) yield return null;
            yield return new WaitForSeconds(.55f);

            handler.WaitForClick("......");
            Show(CutsceneSpeaker.FinalBoss, handler.PendingDialogue);
            while (handler.PendingDialogue != null) yield return null;
            handler.SpeakDialogue("FinalBossVoice/Turn1");
            Show(CutsceneSpeaker.FinalBoss, handler.PendingDialogue);
            while (handler.PendingDialogue != null) yield return null;

            Show(CutsceneSpeaker.None, null);
            boss.View.Shockwave();
            encounterSounds?.Play(Sfx.BeamFire);
            const float warningStart = .24f, warningDuration = .65f;
            yield return new WaitForSeconds(warningStart);
            Show(CutsceneSpeaker.Happy, VoiceStudioCatalog.Current.Find("Handler/EMP").text);
            handler.PlayLine("Handler/EMP");
            yield return new WaitForSeconds(warningDuration);
            handler.Stop();
            Show(CutsceneSpeaker.None, null);
            yield return new WaitForSeconds(BossSilhouetteView.ShockwaveSeconds - warningStart - warningDuration + .35f);

            StartPlatformMotion();
            yield return new WaitForSeconds(1f);

            ui.SealSidebars();
            boss.Axe.SealRules();
            StripPlayerAssistance();
            encounterSounds?.Play(Sfx.Glitch);
            yield return new WaitForSeconds(.9f);

            boss.View.DrainRainbow(RainbowDrainSeconds);
            yield return new WaitForSeconds(RainbowDrainSeconds);
            boss.View.SetFinalBossPhaseTwo();
            yield return new WaitForSeconds(.45f);
            boss.View.SetEyesRed(true);
            yield return new WaitForSeconds(.8f);
            boss.View.RiseFromKneel();
            yield return new WaitForSeconds(.6f);

            Player.GetComponent<OfficerVisual>()?.SetAntiqueRevolver(true);
            ui.FadeSidebars(PreparationSeconds);
            SetPhase(DuelPhase.Prepare);
            while (Phase == DuelPhase.Prepare) yield return null;
            boss.Axe.ReleaseTransition();
            while (boss.Axe.Transitioning) yield return null;
            finalBossTransition = null;
            FinalBossCutscene = false;
            ui.ClearSidebarSeal();
        }

        void Show(CutsceneSpeaker speaker, string text)
        {
            CutsceneSpeakerId = speaker;
            CutsceneText = text;
            ui.Show();
        }

        void ResetFinalBossTransition()
        {
            if (finalBossTransition != null) { StopCoroutine(finalBossTransition); handler.Stop(); }
            finalBossTransition = null; FinalBossCutscene = false;
            CutsceneText = null; CutsceneSpeakerId = CutsceneSpeaker.None;
            ui.ClearSidebarSeal();
        }
    }
}
