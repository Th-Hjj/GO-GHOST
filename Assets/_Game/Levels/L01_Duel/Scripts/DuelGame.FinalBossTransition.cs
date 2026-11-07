using System.Collections;
using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase change (Thomas, 2026-11-06; English lines are drafts). Runs inside the fight with
    // player input off while the Boss holds his second pool, then reuses the normal three-second
    // countdown. Voiced lines come from the Voice Studio catalog; the player's two optional recordings
    // live at Resources/PlayerVoice/<name>.wav and stay silent until Thomas adds them.
    public sealed partial class DuelGame
    {
        public enum CutsceneSpeaker { None, FinalBoss, Happy, Player, Caption }
        public static readonly string[] PlayerTransitionLines = { "I'm a little nostalgic.", "I have big pockets." };
        static readonly string[] PlayerTransitionVoice = { "PlayerVoice/Nostalgic", "PlayerVoice/BigPockets" };
        const float LinePause = .35f, RainbowDrainSeconds = 1.2f;

        Coroutine finalBossTransition;
        // Set before StartCoroutine: its first lines run before the handle is returned.
        public bool FinalBossCutscene { get; private set; }
        public CutsceneSpeaker CutsceneSpeakerId { get; private set; }
        public string CutsceneText { get; private set; }
        public bool CutsceneChoiceOpen { get; private set; }
        public int CutsceneChoice { get; private set; } = -1;
        public int CutsceneStep { get; private set; }
        public string CutsceneSpeakerLabel => CutsceneSpeakerId switch
        {
            CutsceneSpeaker.FinalBoss => "Final Boss",
            CutsceneSpeaker.Happy => "Happy",
            CutsceneSpeaker.Player => Appearance != null && !string.IsNullOrWhiteSpace(Appearance.Name) ? Appearance.Name : "You",
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

        public void ChooseFinalBossLine(int choice)
        {
            if (!CutsceneChoiceOpen || Paused || choice < 0 || choice >= PlayerTransitionLines.Length) return;
            CutsceneChoice = choice; CutsceneChoiceOpen = false;
            ui.Show();
        }

        IEnumerator FinalBossTransition()
        {
            BossAgent boss = null;
            foreach (var actor in actors) if (actor != null && actor.Axe != null) boss = actor;
            var visual = Player != null ? Player.GetComponent<OfficerVisual>() : null;
            input.SetGameplayInputEnabled(false);
            CutsceneChoice = -1; CutsceneStep = 0;
            Trace("final boss phase 2: transition begins");

            CutsceneStep = 1;
            yield return Say(CutsceneSpeaker.FinalBoss, "FinalBossVoice/Turn1");

            // The shockwave: Happy's warning, then every circuit in the arena dies.
            CutsceneStep = 2;
            boss?.View?.Shockwave();
            encounterSounds?.Play(Sfx.BeamFire);
            Show(CutsceneSpeaker.Happy, VoiceText("Handler/LookOut"));
            handler.PlayLine("Handler/LookOut");
            yield return new WaitForSeconds(.35f);
            ui.SealSidebars();
            StripPlayerAssistance();
            StartPlatformMotion();
            boss?.View?.DrainRainbow(RainbowDrainSeconds);
            encounterSounds?.Play(Sfx.Glitch);
            yield return WaitForVoice(1.0f);
            yield return Say(CutsceneSpeaker.Happy, null, "…", 1.4f);

            CutsceneStep = 3;
            yield return Say(CutsceneSpeaker.FinalBoss, "FinalBossVoice/Turn2");
            boss?.Axe?.DropAxe();
            encounterSounds?.Play(Sfx.Land);
            yield return new WaitForSeconds(.7f);

            // The energy gun clicks: nothing comes out.
            CutsceneStep = 4;
            Show(CutsceneSpeaker.Caption, "*click*");
            encounterSounds?.Play(Sfx.Click);
            yield return new WaitForSeconds(1.0f);
            yield return Say(CutsceneSpeaker.FinalBoss, "FinalBossVoice/Turn3");

            // Holster the energy revolver and draw the antique one.
            CutsceneStep = 5;
            Show(CutsceneSpeaker.None, null);
            visual?.SetWeaponHidden(true);
            yield return new WaitForSeconds(.5f);
            visual?.SetAntiqueRevolver(true);
            visual?.SetWeaponHidden(false);
            encounterSounds?.Play(Sfx.Equip);
            yield return new WaitForSeconds(.5f);

            // The player's only line of the game.
            CutsceneStep = 6;
            CutsceneChoiceOpen = true;
            Show(CutsceneSpeaker.None, null);
            while (CutsceneChoice < 0) yield return null;
            yield return Say(CutsceneSpeaker.Player, PlayerTransitionVoice[CutsceneChoice], PlayerTransitionLines[CutsceneChoice], 1.6f);

            CutsceneStep = 7;
            yield return Say(CutsceneSpeaker.FinalBoss, "FinalBossVoice/Turn4");
            boss?.View?.SetEyesRed(true);
            yield return Say(CutsceneSpeaker.FinalBoss, "FinalBossVoice/Turn5");

            // The usual countdown: the sealed sidebars fade and the camera widens to the full screen.
            CutsceneStep = 8;
            Show(CutsceneSpeaker.None, null);
            ui.FadeSidebars(PreparationSeconds);
            SetPhase(DuelPhase.Prepare);
            while (Phase == DuelPhase.Prepare) yield return null;
            boss?.Axe?.ReleaseTransition();
            // The Boss ends his hold on his next tick; clear the seal only once phase two has begun.
            while (boss != null && boss.Axe != null && boss.Axe.Transitioning) yield return null;
            finalBossTransition = null; FinalBossCutscene = false;
            CutsceneStep = 9;
            ui.ClearSidebarSeal();
            Trace("final boss phase 2: transition complete");
        }

        void Show(CutsceneSpeaker speaker, string text)
        {
            CutsceneSpeakerId = speaker; CutsceneText = text;
            ui.Show();
        }

        static string VoiceText(string key) => VoiceStudioCatalog.Current.Find(key)?.text ?? "";

        // Shows a line, plays its recording when present and waits until it finishes (pause-aware).
        IEnumerator Say(CutsceneSpeaker speaker, string key, string text = null, float minimum = 1.2f)
        {
            Show(speaker, text ?? VoiceText(key));
            if (!string.IsNullOrEmpty(key)) handler.PlayLine(key);
            yield return WaitForVoice(minimum);
            yield return new WaitForSeconds(LinePause);
        }

        IEnumerator WaitForVoice(float minimum)
        {
            float until = Time.time + minimum;
            yield return null;
            while (handler.Speaking || Time.time < until) yield return null;
        }

        void ResetFinalBossTransition()
        {
            if (finalBossTransition != null) StopCoroutine(finalBossTransition);
            finalBossTransition = null; FinalBossCutscene = false;
            CutsceneChoiceOpen = false; CutsceneChoice = -1; CutsceneText = null; CutsceneSpeakerId = CutsceneSpeaker.None; CutsceneStep = 0;
            ui.ClearSidebarSeal();
        }
    }
}
