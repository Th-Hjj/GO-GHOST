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
            CutsceneSpeaker.FinalBoss => BossAgent.FinalBossName,
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

        // Called once per Mission 3 encounter, after the rules are bound.
        void PrepareFinalBossPhase(BossAgent boss)
        {
            bool resume = resumeFinalBossPhaseTwo;
            resumeFinalBossPhaseTwo = false;
            if (!resume || boss == null || boss.Axe == null) { FinalBossPhaseTwoReached = false; finalBossPhaseTwoHealth = -1; return; }
            boss.Axe.StartInPhaseTwo();
            StripPlayerAssistance();
            StartPlatformMotion();
            if (finalBossPhaseTwoHealth > 0) Player.SetRuntimeHealth(Player.MaxHealth, finalBossPhaseTwoHealth);
            FinalBossPhaseTwoReached = true;
            Trace($"final boss phase 2 retry: player={Player.CurrentHealth}");
        }

        // The first pool is spent: the Boss holds and the scripted transition takes over.
        void BeginFinalBossPhaseTwo()
        {
            if (FinalBossCutscene) return;
            finalBossPhaseTwoHealth = Player != null ? Player.CurrentHealth : -1;
            FinalBossPhaseTwoReached = true;
            FinalBossCutscene = true;
            ui.FadeSpentBossHealth();
            finalBossTransition = StartCoroutine(FinalBossTransition());
        }

        IEnumerator FinalBossTransition()
        {
            var boss = System.Array.Find(actors, actor => actor != null && actor.Axe != null);
            input.SetGameplayInputEnabled(false);
            while (!boss.Motor.Grounded) yield return null;
            yield return new WaitForSeconds(.55f);

            foreach (var line in VoiceStudioCatalog.Current.BossTransition)
            {
                handler.SpeakDialogue(line.key);
                Show(CutsceneSpeaker.FinalBoss, handler.PendingDialogue);
                while (handler.PendingDialogue != null) yield return null;
            }

            Show(CutsceneSpeaker.None, null);
            Vector2 origin = boss.Position;
            Vector2 farCorner = (Vector2)arenaBounds.extents + new Vector2(
                Mathf.Abs(origin.x - arenaBounds.center.x), Mathf.Abs(origin.y - arenaBounds.center.y));
            float reach = farCorner.magnitude;
            boss.View.Shockwave(origin, reach);
            for (int i = 1; i < ActivePlatforms.Length; i++)
            {
                var platform = ActivePlatforms[i];
                if (platform == null) continue;
                float distance = Vector2.Distance(origin, platform.ClosestPoint(origin));
                float contact = Mathf.InverseLerp(BossSilhouetteView.ShockwaveStartRadius, reach, distance);
                platform.gameObject.AddComponent<PlatformOscillator>().PrimeEmp(
                    Time.time + contact * BossSilhouetteView.ShockwaveSeconds);
            }
            encounterSounds?.Play(Sfx.EmpBurst);
            yield return new WaitForSeconds(BossSilhouetteView.ShockwaveSeconds + .35f);

            StartPlatformMotion(easeIn: true);
            yield return new WaitForSeconds(1f);

            ui.SealSidebars();
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
